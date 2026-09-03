# EINVWORLD — Post-Deployment Verification Checklist

Run this on the **server** (or a staging box that mirrors it) after every deploy. CI proves the code
compiles and unit tests pass; it does **not** exercise the database, LHDN, PDF, email, OCR or Ollama at
runtime — those can only be verified against a running instance with real configuration. Work top to
bottom; stop and investigate on the first ❌.

> Legend: ✅ = expected result. Do a **read-only pass first** (don't submit real invoices to the LHDN
> production host until you've validated against PREPROD or with a disposable document).

## 0. Startup & configuration (fail-fast gates)
- [ ] App pool starts; site responds. ✅ no crash on boot.
- [ ] Logs show the one-line **startup summary** (`EINVWORLD vX.Y.Z starting — Environment=…, PDFEngine=…, AI=…, DocumentCapture=…, OCR=…, SmartCapture=…, AutoMigrate=…`). ✅ flags match what you intend.
- [ ] No **config-validation** error in the log. ✅ the fail-fast validator passed (connection string, `DataProtection:KeyRingPath` set outside `App\`, LHDN BaseUrl, signing cert if `SigningEnabled`, no localhost URLs in Production).
- [ ] `GET /health` ✅ returns Healthy (DB reachable + writable folders).
- [ ] `DataProtection:KeyRingPath` folder exists, is **outside** `App\`, and the app-pool identity has Modify. ✅ existing users stay logged in across a redeploy (keys not rotated).

## 1. Database & migrations
- [ ] First boot applied pending migrations (or you ran `Apply_*.sql`). ✅ no migration error; `__EFMigrationsHistory` up to date — compare its last row against the newest filename in `Migrations\*.cs`.
- [ ] Spot-check a few tables load in the app (invoice list, users). ✅ data intact (migrations are additive — no data loss).
- [ ] **v1.11.0 one-time step:** after migrations land, go to **Admin → System Health → Encrypt PII** and run the backfill (once per environment). ✅ existing bank-account/address data is now encrypted at rest, not just the schema widened. Safe to click again if unsure — it's idempotent.
- [ ] **v1.13.0:** confirm `__EFMigrationsHistory` includes `AddRoleModulePermissions` and `AddCompanyRolePartyInfoScope` (last row). ✅ both additive — no data loss expected.
- [ ] **v1.14.0:** confirm `__EFMigrationsHistory` includes `AddNewInvoiceReceivedEmailTrackingToInvoiceHeader` (last row). ✅ additive — existing `InvoiceHeaders` rows backfill `IsNewInvoiceReceivedEmailSent = 1` (not applicable), no retroactive emails sent.
- [ ] **v1.14.1:** confirm `__EFMigrationsHistory` includes `AddRejectionCancellationEmailTrackingToInvoiceHeader` (last row) **before** the new app code goes live — the EF model expects these columns, so running the new code against an un-migrated DB will error on every `InvoiceHeaders` query. ✅ additive — existing rows backfill `IsRejectionEmailSent`/`IsCancellationEmailSent = 1` (not applicable), no retroactive emails sent.
- [ ] **v1.14.1:** confirm `EmailConfiguration:Default:GlobalBccEmail` is set to a real address (or intentionally blank) on this server. ✅ either way, Rejection/Cancellation emails to Supplier/Buyer now send regardless — a blank BCC only skips the admin copy and logs a warning, it no longer blocks the notification entirely.
- [ ] **v1.14.1:** reject a test document, then check both the Supplier and Buyer received the rejection email (and the Cancel flow, cancelling a different test document). ✅ both parties receive it; if the immediate send fails, `InvoiceHeader.IsRejectionEmailSent`/`IsCancellationEmailSent` stays `false` so the next background cycle (`InvoiceStatusUpdater.RunRejectionCancellationFinalizerAsync`) retries it — indefinitely, no age cutoff.
- [ ] **Smart Capture:** confirm `__EFMigrationsHistory` includes `AddSmartCaptureDocument`, `AddSmartCaptureCompanyHint` (Stage 2), and `AddSmartCaptureAutoSubmit` (Stage 4). ✅ all additive — new tables / a new nullable column only, no existing table altered.
- [ ] **Smart Capture ("Create from Document"):** before enabling `SmartCapture:Enabled=true`, note there is **no application-level malware scanning** (deliberate — see `IIS-DEPLOYMENT-GUIDE.md` PART 17d for the upload-security controls relied on instead). Upload a real test PDF — it should queue and process; then upload a renamed non-PDF file (e.g. `.txt` renamed to `.pdf`) — it must be **rejected** with a signature-mismatch error, proving format validation is actually enforced.
- [ ] **Smart Capture:** end-to-end — upload a supplier invoice at `/Invoices/SmartCapture`, wait for it to leave "Processing", confirm the LHDN document type and a registered buyer on the review screen, create the draft, and confirm it opens correctly in the normal `InvoiceEdit` page. ✅ draft is fully editable and submits through the unchanged MyInvois path — Smart Capture never bypasses review or submission.
- [ ] **Smart Capture Stage 1.5 (duplicate + condensed review):** re-upload the exact same file for the same company. ✅ the second upload's review screen shows a "possible duplicate" **Warning**, never blocks draft creation. Upload a clean document with no review issues. ✅ the review screen shows a condensed "all checks passed" summary with the full checklist collapsed behind a toggle, not expanded by default.
- [ ] **Smart Capture Stage 3 (bulk upload):** select several files at once in the upload picker (capped at `SmartCapture:MaxFilesPerBulkUpload`, default 20). ✅ each file gets its own row/status on the list page; include one invalid file in the batch and confirm it's reported as a per-file failure without blocking the others.
- [ ] **Smart Capture Stage 4 (conditional auto-submission) — only if enabling `SmartCapture:AutoSubmitEnabled=true`:** at **Admin → Smart Capture Auto-Submit** (`/Admin/SmartCaptureAutoSubmit`), opt in one test company with a narrow doc-type allowlist, a small value ceiling, and a short delay. Confirm a small, clean Smart Capture draft for that company. ✅ the Smart Capture list page shows an "Auto-submit HH:mm" badge with a **Cancel** button; clicking Cancel before the delay elapses stops it (job shows Cancelled in Admin → Sync Jobs); letting it run submits automatically via the normal `SubmitDocument` job. Then disable the company's opt-in **while a job is still pending** in its delay window. ✅ (v1.20.1+) that pending job is also retracted (Cancelled), not just future ones blocked.

- [ ] **v1.20.1 auto-submit cancel-race fix:** if upgrading from v1.20.0, this patch closes a narrow timing
      window where Cancel could show "Cancelled" on a job the durable worker had already completed (a real
      LHDN submission may have already gone through) — no action needed beyond confirming the version, this
      is a correctness fix with no schema/config change.
- [ ] **v1.22.0:** confirm `__EFMigrationsHistory` includes `AddLineTariffOriginAndHeaderShippingCustoms`
      (last row). ✅ additive only — new nullable columns on `InvoiceLines`/`InvoiceHeaders`, no existing
      row touched.
- [ ] **v1.22.0 Invoice Items redesign — Create Invoice:** open Create Invoice, add a line item. ✅ the
      Item/Service block shows Select Saved Item → Item Code → Description → Classification → Unit, and
      the Discount/Fee/Charge/Taxes/Additional Information pill toggles below Quantity & Pricing all
      expand/collapse and stay filled in correctly. Click **Add Item** and **Duplicate** on an existing
      row. ✅ the new row has the same structure (not the old always-visible-tax layout) — if it doesn't,
      hard-refresh the browser first (this exact symptom was cache-related, see CHANGELOG v1.22.0).
      Enter a Discount and a Fee/Charge on a taxed line, check Review & Submit. ✅ the Tax figure and Item
      Summary note line reflect the discount/fee correctly, and the submitted invoice's UBL JSON shows a
      non-zero line `AllowanceCharge` and the discount-netted `TaxableAmount`/`TaxAmount`.
- [ ] **v1.22.0 Invoice Items redesign — Invoice Edit:** open an existing multi-line invoice with tax
      data in Edit mode. ✅ item rows render with the same new structure as Create Invoice and all
      existing data (including any Discount/Fee/Tariff/Country of Origin) loads correctly; Save and
      reload confirms it round-trips.
- [ ] **v1.22.0 Additional Information sections:** on Create Invoice (or Edit), fill in the new
      **Payment & Prepayment**, **Shipping Recipient**, and **Customs / Import-Export** collapsed
      sections, save a draft, reload it. ✅ all values round-trip.
- [ ] **v1.23.0 — Shipping Recipient/Customs now reach the LHDN payload:** submit (or view the generated
      JSON for) an invoice with a Shipping Recipient filled in. ✅ `Delivery.DeliveryParty` shows the
      real name/address/TIN/ID instead of being blank. Fill in Customs Form No.1/No.2, FTA info, and
      Certified Exporter Authorization Number on another test invoice. ✅ the top-level
      `AdditionalDocumentReference` list shows entries with `DocumentType` `CustomsImportForm`/
      `FreeTradeAgreement`(with `ID: "FTA"`)/`K2`, and `AccountingSupplierParty.AdditionalAccountID`
      shows the authorization number. An invoice with **none** of these filled in should look unchanged
      from before v1.23.0 (no blank `DeliveryParty`, no empty `AdditionalDocumentReference`).
- [ ] **v1.23.1 — Incoterms placement fix:** set Incoterms (Payment & Prepayment section) on a test
      invoice and check the generated JSON. ✅ a bare entry (`ID` only, no `DocumentType`) appears in the
      top-level `AdditionalDocumentReference` list with the Incoterms code, and
      `Delivery.Shipment.ID._` is blank (`""`).
- [ ] **v1.24.0 — Item rows mobile layout (Phase 4):** open Create Invoice (or Invoice Edit) on a phone or
      a narrow (<992px) browser window. ✅ each item row shows a 2-column layout — Item/Service and
      Description full-width, Classification+Unit and Qty+Price paired 2-up, a highlighted Subtotal/Total
      footer, and larger Duplicate/Remove tap targets — not the old cramped fixed-width wrapping. Desktop
      (≥992px) is unchanged (CSS-only change, no JS/schema touched).
- [ ] **v1.25.0 — Items step Summary + Validation rail:** open Create Invoice, go to Step 2 (Items).
      ✅ a sticky right rail (Invoice Summary + Validation Checklist) is visible, matching Step 1/Step 3.
      Add a line, leave Classification/Unit/Qty/Price blank. ✅ checklist shows red/not-ready. Fill them
      in. ✅ checklist flips green live without navigating away. Remove the only item row. ✅ "Line items
      required" flips back to red. Repeat on Invoice Edit against an existing multi-line invoice. ✅ the
      checklist reflects the loaded data's real state and updates live on edit.

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
- [ ] **(v1.11.0) Admin sidebar on mobile** (<992px): the hamburger toggle opens a sliding off-canvas
      drawer with a backdrop and its own close button, not the old inline collapse. ✅ at desktop widths
      the sidebar is unchanged (fixed column, collapsible to icon-only with tooltips on hover).
- [ ] (Automated) With Turnstile **test** keys + `Security__EnforceAdminMfa=false` set temporarily, run
      `tests/playwright/10-tabler-modules.spec.js` — ✅ all module pages pass; then revert those env vars.
      (See DEPLOY-NOTES / `docs/TABLER-MIGRATION-AUDIT.md`.)
- [ ] **(v1.14.0) Dark mode toggle** (topbar sun/moon icon, authenticated Tabler pages only): click it —
      ✅ theme flips instantly, persists across a page reload (cookie), and legibility holds across
      cards/tables/badges/sidebar/pagination. First visit in a fresh/incognito session with the OS set to
      dark ✅ loads dark by default, no flash of the wrong theme.
- [ ] **(v1.14.0) Skip-navigation link:** press Tab once on page load (before clicking anything). ✅ a
      "Skip to main content" link becomes visible as the first focusable element; activating it moves
      focus past the sidebar/topbar into the page content.

## 2b. Company Management workspace (v1.11.0)
- [ ] **My Company** shows the tabbed workspace (Overview/Profile/Users/Roles & Permissions/Invoice Branding/Security/Audit). ✅ all tabs load without error.
- [ ] Invite a new user by email (Users tab). ✅ invitation email sends (reuses existing SMTP), accept link works, invitee sets their **own** password (no admin-set-password path exists anymore).
- [ ] Assign a company role (Owner/Admin/Editor/Viewer) to a member (Roles & Permissions tab). ✅ their effective permissions change accordingly; a member with no role assigned still works via the legacy `HasCompanyAccess`/`IsViewOnly` fallback.
- [ ] Set an invoice accent color / footer note / bank-details visibility (Invoice Branding tab). ✅ saves; note this is **not yet wired into PDF rendering** in this release (settings-only).
- [ ] Audit tab loads recent `AuditLog` entries filtered to the company's TIN. ✅ no cross-tenant rows visible.

## 2c. Buyer Management & Items (v1.11.0)
- [ ] Buyer List/Create/Edit/Details/Import render the new Tabler layout. ✅ KPI cards, search/status filter, sortable table.
- [ ] Duplicate Review page loads (read-only — no merge/delete actions in this phase). ✅
- [ ] Deleting a buyer shared with another supplier **unlinks** rather than hard-deletes it. ✅ the other supplier still sees the record.
- [ ] Create/Edit an Item with a **Unit** and **Unit Price**. ✅ Unit validates against active LHDN unit codes; Unit Price stores 4 decimal places (`decimal(18,4)`) — check a fractional price like `12.3456` round-trips exactly, not rounded to 2dp.
- [ ] Select a saved item on **Create Invoice**. ✅ the line's unit and price auto-fill from the item.

## 2d. Role Management & company user administration (v1.13.0)
- [ ] **Admin → User Management → Role Management** loads. ✅ shows the Identity role list (with `Admin`/`Supplier`/`Buyer` marked "Core") and the Module Access grid.
- [ ] Create a new role, then delete it. ✅ appears in Manage Users' "Change Role" dropdown immediately; delete succeeds since unassigned.
- [ ] Try to delete `Admin`, `Supplier`, or `Buyer`, or a role currently assigned to a user. ✅ blocked with a clear message.
- [ ] Restrict a module for the Supplier role (uncheck it, Save), then log in as a Supplier and visit that module. ✅ redirected to Access Denied; re-check the box and access is restored. ✅ Admin is never affected by any restriction.
- [ ] **Company Management → Users**: as a Supplier Owner/Admin, remove a team member. ✅ succeeds; trying to remove yourself or the last Owner ✅ blocked with a clear message.
- [ ] **Company Management → Roles & Permissions**: create a custom role scoped to your company (name + permission checkboxes), assign it to a member, then delete it. ✅ the role and its "Custom" badge only appear for your own company; assigned members fall back to "no role" after deletion.

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
- [ ] **v1.25.1 — background retry actually succeeds:** let a queued `SubmitDocument` retry run (Admin → Sync Jobs). ✅ its `Message` never shows `TIN not found in session` — that was `InvoiceSubmissionHelper` dropping the TIN before calling `SubmitDocumentsAsync`, breaking every background-initiated submission (retries and Smart Capture Stage 4 auto-submit alike). The retry should now authenticate and either succeed or fail with a real LHDN-side error, never this one.
- [ ] **v1.25.2 — Shipping Recipient Postcode/State validation:** on Create Invoice (or Edit), open the
      invoice-level Additional Information → Shipping Recipient section. ✅ Postcode is capped at 5
      characters (`maxlength`); State is a dropdown of 2-character LHDN state codes, not a free-text
      box. Submit an invoice with both filled in. ✅ no `CF405`/`CF416` validation error from LHDN.
- [ ] **Duplicate submit** of the same payload within the dedup window. ✅ replays the prior response — no second LHDN call.
- [ ] **Manual status sync** (Admin → Invoice Sync). ✅ job queued; Sync Jobs page shows it run/complete.
- [ ] **Background sync** runs on its own cadence. ✅ statuses update; no worker crash after an app-pool recycle (orphan recovery).
- [ ] **Cancel/Reject** within the 72h window. ✅ succeeds; outside the window ✅ blocked with a clear message.
- [ ] **Cancel vs background sync (v1.8.2):** cancel an invoice while background sync is enabled, then wait one sync cycle. ✅ the invoice stays **Cancelled** (concurrency token prevents a stale sync overwriting it; sync log may show a benign "concurrency conflict … skipping" warning).
- [ ] Intermediary submit with `onbehalfof`. ✅ uses the right per-TIN token.
- [ ] **(v1.13.0) Unit-code validation:** try to create/import an invoice line with an invalid/blank unit code (e.g. via CSV import, bypassing the Create Invoice dropdown). ✅ submission is rejected with a clear "invalid or missing unit of measure" error, not silently accepted.
- [ ] **(v1.13.0) Signed SVDP, only if `SigningEnabled=true`:** submit an SVDP-flagged invoice. ✅ document declares version `1.3` (not `1.2`) and carries a valid XAdES signature; with `SigningEnabled=false`, SVDP invoices still submit as unsigned `1.2` as before.

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
- [ ] **(v1.14.0) New-e-invoice-received email:** trigger an LHDN sync for a company that has a genuinely
      new buyer-side invoice from an external ERP (or use "Refresh from API" on the Received tab shortly
      after one lands). ✅ the buyer gets a "New e-Invoice Received" email (not the "Validated" one).
      Temporarily stop the SMTP relay and repeat — ✅ the send fails, is logged, and
      `InvoiceHeader.IsNewInvoiceReceivedEmailSent` stays `false` so the next background cycle (every
      `InvoiceStatusUpdaterSettings:PollingIntervalSeconds`) retries it once SMTP is back, with no manual
      resend needed.

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
- [x] **Web Analytics / Browser Insights is OFF** in the Cloudflare zone (*Analytics & Logs → Web Analytics*,
      or wherever the zone exposes the auto-injected beacon toggle). Cloudflare auto-injects a `defer`
      `<script src="https://static.cloudflareinsights.com/beacon.min.js/...">` into every response; on
      networks where that host is slow/unreachable this stalls `DOMContentLoaded` 20-35 s — same failure
      class as Rocket Loader, confirmed via HAR on `/` and `/login` (2026-08-01). The app already has its
      own GTM-based analytics, so this beacon is redundant. Not fixable from app code — it is not referenced
      anywhere in our HTML/JS; Cloudflare injects it at the edge. Verify: page source must NOT contain
      `static.cloudflareinsights.com/beacon.min.js`.
      **Done and re-verified 2026-08-01: RUM disabled for `einvworld.com`; HAR confirms `DOMContentLoaded`
      down from 21-34 s to 0.9-2.5 s.**

---

### If anything fails
Capture the **CorrelationId** from the error/log line and the exact step. Most first-deploy failures are
configuration, not code: missing env var/secret, `DataProtection:KeyRingPath` not set, SQL login lacking
DDL rights, a missing native runtime (wkhtmltox / Tesseract / PDFium), or Ollama not installed/model not
pulled. See **IIS-DEPLOYMENT-GUIDE.md** (PART O for AI) and **DEPLOY-NOTES.md** (§0 upgrade steps).
