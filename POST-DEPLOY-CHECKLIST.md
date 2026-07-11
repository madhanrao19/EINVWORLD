# EINVWORLD — Post-Deployment Verification Checklist

Run this on the **server** (or a staging box that mirrors it) after every deploy. CI proves the code
compiles and unit tests pass; it does **not** exercise the database, LHDN, PDF, email, OCR or Ollama at
runtime — those can only be verified against a running instance with real configuration. Work top to
bottom; stop and investigate on the first ❌.

> Legend: ✅ = expected result. Do a **read-only pass first** (don't submit real invoices to the LHDN
> production host until you've validated against PREPROD or with a disposable document).

## 0. Startup & configuration (fail-fast gates)
- [ ] App pool starts; site responds. ✅ no crash on boot.
- [ ] Logs show the one-line **startup summary** (`EINVWORLD vX.Y.Z starting — Environment=…, PDFEngine=…, AI=…, DocumentCapture=…, OCR=…, AutoMigrate=…`). ✅ flags match what you intend.
- [ ] No **config-validation** error in the log. ✅ the fail-fast validator passed (connection string, `DataProtection:KeyRingPath` set outside `App\`, LHDN BaseUrl, signing cert if `SigningEnabled`, no localhost URLs in Production).
- [ ] `GET /health` ✅ returns Healthy (DB reachable + writable folders).
- [ ] `DataProtection:KeyRingPath` folder exists, is **outside** `App\`, and the app-pool identity has Modify. ✅ existing users stay logged in across a redeploy (keys not rotated).

## 1. Database & migrations
- [ ] First boot applied pending migrations (or you ran `Apply_*.sql`). ✅ no migration error; `__EFMigrationsHistory` up to date.
- [ ] Spot-check a few tables load in the app (invoice list, users). ✅ data intact (migrations are additive — no data loss).

## 2. Authentication & authorization
- [ ] Admin login. ✅ succeeds; 2FA prompt if `Security:EnforceAdminMfa=true`.
- [ ] Supplier and Buyer logins. ✅ each sees only their permitted areas.
- [ ] Forgot-password flow sends an email and resets. ✅ (also validates SMTP — see §7).
- [ ] IDOR check: a Supplier tries to open another company's invoice by URL/id. ✅ blocked (per-TIN ownership).
- [ ] Anonymous access to public pages (home, about, contact, register). ✅ allowed; everything else redirects to login.

## 2a. UI theme (Tabler migration)
- [ ] Authenticated pages render the **Tabler** layout (dark vertical sidebar, top search + user menu),
      not the old Velzon chrome. ✅ consistent across Admin/Supplier/Buyer; brand logo is correctly sized;
      no invisible/low-contrast text; the invoice list is usable on mobile.
- [ ] Public pages (home/about/contact/resources) still use the **marketing** layout; error pages are
      standalone. ✅
- [ ] (Automated) With Turnstile **test** keys + `Security__EnforceAdminMfa=false` set temporarily, run
      `tests/playwright/10-tabler-modules.spec.js` — ✅ all module pages pass; then revert those env vars.
      (See DEPLOY-NOTES / `docs/TABLER-MIGRATION-AUDIT.md`.)

## 3. Invoice lifecycle (the core money path)
- [ ] Create a **standard invoice (01)** with ≥2 lines + tax. ✅ totals correct (line extension / tax-exclusive / tax-inclusive / payable); draft saved with a `.json` file.
- [ ] Create one of each remaining type used: **02 credit, 03 debit, 04 refund**, and **11–14 self-billed**. ✅ each maps and the `BillingReference` shape is right (01 = additional ref; 02–04 = invoice ref; 11–14 = both).
- [ ] Edit a draft. ✅ header + lines update atomically.
- [ ] Invoice list — all three tabs. ✅ sorted by **Last Updated desc**; paging works.
- [ ] Invoice details view. ✅ addresses/descriptions render safely (no broken HTML), QR present.
- [ ] Download PDF. ✅ renders via the configured engine (DinkToPdf/Puppeteer); no hang (timeout guard).

## 4. LHDN / MyInvois integration  *(use PREPROD or a disposable doc first)*
- [ ] **Taxpayer validation** (Admin/Supplier "Validate TIN"). ✅ returns a result; on a 429 it now retries with `Retry-After` instead of erroring (v1.5.2 fix).
- [ ] **Submit** a document. ✅ UUID/longId persisted; status transitions; audit row written. *(Regression guard, v1.9.7: the UI submit must PERSIST the UUID locally — before v1.9.7 the submit-guard claim bumped the row's rowversion and the save silently failed with a concurrency conflict, leaving an accepted document as a local Draft. If the invoice still shows Draft after an "accepted" submit, the fix is not deployed.)*
- [ ] **v1.9.7 one-time reconciliation** (first deploy of v1.9.7+ only). ✅ run `scripts/Reconcile-OrphanedSubmissions.sql` **per environment**: SECTION 1 lists invoices claimed-but-UUID-less; verify each at LHDN; fill in the verified UUID/SubmissionUid rows; run SECTION 2. Known staging orphans: EINV100360, EINV100361. Do **not** run it verbatim (it refuses to run with no rows filled in).
- [ ] **Failed-submission retry:** force a submission failure (e.g. temporarily wrong LHDN BaseUrl on staging). ✅ error message says a retry was queued; a `SubmitDocument` job appears in Admin → Sync Jobs and retries/dead-letters per the backoff schedule.
- [ ] **Duplicate submit** of the same payload within the dedup window. ✅ replays the prior response — no second LHDN call.
- [ ] **Manual status sync** (Admin → Invoice Sync). ✅ job queued; Sync Jobs page shows it run/complete.
- [ ] **Background sync** runs on its own cadence. ✅ statuses update; no worker crash after an app-pool recycle (orphan recovery).
- [ ] **Cancel/Reject** within the 72h window. ✅ succeeds; outside the window ✅ blocked with a clear message.
- [ ] **Cancel vs background sync (v1.8.2):** cancel an invoice while background sync is enabled, then wait one sync cycle. ✅ the invoice stays **Cancelled** (concurrency token prevents a stale sync overwriting it; sync log may show a benign "concurrency conflict … skipping" warning).
- [ ] Intermediary submit with `onbehalfof`. ✅ uses the right per-TIN token.

## 5. Bulk import & connectors
- [ ] **Bulk Import** a CSV and an XLSX (download the template first). ✅ per-row validation report against LHDN codes; valid rows create drafts.
- [ ] Watched-folder importer (only if `WatchedFolderImport:Enabled`). ✅ a file dropped in the Inbox is validated and sorted.
- [ ] REST validate API `POST /api/import/validate` with header `X-Api-Key` (only if `Api:Key` set). ✅ 200 with report; wrong/no key ✅ rejected.

## 6. AI features (only if `AI:Enabled=true`)
- [ ] **Admin → AI Settings → Test connection.** ✅ reachable + model pulled + latency (no API key shown).
- [ ] **/Assistant** — ask a question and generate an invoice suggestion. ✅ suggestion validates against real codes; nothing is submitted automatically.
- [ ] **AI Document Capture** (`/Invoices/CreateFromFile`) with a digital PDF. ✅ extracts → suggestion → review.
- [ ] Scanned PDF (only if `DocumentCapture:OcrEnabled` + tessdata + native runtimes). ✅ OCR path works; if off, ✅ reports "needs OCR".
- [ ] **AI-down safety:** stop Ollama, retry the assistant. ✅ graceful "unavailable" message; **invoice create/submit still works** (AI is optional).

## 7. Email & notifications
- [ ] Trigger a notification email (e.g. account confirm, validated invoice). ✅ delivered; links use the configured public base URL (not localhost).
- [ ] Confirm SMTP creds are supplied via **env vars** (not committed). ✅ (`appsettings.json` ships blank).

## 8. Admin & observability
- [ ] Admin → **Audit Trail** → Verify Chain. ✅ hash chain intact (tamper-evident).
- [ ] As Admin, open another company's invoice. ✅ an `InvoiceViewedCrossTenant` entry appears in the Audit Trail (same-tenant views are not audited, by design).
- [ ] Admin → **Sync Jobs**: retry / cancel a job; dead-letter (Failed) visible. ✅ actions audited.
- [ ] Admin → **System Health** and **Logs**. ✅ load; `SystemLogs` receiving structured entries with CorrelationId.
- [ ] Per-request log line appears (Serilog request logging). ✅ one tidy line per request.
- [ ] Rate limits: hammer `/Admin/InvoiceSync`. ✅ 429 after the per-user limit; global limiter otherwise generous.

## 9. Behind Cloudflare Tunnel (if applicable)
- [ ] Site reachable over the public HTTPS hostname. ✅ no redirect loop (smart HTTPS-redirect default is off behind the tunnel).
- [ ] Cookies are Secure; audit shows the **real client IP** (forwarded headers honoured), not 127.0.0.1.
- [ ] **Rocket Loader is OFF** in the Cloudflare zone (*Speed → Optimization*). With it on, every page's
      `DOMContentLoaded` stalls ~20 s and Turnstile becomes unreliable (documented incompatibility).
      Verify: page source must NOT contain `rocket-loader.min.js` / `type="…-text/javascript"` rewrites.

---

### If anything fails
Capture the **CorrelationId** from the error/log line and the exact step. Most first-deploy failures are
configuration, not code: missing env var/secret, `DataProtection:KeyRingPath` not set, SQL login lacking
DDL rights, a missing native runtime (wkhtmltox / Tesseract / PDFium), or Ollama not installed/model not
pulled. See **IIS-DEPLOYMENT-GUIDE.md** (PART O for AI) and **DEPLOY-NOTES.md** (§0 upgrade steps).
