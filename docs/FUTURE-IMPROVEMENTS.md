# Future Improvements

Work that has been investigated but **not approved for implementation**. Each entry records its status, the evidence,
and a proposed design, so it can be picked up later without redoing the research. Nothing here is built. Do not
implement an entry until its status says **Approved**.

Identifiers are masked the same way `LogSanitizer.MaskTin` masks them in logs (e.g. `IG20*******70`). Full TINs, UUIDs, amounts and
personal details stay out of this file. Look them up in the database or the MyInvois portal when needed.

---

## 1. LHDN taxpayer credentials for received invoices

| Stage | Status |
|---|---|
| Phase 0: root-cause investigation | ✅ **Completed** 2026-11-04 |
| Phase 1: validate taxpayer credentials (read-only) | ⏳ **Pending.** Waiting on the owner to register an ERP for an individual (IG) TIN in MyInvois preprod |
| Implementation | ⛔ **Not approved** |

Related: PR #457 (v1.50.35, Sent + Received import).

### Problem
Invoices created directly in the MyInvois portal, or issued to a company by **other suppliers' ERPs**, never appear in
EINVWORLD's **Received** tab. Reproduced on preprod with a Valid invoice issued in the portal by supplier `C289*****70`
to buyer `IG20*******70` (UUID `54VV…6M10`). It shows on the MyInvois portal's Received tab, but EINVWORLD never imports it.

