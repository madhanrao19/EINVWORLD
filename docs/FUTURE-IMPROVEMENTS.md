# Future Improvements

Work that has been investigated but **not approved for implementation**. Each entry records status, evidence and a
proposed design so it can be picked up without redoing the research. **Nothing here is built. Do not implement an entry
until its status says Approved** (see the approval gates in `CLAUDE.md`). Editing this file never activates a feature.

**Labels:** ✅ **Verified**: confirmed by official LHDN documentation or recorded test evidence (source given) ·
🔬 **Unverified**: an assumption still to be tested · 📝 **Proposed**: a draft design, not approved.

Identifiers are masked the way `LogSanitizer.MaskTin` masks them (e.g. `IG20*******70`). Full TINs, UUIDs, amounts and
personal details stay out of this file.

---

## 1. LHDN taxpayer credentials for received invoices

| Stage | Status |
|---|---|
| Phase 0: root-cause investigation | ✅ **Completed** 2026-11-04 |
| Phase 1: validate taxpayer credentials (read-only) | ⏳ **Pending.** Owner to register an ERP for an individual (IG) TIN in MyInvois preprod |
| Implementation | ⛔ **Not approved** |

Related: PR #457 (v1.50.35, Sent + Received import).

### Problem
Invoices created directly in the MyInvois portal, or issued to a company by **other suppliers' ERPs**, never appear in
EINVWORLD's **Received** tab. Reproduced on preprod: a Valid invoice issued in the portal by supplier `C289*****70` to buyer
`IG20*******70` (UUID `54VV…6M10`) shows on the portal's Received tab but is never imported.

