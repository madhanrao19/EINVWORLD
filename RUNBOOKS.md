# EINVWORLD — Operator Runbooks

Short, step-by-step procedures for the three scenarios an on-prem operator is most likely to hit. Each
one describes a mechanism that already exists in the app — this document doesn't add new behaviour, it
writes up how to *use* what's there under pressure.

---

## Runbook 1 — LHDN signing certificate rotation

**When:** the certificate is nearing/at expiry (you'll get a **CertExpiryAlertService** email if
`CertExpiryAlerts:Enabled=true`, or see it on **Admin → System Health** either way), or you're switching
to a newly issued cert for any other reason.

**Impact if you do nothing:** once the cert expires, every **signed** submission (`SigningEnabled=true`)
will fail. Unsigned (v1.0) submission is unaffected — this only applies once signing is turned on.

**Steps:**
1. Obtain the new `.p12` certificate + password from your Malaysian CA (LHDNM/MCMC-recognised).
2. Copy it to the server's cert folder (see IIS-DEPLOYMENT-GUIDE.md Part 11), e.g.
   `E:\EINVWORLD\Cert\<new-file>.p12`. **Do not delete the old file until the new one is confirmed
   working** — keep both side by side during the cutover.
3. Update the environment variables:
   - `LHDNApiConfig__CertPath` → the new file's relative path.
   - `LHDNApiConfig__CertPass` → the new password.
4. `iisreset` (or recycle the app pool) so the new values load.
5. Verify: **Admin → System Health** should show the new cert's expiry date and "valid". Submit one test
   document against **LHDN PREPROD** first if at all possible before trusting it in Production.
6. Once confirmed, remove the old `.p12` file and secret value.

**Related:** the LHDN *client secret* (OAuth, not the signing cert) rotates independently and supports
zero-downtime rotation via the dual `ClientSecret`/`ClientSecret2` slots — see `SECRETS-SETUP.md`
"Rotation & hygiene".

> **Custody note:** these steps describe the default `File` provider
> (`LHDNApiConfig:SigningKeyProvider = "File"`). If the key is ever moved to a vault/HSM provider (see
> SECRETS-SETUP.md "Signing-key custody"), rotation happens in the vault instead — steps 1–3 above are
> replaced by renewing the certificate there; the recycle in step 4 still applies (the provider caches
> the certificate for the process lifetime).

---

## Runbook 2 — MyInvois (LHDN) is down or heavily rate-limiting

**When:** submissions are failing with 429s, timeouts, or 5xx from LHDN; LHDN has announced maintenance;
or the Admin → Sync Jobs page shows a growing Failed backlog.

**What already protects you (no action needed):**
- `LhdnRateLimitHandler` paces outbound calls below LHDN's published per-endpoint RPM limits, so normal
  traffic shouldn't trigger 429s in the first place.
- `SendWithRetryAsync` (submission calls) honours LHDN's `Retry-After` and retries with growing,
  jittered backoff before giving up.
- An interactive submission that still fails **automatically queues a `SubmitDocument` background retry**
  (the user is told so in the error message) — it re-attempts on the durable queue's backoff schedule and
  no-ops safely if the invoice got submitted in the meantime, so a LHDN blip usually heals itself with no
  operator action at all.
- `TokenService` caches the OAuth token for its full lifetime with a DB-backed fallback, so a LHDN blip
  doesn't force a token re-fetch storm.
- The durable background job queue (`DurableSyncJobWorker`) retries failed sync/import jobs on an
  exponential backoff automatically, surviving app-pool recycles.