### Root cause (Phase 0)
EINVWORLD authenticates as an **intermediary** (`Login as Intermediary System`, `onbehalfof` header) for every company.
LHDN restricts what intermediaries can see. From the official [MyInvois SDK FAQ](https://sdk.myinvois.hasil.gov.my/faq/):

> "Due to privacy regulations and confidentiality considerations, intermediaries will only be allowed to view and access
> e-Invoices that they have submitted on behalf of taxpayers. Intermediaries are not permitted to access or retrieve
> e-Invoices submitted independently by taxpayers or e-Invoices received by taxpayers."

This is an LHDN policy, not a sync bug, and the intermediary model cannot work around it. The portal's intermediary permissions
(Document View/Submit/Cancel/Request Rejection, Notifications View) do not widen it. **Consequence:** v1.50.35 imports
received documents correctly *when LHDN returns them*. Under intermediary auth LHDN only returns documents EINVWORLD
submitted, so third-party received invoices will not appear in Production either.

### Evidence (read-only preprod probes, 2026-11-04)
| Test (intermediary token, `onbehalfof` = buyer `IG20*******70`) | Result |
|---|---|
| `documents/recent`: no filter / Received / Received + `issuerTin` = the supplier | 7 / 4 / 0 docs |
| `documents/search`, 30 days: by submission date (Received) / by issue date (all) | 27 / 38 docs |
| `documents/search` with the `uuid` filter for the missing invoice | 0 docs |
| `documents/{uuid}/raw` and `/details` for the missing invoice | **404 / 404** |
| Same calls without the per-request `onbehalfof` header | Identical (the taxpayer is fixed inside the token) |
| Same calls as the issuer (`onbehalfof` = supplier `C289*****70`) | 0 docs, 404 |
| Taxpayer token for EINVWORLD's own system TIN | 0 docs, 404 |
| Control: a document EINVWORLD itself submitted | 200, Valid |

All 38 visible documents had `submissionChannel = IntermediaryERP` and were issued by the represented TIN itself.
**Ruled out:**
- date filters: issue and submission date, both inside the 31-day window;
- pagination: well under one 100-row page;
- direction: Sent / Received / none;
- endpoint: recent / search / raw / details;
- header placement;
- indexing delay: probed about 1 hour after issue.

The Staging logs agree: two Refresh jobs after the v1.50.35 deploy returned 38 and 43 UUIDs, and every one was already stored locally.

SDK references: [Login as Intermediary](https://sdk.myinvois.hasil.gov.my/api/08-login-as-intermediary-system/) ·
[Login as Taxpayer](https://sdk.myinvois.hasil.gov.my/api/07-login-as-taxpayer-system/) ·
[Get Recent Documents](https://sdk.myinvois.hasil.gov.my/einvoicingapi/05-get-recent-documents/) ·
[Search Documents](https://sdk.myinvois.hasil.gov.my/einvoicingapi/09-search-documents/) ·
[Get Document](https://sdk.myinvois.hasil.gov.my/einvoicingapi/07-get-document/).

### LHDN rules that shape the design
- **The 31-day limit is a *discovery* limit, not a retention limit.** Get Recent Documents "will only return documents that are
  issued within the last 31 days", and a Search Documents date range may not exceed 31 days. Documents stay on MyInvois
  and in the portal much longer. Get Document / Get Document Details look a document up **by UUID**, and the SDK pages
  reviewed state no 31-day limit for them (Phase 1 test T9 checks this). So:
  - Ongoing sync (Refresh every 30 days or less) loses nothing.
  - Documents older than 31 days can't be *discovered* through the API. They can only be imported if their UUID is already
    known, e.g. from LHDN's notification emails or a portal export. Any historical backfill beyond 31 days is out of scope until
    someone designs a UUID-list import.
- Receivers only ever see documents in **Valid** or **Cancelled** status.
- Taxpayers self-provision their Client ID / two client secrets under **View and Register ERP**. **Both secrets expire
  together.** Preprod and Production need **different** credentials.

### Phase 1: validation before any code (pending)
**Owner-only steps.** Never paste credentials into chat, commits, tickets or this file.
1. Sign in to **preprod** MyInvois as the IG taxpayer → Taxpayer Profile → **View and Register ERP**. Record whether
   an individual (IG) TIN may register an ERP.
2. If it can, register a test ERP. Store it **only** in the local user-secrets store:
   ```
   dotnet user-secrets set "Phase1:IG:ClientId" "<client id>"
   dotnet user-secrets set "Phase1:IG:ClientSecret" "<client secret>"
   ```
   These are test-only keys that nothing in the app reads. Remove them after Phase 1 (`dotnet user-secrets remove …`) and
   delete the test ERP in the portal if it's no longer needed.

**Read-only probe** (token + GET calls only, about 20 calls, stop on the first 429). It prints counts, masked UUIDs, statuses,
masked issuers and channels. It never prints a token, secret or document body.
| # | Call | Proves |
|---|------|--------|
| T1 | Taxpayer token (no `onbehalfof`) with the IG credentials; list the JWT claim **names** and the TIN claim (masked) | Taxpayer auth works; whether the token identifies its TIN (needed for isolation check I3) |
| T2 | `documents/search` 30 days, Received + Sent, issue and submission date, all pages | Missing invoice found; third-party received docs appear |
| T3 | `documents/recent` Received, and filtered by the supplier's `issuerTin` | Missing invoice found through Get Recent |
| T4 | `documents/{uuid}/details` and `/raw` for the missing invoice | Direct UUID retrieval |
| T5 | Taxpayer token: details for 3 documents EINVWORLD submitted earlier | Previously submitted docs stay visible |
| T6 | Taxpayer token **with** an `onbehalfof` header on a GET | Whether the read path must strip the header |
| T7 | Intermediary token again: the same 3 details + one `recent` | Intermediary access and status monitoring unaffected |
| T8 | Group T2 results by `submissionChannel` / issuer | Portal and other-supplier documents are visible |
| T9 | Taxpayer token: details for one document issued **more than 31 days ago** | Whether lookup by UUID works outside the 31-day window |

No submit, cancel or reject calls are made. The next scheduled `[LHDN Import]` lines in Staging SystemLogs should be unchanged.

**Pass:** T1 OK; T2–T4 return the missing invoice; T5 200 for all; T7 unchanged; T8 shows non-`IntermediaryERP` channels.
T6, T9 and the T1 claim list are recorded as design inputs.
**Fail at step 1** (IG can't register an ERP): offer the feature to company TINs only, and IG users rely on LHDN's buyer
notification emails. No code workaround exists, because LHDN enforces the limit.

### Proposed implementation (draft, for approval after Phase 1)
Use the company's **own ERP credentials for document discovery and retrieval only**. Intermediary submission,
cancellation, signing, retry and idempotency stay **unchanged**. The feature is opt-in per company. Companies without credentials
behave exactly as today.

**Storage**
- New table `LhdnTaxpayerCredentials`: `PartyInfoId` (FK), `Tin`, `ApiHost`, `ClientId`, `Secret1`, `Secret2?`,
  `SecretExpiresOn?`, `IsEnabled`, `LastVerifiedUtc`, `LastError` (sanitised, no secrets), created/updated by + at,
  `RowVersion`. Unique index on (`PartyInfoId`, `ApiHost`).
- Secrets encrypted at rest with the existing `_secretProtector` + `ProtectedStringConverter` (`Data/ApplicationDbContext.cs`,
  the same pattern as `WebhookSubscription.Secret`). Additive migration with the usual 4 artifacts.

**Isolation rules (all enforced in code and covered by tests)**
- **I1 Company:** a credential belongs to exactly one `PartyInfo`. It's looked up by the TIN being synced **and** must match
  that company's `PartyInfoId`. It's never chosen by user input or a request parameter.
- **I2 Environment:** a credential is used only when `ApiHost` equals the host of the running `LHDNApiConfig:BaseUrl`. A Staging copy of
  Production data can never call Production LHDN. The DataProtection key ring is per site, so copied ciphertext can't be decrypted anyway.
  Copied rows show as "Unavailable in this environment" and are never used.
- **I3 Identity:** **Test connection** must prove the token belongs to the row's TIN (JWT TIN claim if T1 confirms there is one;
  otherwise the row can't be enabled until a search returns at least one document issued by or to that TIN). This stops company A's
  credentials being saved against company B.
- **I4 Token cache:** the key is `lhdn_token:{mode}:{apiHost}:{tin}:{clientId}` (mode = `intermediary` | `taxpayer`). Taxpayer tokens
  stay in memory only (no `LHDNTokens` row). Locks are per key. Saving, rotating, disabling or deleting a credential evicts that key. A 401
  evicts it and retries **once**.
- **I5 No silent fallback:** with an enabled credential, a taxpayer-auth failure records `LastError`, surfaces a clear message and
  stops that company's read sync. It **never** falls back to the intermediary token and never sets
  `LhdnIntermediaryRejected`. Disabling the credential (an audited Admin action) is the only way back to intermediary
  reads.
- **I6 Concurrency:** credential edits use `RowVersion`. A concurrent edit is rejected with "changed by someone else, reload".

**Credential provisioning: Admin UI only, audited; no manual SQL**
- `Pages/Admin/LhdnCredentials` (Admin role only, same pattern as `Pages/Admin/Webhooks`; anti-forgery; listed in
  `AuthorizationMatrixTests`). Credentials are only ever entered here, never by SQL, config files or scripts, including on Staging.
- Flow: choose the company → enter Client ID, Secret 1 (write-only, never displayed, logged or returned again), optional
  Secret 2, expiry date → **Test connection** (must pass I2 + I3 before **Enable** is possible) → status in plain language:
  "Connected, last checked 10:32", "Secret expires in 9 days", "LHDN rejected the secret: re-enter it from View and
  Register ERP".
- **Rotation:** enter a new secret → test → save. Secret 2 lets the company rotate without downtime. Expiry warnings appear on this page
  and in the existing admin / Needs Attention alerts when expiry is ≤ 14 days away.
- **Audit:** create/update/rotate/enable/disable/delete/test write `AuditService` entries (actor, company, masked TIN, ClientId last
  4, result). The secret is never part of an audit entry.

**Read path**
- `Services/TokenService.cs`: add `GetReadTokenForTIN(tin)`. An enabled, environment-matching credential → `Login as Taxpayer System`
  (no `onbehalfof`, secret 1 then secret 2 using the existing loop). No credential → the existing intermediary token. This is the
  documented default, not a fallback.
- `Services/LHDNApiService.cs`: discovery and retrieval (search used by `GetAllUuidsForSubmitterAsync`, recent, details, raw) use the
  read token and omit `onbehalfof` for taxpayer tokens (per T6). Submit, cancel, reject and the post-submit poll in
  `InvoiceSubmissionHelper` keep the intermediary token **unchanged**. Rejecting *received* invoices with the taxpayer
  token is a separate, later decision.
- `Helpers/InvoiceSyncHelper.RunFullImportFromLhdnAsync` and the status sync use the read token. Dedupe and idempotency
  stay as they are: UUID lookup, `PlanDocumentFetches`, the 40-fetch cap and continuation job, v1.50.35 direction logic, and the 1-day
  received-email window. So there are no duplicate invoices and no email flood on the first import. Discovery backfill = the existing 30-day Refresh.
- **Sync status for Finance:** the Received tab and the Admin page show the last successful import time and the mode
  ("Using company credentials" / "Intermediary — only invoices sent through EINVWORLD are visible").

**Tests**
- Unit: token request fields and headers (fake `HttpMessageHandler`); `onbehalfof` omitted for taxpayer tokens; cache-key isolation
  across mode/host/TIN/client; host mismatch refused; identity mismatch refused; no fallback on taxpayer failure; secrets and
  tokens never appear in log output.
- Integration (real SQL Server): secret encrypted at rest; unique index; `RowVersion` conflict; audit entries written without
  secrets; Admin-only access in `AuthorizationMatrixTests`.
- Staging regression: the missing preprod invoice imports under Received exactly once (no duplicate email); intermediary submit
  and status poll unchanged; a second company still on intermediary; disabling the credential reverts cleanly.

**Delivery (each PR small, CI green before merge)**
1. **PR 1, inert:** table + migration + Admin page (enter / test / enable / disable / audit). The read path is not wired yet,
   so enabling a credential changes nothing at runtime.
2. **PR 2:** wire the read path behind the per-company enable. Staging regression above.
3. Production rollout: one company at a time, after that company registers a **Production** ERP.

**Rollback:** disable or delete the credential (audited) → the next read token is intermediary again. Redeploying the previous build
ignores the table, which is additive and harmless.

### Open risks
- An individual (IG) TIN may not be allowed to register an ERP. Phase 1 step 1 decides this.
- Both client secrets expire together. Without the expiry warning, imports would stop on that date.
- Production needs every opted-in company to register its own ERP in the Production portal.
- Documents older than 31 days can't be discovered through the API (see "LHDN rules").
- Holding a taxpayer's own ERP credentials makes EINVWORLD that company's ERP of record for reads. Get the company's explicit
  consent before an Admin enters its credentials.