### Root cause ✅ (Phase 0)
EINVWORLD authenticates every company as an **intermediary** (`Login as Intermediary System` + `onbehalfof`). The official
[MyInvois SDK FAQ](https://sdk.myinvois.hasil.gov.my/faq/) says:

> "Due to privacy regulations and confidentiality considerations, intermediaries will only be allowed to view and access
> e-Invoices that they have submitted on behalf of taxpayers. Intermediaries are not permitted to access or retrieve
> e-Invoices submitted independently by taxpayers or e-Invoices received by taxpayers."

This is LHDN policy, not a sync bug, and no intermediary-side setting or portal permission works around it.
**Consequence:** v1.50.35 imports received documents correctly *when LHDN returns them*. Under intermediary auth LHDN only
returns documents EINVWORLD submitted, so third-party received invoices will not appear in Production either.

### Evidence ✅ (read-only preprod probes, 2026-11-04)
| Test (intermediary token, `onbehalfof` = buyer `IG20*******70`) | Result |
|---|---|
| `documents/recent`: no filter / Received / Received + `issuerTin` = the supplier | 7 / 4 / 0 docs |
| `documents/search`, 30 days: by submission date (Received) / by issue date (all) | 27 / 38 docs |
| `documents/search` with the `uuid` filter for the missing invoice | 0 docs |
| `documents/{uuid}/raw` and `/details` for the missing invoice | **404 / 404** |
| Same calls without the per-request `onbehalfof` header | Identical (the taxpayer is fixed inside the token) |
| Same calls as the issuer (`onbehalfof` = supplier `C289*****70`) | 0 docs, 404 |
| Token for EINVWORLD's own system TIN (no `onbehalfof`) | 0 docs, 404 |
| Control: a document EINVWORLD itself submitted | 200, Valid |

All 38 visible documents had `submissionChannel = IntermediaryERP` (a value the SDK doesn't list: it documents `ERP`,
`Invoicing Portal` and `InvoicingMobileApp`) and were issued by the represented TIN itself.
**Ruled out:**
- date filter type and window (both inside 31 days);
- pagination (one page of 100 or fewer);
- direction (Sent / Received / none);
- endpoint (recent, search, raw, details);
- header placement;
- indexing delay (probed about 1 hour after issue).

The Staging logs agree: two Refresh jobs after the v1.50.35 deploy returned 38 and 43 UUIDs, all already stored locally.

### LHDN rules that shape the design (SDK re-checked 2026-11-04)
- ✅ **[Search Documents](https://sdk.myinvois.hasil.gov.my/einvoicingapi/09-search-documents/)**: needs an issue-date
  **or** submission-date pair. The **span of one query** may not exceed a configurable limit, currently **31 days**. The SDK
  states **no age limit** on the documents themselves. `pageSize` is 1–100, and responses include `totalPages`/`totalCount`.
  Recommended maximum: **12 requests/minute per Client ID**, plus per-taxpayer throttling of 1 request every 5 seconds.
- ✅ **[Get Recent Documents](https://sdk.myinvois.hasil.gov.my/einvoicingapi/05-get-recent-documents/)**: only documents
  **issued within the last 31 days**, at most 10,000 results, same 12 RPM recommendation. `issuerTin` filter only with
  Direction = Received.
- ✅ **[Get Document](https://sdk.myinvois.hasil.gov.my/einvoicingapi/07-get-document/)**: issuers can retrieve documents in any
  status. **Receivers can retrieve only Valid or Cancelled** documents (others return not-found). Invalid documents are
  never returned; Get Document Details returns their details instead.
- ✅ **[Login as Taxpayer](https://sdk.myinvois.hasil.gov.my/api/07-login-as-taxpayer-system/)**: the token is a JWT valid for
  60 minutes and "includes information about the taxpayer", but **no claim names are documented**.
- ✅ **[FAQ](https://sdk.myinvois.hasil.gov.my/faq/)**: taxpayers self-provision Client ID + two secrets under
  **View and Register ERP**. **Both secrets expire simultaneously**: secret 2 is a backup in case one is compromised, not a
  way to keep working past expiry. Preprod and Production need **different** credentials.
- 🔬 How far back Search actually reaches with a taxpayer token, whether lookup by UUID works for documents older than 31 days,
  whether an IG TIN can register an ERP, and which (if any) token claim identifies the taxpayer. All are tested in Phase 1.

### Phase 1: validation before any code (⏳ pending)
**Owner-only steps.** Never paste credentials into chat, commits, tickets or this file.
1. Sign in to **preprod** MyInvois as the IG taxpayer → Taxpayer Profile → **View and Register ERP**. Record whether an
   individual (IG) TIN may register an ERP.
2. If it can, register a test ERP and store it **only** in local user-secrets (test-only keys that the app never reads):
   ```
   dotnet user-secrets set "Phase1:IG:ClientId" "<client id>"
   dotnet user-secrets set "Phase1:IG:ClientSecret" "<client secret>"
   ```
   Afterwards: `dotnet user-secrets remove` both keys, and delete the test ERP in the portal if it's no longer needed.

**Read-only probe** (token + GET only; at most 12 requests/minute; stop on the first 429 and honour `Retry-After`). It prints
counts, masked UUIDs/TINs, statuses and channels only. It never prints a token, secret, claim *value* or document body.
| # | Call | Proves |
|---|------|--------|
| T1 | Taxpayer token (no `onbehalfof`); decode the JWT **locally** and list claim **names** only | Taxpayer auth works; candidate identity claim for I3 (still not authoritative until LHDN documents it) |
| T2 | `documents/search` 30 days, Received + Sent, issue and submission date, all pages via `totalPages` | Missing invoice found; third-party received docs appear |
| T3 | `documents/recent` Received, and with the supplier's `issuerTin` | Missing invoice found through Get Recent |
| T4 | `documents/{uuid}/details` and `/raw` for the missing invoice | Direct UUID retrieval |
| T5 | Taxpayer token: details for 3 documents EINVWORLD submitted earlier | Previously submitted docs stay visible |
| T6 | Taxpayer token **with** an `onbehalfof` header on a GET | Whether the read path must omit the header |
| T7 | Intermediary token again: the same 3 details + one `recent` | Intermediary access and status monitoring unaffected |
| T8 | Group T2 results by `submissionChannel` / issuer | Portal and other-supplier documents are visible |
| T9 | Taxpayer token: details for one known document issued **more than 31 days ago** | Whether UUID lookup works for older documents |
| T10 | Taxpayer token: `documents/search` over an **older** window (e.g. days 40–60 ago, span ≤ 31) with pagination | Whether old documents can be *discovered*: a 31-day span limit vs a 31-day history cutoff |

No submit, cancel or reject calls are made. The next scheduled `[LHDN Import]` lines in Staging SystemLogs should be unchanged.

**Pass:** T1 OK; T2–T4 return the missing invoice; T5 200 for all; T7 unchanged; T8 shows non-`IntermediaryERP` channels.
T1 claim names, T6, T9 and T10 are recorded as design inputs (T9/T10 may be inconclusive without a suitable older document).
**If IG registration is unavailable:** record the portal evidence and look for an officially supported alternative before
deciding product eligibility. Don't treat notification emails as a complete import, and don't build an unsupported workaround.

### Proposed implementation 📝 (draft; approval only after Phase 1)
Use the company's **own ERP credentials for document discovery and retrieval only**. Intermediary submission, cancellation,
signing, retry and idempotency stay **unchanged**. Opt-in per company. Companies without credentials behave as today.

**Authentication routing.** Implement it as an explicit, typed operation (e.g. `LhdnOperation.Discover | ReadThirdParty |
SubmissionOwned`), never a boolean. Inventory every caller before wiring.
| Operation | Identity | Rule |
|---|---|---|
| Submit, retry, cancel, signing, post-submit poll (`InvoiceSubmissionHelper`, submit job handlers) | Intermediary (unchanged) | Untouched code path, idempotency and audit |
| Status sync of documents **EINVWORLD submitted** (`InvoiceStatusUpdater`, `SyncLhdnInvoiceStatusAsync`, `StatusSync` jobs) | Intermediary (unchanged) | Route by recorded origin, never by trial-and-error |
| Discovery: search/recent in the scheduled import, `SupplierRefresh`, `FullImport` | Taxpayer if enabled for that company + environment; else intermediary | Never switch identity on error |
| Retrieve raw/details + status sync of **third-party / portal** documents | Taxpayer only | Without an enabled credential: don't call (it would 404); show "not available in intermediary mode" |
| Token pre-renewal (`TokenRenewalService`) | Intermediary only | Taxpayer tokens are fetched on demand, never pre-renewed |
| Reject / request rejection / cancel of documents not submitted by EINVWORLD | **Out of scope** | Separate business, permission and approval review |

**Isolation and failure rules** (each one enforced in code and covered by tests)
- **I1 Company:** a credential belongs to exactly one `PartyInfo` and is resolved from the company being synced, never from
  user input. The resolved row's TIN must equal that company's TIN, and the operation's TIN must match too.
- **I2 Environment:** store an explicit environment (`Preprod` | `Production`) and the canonical API origin (scheme + host) on the
  row. On **every use**, check both against the running `LHDNApiConfig:BaseUrl` against an allowlist. A mismatch fails closed, so a Staging copy of Production data
  can never call Production. DataProtection key-ring separation helps but is **not** relied on as the control.
- **I3 Ownership.** Must also work for companies with no invoices yet; never inferred from search results.
  1. **Provenance (required):** a company-authorised user obtains the credentials from that company's own **View and Register
     ERP** page and records consent. The Admin records who supplied them and when, audited.
  2. **Technical check (only if Phase 1 finds an officially supported identifier):** Test connection compares it to the row's
     TIN. An undocumented JWT claim is at most a mismatch *warning*, never proof.
  3. **Continuous guard (always on):** every document returned under a credential must name that TIN as supplier/issuer or
     buyer/receiver. A single violation stops the batch (nothing is imported), disables the credential and alerts the Admin.
  If neither (1) nor (2) can be met with confidence, the credential stays disabled. Record the gap and seek LHDN guidance.
- **I4 Token cache:** key and lock by `PartyInfoId` + environment + TIN + auth mode + credential version (never the secret; never
  log the raw key). Taxpayer tokens stay in memory only. Any save/rotate/disable/delete bumps the version, which evicts the token.
  A 401 evicts and retries **once** with the same identity.
- **I5 No silent fallback:** a taxpayer-auth failure on an enabled credential records a sanitised `LastError`, stops that
  company's taxpayer reads and shows an actionable message. It **never** falls back to intermediary for those operations and
  never sets `LhdnIntermediaryRejected`. Only an audited Admin disable returns the company to intermediary discovery.
- **I6 Concurrency:** credential edits use `RowVersion`; a conflict reads "changed by someone else, reload".
- **I7 In-flight jobs:** a job snapshots company, environment, auth mode and credential version at start. If the version
  changes mid-run, it stops at the next page boundary and requeues. It never continues with a different identity. Retry time is bounded.
- **I8 Expiry and rotation:** store `SecretExpiresOn` (as shown in the portal). Warn at 30/14/7 days in the Admin page and the
  existing Needs Attention alerts. Because both secrets expire together, rotation = generate new secrets in the portal → enter →
  Test → atomic swap (version bump). After expiry, the credential is shown as **Expired**, taxpayer reads stop (I5), and nothing falls back.
- **I9 Secrets:** encrypted at rest with the existing `_secretProtector` + `ProtectedStringConverter`
  (`Data/ApplicationDbContext.cs`, as for `WebhookSubscription.Secret`). Write-only in the UI. Never in logs, audit entries,
  exceptions, `LastError` or API responses. Finance users never see them.

**Storage:** new table `LhdnTaxpayerCredentials`: `PartyInfoId` (FK), `Tin`, `Environment`, `ApiOrigin`, `ClientId`,
`Secret1`, `Secret2?`, `SecretExpiresOn?`, `Status` (Draft/Enabled/Disabled/Expired/Quarantined), `CredentialVersion`,
`LastVerifiedUtc`, `LastError`, provenance (supplied-by, consent reference), created/updated by + at, `RowVersion`. Unique
(`PartyInfoId`, `Environment`). Additive migration with the usual 4 artifacts.

**Synchronisation**
- **Incremental:** keep the scheduled import (`InvoiceStatusUpdater`, `BackgroundImportLookbackDays` = 7). For taxpayer mode, add a
  durable checkpoint per (company, environment, credential version, direction) on **submission date**, because issue dates can be
  backdated. Each run searches `[checkpoint − overlap, now]` (overlap e.g. 24 h; dedupe by UUID makes the overlap safe). It pages via
  `totalPages` and advances the checkpoint **only after every page of the window was processed**. At most 12 requests/minute through the
  existing `LhdnRateLimitHandler`; a 429 honours `Retry-After`, and a partial window resumes from the same checkpoint.
- **Backfill:** a separate, resumable checkpoint walking backwards in ≤ 31-day windows. Its depth is set only by T9/T10
  results. Each run is bounded by the existing 40-fetch cap + continuation job. Validate it by comparing per-month counts against
  a portal export for one company before enabling it widely.
- **Idempotency:** dedupe by UUID within company + environment. ✅ **Code fact:** `InvoiceHeaders.UUID` has a **non-unique** index,
  so concurrent imports (scheduled + Refresh) could insert duplicates. Staging has 0 duplicate UUIDs (read-only count,
  2026-11-04). 📝 Add a filtered **unique** index on UUID after confirming 0 duplicates on Production too, and handle the
  unique-violation as "already imported".
- **Emails:** keep the per-invoice `IsNewInvoiceReceivedEmailSent` flag plus the 1-day received-email window. Backfilled
  documents never send emails. Tests prove that overlap windows and re-runs send nothing twice.
- **Finance-facing status:** the Received tab shows the mode ("Using company credentials" / "Intermediary: only invoices sent
  through EINVWORLD are visible"), last successful sync, current state and the last actionable error. Manual Refresh uses
  the same limits and checkpoint; it never bypasses them.

**Delivery.** No stage exposes a control that doesn't work, or activates a half-built feature.
1. **PR 1, dormant backend:**
   - storage + migration, encryption;
   - typed routing, isolation guards, checkpoint logic;
   - a server-side flag `LhdnTaxpayerCredentials:Enabled` (default **false** everywhere);
   - unit + integration tests.

   No UI and no way to create rows, so runtime behaviour is unchanged.
2. **PR 2, Admin page + wiring together, behind the flag:**
   - `Pages/Admin/LhdnCredentials` (Admin role only, anti-forgery, in `AuthorizationMatrixTests`, `Pages/Admin/Webhooks` pattern);
   - Draft → Test connection → Enable, with audit (`AuditService`: actor, company, masked TIN, ClientId last 4, result; never the secret);
   - with the flag off, the page returns 404 and the read path ignores rows.

   Credentials are entered **only** through this page: never SQL, config or scripts, including on Staging.
3. **Staging enablement:** flag on for Staging only (an approved config change), one preprod test company, full regression.
4. **Production:** flag on only after approval, then one company at a time once it registers a **Production** ERP.

**Tests**
- Unit:
  - routing table: every operation → identity, including the "don't call" case;
  - token request fields/headers via a fake `HttpMessageHandler`, and `onbehalfof` omitted for taxpayer tokens;
  - cache/lock isolation across company/environment/TIN/mode/version;
  - environment mismatch refused, ownership-guard violation quarantines the batch, no fallback, expiry handling;
  - checkpoint advance and resume, overlap dedupe;
  - secrets/tokens never in log output (captured logger).
- Integration (real SQL Server):
  - secret encrypted at rest; unique indexes; `RowVersion` conflict;
  - audit entries contain no secrets;
  - Admin-only access in `AuthorizationMatrixTests`;
  - concurrent imports insert one row per UUID.
- Staging regression:
  - the missing preprod invoice imports once, with one email at most;
  - intermediary submit, cancel and status poll unchanged;
  - a second company still on intermediary;
  - disable/rotate/expire behave as specified.

**Rollback:** disable the credential (audited) or turn the flag off. Discovery returns to intermediary and third-party reads stop.
Redeploying the previous build ignores the additive table.

### Open risks
- 🔬 An individual (IG) TIN may not be allowed to register an ERP (Phase 1 step 1).
- 🔬 There may be no officially supported way to prove credential ownership technically. I3 then depends on provenance + the continuous guard.
- 🔬 How deep backfill can go is unknown until T9/T10.
- Both secrets expire together: a missed rotation stops taxpayer reads for that company on the expiry date.
- Each company must register its own **Production** ERP. Holding a taxpayer's ERP credentials makes EINVWORLD that company's
  read system of record, so explicit, revocable company consent is required.