**Steps if the backlog is still growing:**
1. Check **Admin → System Health** for the current Failed-job count and queue depth.
2. Check **Admin → Sync Jobs → filter Failed** for the specific error messages — distinguish a genuine
   LHDN outage (timeouts/5xx across many TINs) from a single bad document (validation error on one
   invoice, which will keep failing regardless of LHDN's health).
3. If it's a genuine LHDN-side outage: **do nothing but wait** — the durable queue will keep retrying on
   its own backoff schedule. Do not manually resubmit affected invoices; that risks a duplicate
   submission once LHDN recovers and your retry lands at the same time as the queue's own retry.
4. Once LHDN recovers, use **Admin → Sync Jobs → Retry All Failed** to immediately re-attempt the backlog
   rather than waiting for each job's individual backoff timer.
5. If `SyncFailureAlerts:Enabled=true`, you'll also get an email once the Failed count crosses the
   configured threshold — configure a recipient who can act on it (see `appsettings.json`).

---

## Runbook 3 — Failed-job / dead-letter replay

**When:** a document is stuck in the Failed state on **Admin → Sync Jobs** for a reason that's now fixed
(e.g. a transient LHDN error, a since-corrected TIN, a since-renewed token). This covers all job types —
StatusSync / FullImport / SupplierRefresh **and `SubmitDocument`** (a failed interactive LHDN submission
whose automatic retries were exhausted; retrying it re-runs the submission itself, and it no-ops safely
if the invoice was already submitted another way).

**Steps:**
1. Go to **Admin → Sync Jobs**, filter by `Failed`.
2. Read the job's `Message` column to confirm the underlying cause is actually resolved before retrying —
   retrying a job that will fail for the same reason just adds noise.
3. **Single job:** click **Retry** on that row — it re-queues with a fresh attempt count.
4. **Bulk:** click **Retry All Failed** to re-queue every failed job at once (useful after an outage —
   see Runbook 2).
5. **Cancel** instead of retry for a job that's genuinely obsolete (e.g. superseded by a manual fix) —
   only available while a job is still `Queued`.
6. All retry/cancel/bulk-retry actions are written to the tamper-evident audit trail
   (**Admin → Audit Trail**) — `SyncJobRetried`, `SyncJobCancelled`, `SyncJobsBulkRetried` — so there's a
   record of who replayed what and when.

---

## Runbook 4 — Encrypt existing PII (one-time backfill)

**When:** immediately after upgrading to v1.7.2 (or later) for the first time, to encrypt bank account
numbers and secondary/tertiary address lines that were stored as plaintext before this release. New and
edited records are encrypted automatically — this is only for pre-existing rows.

**What's protected already (no action needed):** from v1.7.2 on, `BankAccountNo` and `Addr2`/`Addr3` on
suppliers/buyers/invoices/templates are encrypted at rest transparently using the DataProtection key-ring.
Reads are lenient, so the app keeps working with a mix of encrypted and not-yet-encrypted rows — there is
no rush and no downtime.

**Before you start:**
1. **Take a full database backup.** (Additive-only, but back up anyway per policy.)
2. Confirm the DataProtection key-ring folder (`DataProtection:KeyRingPath`, e.g. `E:\EINVWORLD\Keys`) is
   backed up. **Losing the key-ring makes the encrypted columns permanently unreadable** — see
   SECRETS-SETUP.md "Field-level PII encryption".

**Steps:**
1. Go to **Admin → System Health**.
2. In the **🔐 PII encryption (maintenance)** card, read the warning, then click **"Encrypt existing PII"**
   and confirm.
3. The page reports how many values were scanned and encrypted. The action is **idempotent** — rows that
   are already encrypted are skipped — so if it's interrupted (app-pool recycle, timeout on a very large
   database) just **run it again**; it resumes where it left off and a fully-encrypted database is a no-op.
4. Verify: open a supplier/buyer or invoice with a bank account number in the UI — it displays normally
   (decrypted transparently). The run is recorded in **Admin → Audit Trail** as `PiiEncryptionBackfill`.

**Note:** TIN is intentionally left in plaintext (it's filtered on throughout the app and is a semi-public
tax identifier); so are `Addr1`/city/state/postal (used for reporting and PDF rendering). This is by
design — see CHANGELOG v1.7.2.
