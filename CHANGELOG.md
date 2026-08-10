# 🧾 EINVWORLD Developer Change Log

> **Current version: `v1.21.4`** (`AppInfo:Version` in `appsettings.json`). v1.21.4 is a **patch**
> release: three more fixes from the same full production-verification QA pass on staging.einvworld.com
> that produced v1.21.3 — the demo Buyer account was linked to an LHDN generic placeholder TIN that can
> never complete an intermediary OAuth token exchange (now points at a real onboarded company); the
> Supplier/Buyer dashboard's "LHDN Invalid"/"Connection Failed" action tiles always rendered with
> alarming static text even with zero actual issues (now hidden when there's nothing to act on, plus a
> missing filter dropdown option); and demo accounts got "Access Denied" viewing their own company's
> "My Company" page because `RoleSeeder` never granted the legacy `HasCompanyAccess`/`IsPrimaryCompany`
> flags that gate it (a separate mechanism from the newer "Roles & Permissions" system). All four fixes
> from this QA pass (this one plus the three that shipped as v1.21.3) have been live-verified against
> Staging post-redeploy: Admin, Supplier, and Buyer all log in and reach a fully working dashboard, "My
> Company" loads correctly, and the dashboard action tiles no longer show false alarms. See the dated
> entries below for details. v1.21.3 was a **patch**
> release, found during the same QA pass: fixes a
> false-positive "Row 1: contents were altered after it was written" from Admin → Audit Trail →
> "Verify chain integrity" (a `DateTimeKind` round-trip bug in the hash recomputation, not a real tamper
> event — proven with a real-SQL-Server integration test that reproduces the exact error on a
> brand-new database and passes once fixed). See the dated entry below for details. v1.21.2 was a
> **patch**
> release: `CreateInvoice.cshtml`/`InvoiceEdit.cshtml` were rendering `Quantity`/`UnitPrice`/
> `TaxPercentage`/`ExchangeRate` inputs at their raw `decimal(18,6)`/`decimal(18,4)` database precision
> (e.g. `1.000000`, `1500.0000`) instead of the 2 decimal places every other amount on the form already
> used — fixed by formatting the rendered `value` for display only (storage precision unchanged). Also
> fixes a real bug found in the same pass: the Step 1 sticky "Invoice Summary" sidebar card on both forms
> was never wired up to the totals calculation, so it stayed frozen at `RM 0.00` regardless of the
> invoice's actual items/totals, even while the "Running Total" KPI right next to it updated correctly.
> See the dated entry below for details. v1.21.1 was a **patch**
> release: adds `TessdataSyncWorker`, a background job that automatically keeps AI Document Capture/Smart
> Capture's OCR language files (`eng.traineddata`, `msa.traineddata`, ...) current from the official
> `tesseract-ocr/tessdata` GitHub repo, replacing the previous fully-manual "download and copy the file in
> yourself" process (still documented as the fallback for air-gapped servers). Only runs at all when
> `DocumentCapture:OcrEnabled=true` (off by default) — independently switchable off via
> `TessdataSync:Enabled=false` for servers with restricted/no outbound internet access. See the dated
> entry below for details. v1.21.0 was a **minor**
> release: brings `Pages/Invoices/InvoiceEdit.cshtml` (the page every Smart Capture confirmation and
> every "Edit" click hands off to) to full visual and functional parity with `CreateInvoice.cshtml` —
> the two had quietly drifted apart over several UI-migration commits, and a side-by-side comparison
> against live staging screenshots confirmed InvoiceEdit was still showing an older, plainer layout
> despite being restyled in the same commits. All 3 steps now match: Step 1 gets the KPI stat row
> (Buyer Status/Currency/Running Total/Document Type), collapsible Document Setup/Buyer & Supplier/
> Additional Party Information sections, and a sticky Invoice Summary + live Validation Checklist
> sidebar; Step 2 gets a Row Total column per line item and an Invoice Summary/Amount Payable block;
> Step 3 gets Invoice Details/Buyer & Supplier cards, an Item Summary table, a Submission Readiness
> checklist, and a sticky Total Amount sidebar — while keeping InvoiceEdit's own Save Draft/Save as
> Template/Submit to LHDN button set and handlers completely unchanged (only CreateInvoice's Review
> step's markup/layout was reused, never its buttons). Found and fixed one real pre-existing bug along
> the way: `calculateTotals()` never updated a line item's own "Subtotal" display for an already-saved
> draft — only a live `oninput` handler did — so a freshly-opened existing invoice showed "0.00"
> everywhere that value fed into (the Step 2 Subtotal box and, worse, the new Step 3 Item Summary
> table's Tax column, computed as Row Total − Subtotal). Now `calculateTotals()` keeps that value in
> sync directly, like it already did for Row Total. No schema change; pure Razor markup + JavaScript.
> v1.20.2 was a **patch**:
> fixes a **Critical** IDOR/tenant-isolation gap on `Pages/Invoices/InvoiceEdit.cshtml` — the page every
> Smart Capture confirmation hands its new draft off to had no ownership check at all on loading (GET) or
> saving (POST) an invoice by id; `SupplierBasePage`'s own authorization never actually engaged for this
> page (it parses `id` as a query-string int `PartyInfoId`, but this page's `id` is a route-value
> `InvoiceNo` string, so the parse always silently fails and falls back to checking the current user's
> *own* company instead of the invoice actually requested). Fixed by reusing the same
> `CanAccessInvoiceAsync` helper already guarding this page's own Submit handler and every other
> invoice-by-id endpoint in the app. Also removed **AI Document Capture** from the sidebar/admin nav menus
> — fully superseded by Smart Capture ("Create from Document") across all 5 shipped stages; the route
> stays alive as a documented rollback path (`CLAUDE.md`), only the nav links were removed. No schema
> change. See the dated entry below for details. v1.20.1 was a **patch**:
> fixes a race condition found during a full Smart Capture re-verification pass —
> `SmartCaptureAutoSubmitEligibilityService.CancelAsync` could, under a real timing race with the durable
> worker, blindly overwrite an already-`Completed` `SyncJob` row back to `Cancelled`, showing "Cancelled"
> in the UI even though the invoice had already been submitted to MyInvois. Fixed with a single atomic
> conditional `UPDATE ... WHERE Status = Queued` (via `ExecuteUpdateAsync`) instead of a load-check-save
> round trip. Also fixes disabling a company's auto-submit opt-in
> (`/Admin/SmartCaptureAutoSubmit`) not retracting jobs already scheduled during their delay window — it
> now cancels them too (`CancelAllPendingForCompanyAsync`). No schema change. See the dated entry below
> for the full re-verification findings. v1.20.0 was a **minor** release: **Smart Capture Stage 4
> (reduced first cut)** — conditional automatic LHDN submission. A
> system Admin can opt a company into unattended submission of Smart Capture drafts
> (`/Admin/SmartCaptureAutoSubmit`, never self-service); a global kill switch
> (`SmartCapture:AutoSubmitEnabled`, default **false everywhere**) must also be on. Every confirmed draft
> is still re-evaluated against deterministic gates (doc-type allowlist, zero review warnings/errors,
> exact buyer match, a per-company value ceiling) — never a fuzzy confidence score — before a delayed,
> cancellable `SubmitDocument` job is enqueued through the existing, unchanged submission pipeline
> (idempotency/signing/retry/audit untouched). New additive migration (`SmartCaptureAutoSubmitSettings`
> table + `SmartCaptureDocuments.PendingAutoSubmitJobId`). v1.19.0 shipped Stage 3 (reduced first cut):
> bulk upload. The upload form accepts multiple files at once (capped at
> `SmartCapture:MaxFilesPerBulkUpload`, default 20); each file goes through the exact same per-file
> validation/quota/storage/durable-job path as a single upload
> (`SmartCaptureDocumentService.UploadAsync`, unchanged) — one bad file in a batch never blocks the
> others. v1.18.0 shipped Stage 2 (reduced first cut): per-company "learned hints" fed into the AI
> suggestion prompt as advisory-only context, new additive `ApplicationDbContext` migration
> (`SmartCaptureCompanyHints`). v1.17.3 (folded into v1.18.0) documented Stage 1.5 (2026-08-08, PR #173):
> exact-content duplicate-upload detection (flag, never block) and a condensed review view for
> extractions with zero warnings. v1.17.2 removed application-level malware scanning (ClamAV) from Smart
> Capture — a deliberate architecture decision; see that entry for the reasoning and the upload-security
> controls it relies on instead. v1.17.1 fixed a **Critical** bug where every Smart Capture "Confirm"
> attempt failed (`InvoiceDraftService.SaveDraft` never set the NOT-NULL `PrefixedID` column). v1.17.0
> was the **minor** release that introduced **Smart Capture (Stage 1)** — persisted, async
> supplier-invoice capture → draft, now labelled **"Create from Document"** in navigation — via PR #170,
> with a new additive `ApplicationDbContext` migration (`SmartCaptureDocuments`). Feature-flagged **OFF
> by default** in Development and Production; enabled on Staging only, for verification (real Ollama
> sign-off still outstanding — see
> `POST-DEPLOY-CHECKLIST.md`).

## 📅 2026-08-10 — Fix demo Supplier/Buyer "My Company" Access Denied

Continuing the same QA pass, re-verified live on Staging after the TIN-missing fix's redeploy:
`supplier@einvworld.com` logged in cleanly, but **Company Management → My Company
(`/Suppliers/Details?id=...`) returned "Access denied"** for the account viewing its own company.

`SupplierBasePage.OnPageHandlerExecutionAsync` (the gate for `/Suppliers/Details`, `/Edit`, `/Users`,
`/RolesPermissions`, `/Security`, `/Audit`) only grants access when the caller's `UserCompanies` row has
`HasCompanyAccess=true` or `IsPrimaryCompany=true`. `RoleSeeder`'s `UserCompany` inserts only ever set
`UserId`/`PartyInfoId`, leaving both flags at their default `false` — every demo account's company link
has always failed this check. A real, properly onboarded user gets these flags set correctly via the
app's own "Invite a teammate" flow. Note this is a genuinely separate mechanism from the newer
"Roles & Permissions" tab (`UserCompany.CompanyRoleId` → `CompanyRoles`) — assigning a role there only
ever writes `CompanyRoleId`, never `HasCompanyAccess`/`IsPrimaryCompany`, so it would not have fixed this
on its own either.

`RoleSeeder` now sets `IsPrimaryCompany=true` on the first company in each demo account's list and
`HasCompanyAccess=true` on all of them. The stale pre-fix `UserCompanies` rows were removed directly via
Admin → Company Management → Users on the live Staging database so the self-heal logic recreated them
correctly on the next restart — confirmed post-redeploy: "My Company" now loads the full Company Profile
with all tabs.

dotnet build: 0 errors. dotnet test: 242/242 pass, no regressions.

## 📅 2026-08-10 — Fix misleading dashboard action tiles + missing filter option

Continuing the same QA pass, found as Supplier: the dashboard's "LHDN Invalid" and "Connection Failed"
Action Center tiles always rendered with alarming static text ("Fix & Resubmit", "Server down. Retry
later.") even when there was nothing to act on — only the small badge count next to each tile was
conditional on `Model.ActionInvalidCount`/`ActionTransmissionErrorCount > 0`, not the tile itself.

Wrapped each tile in the same `> 0` condition already used for its badge — unchanged when there IS
something to act on, hidden entirely when there isn't. Also added the missing `TransmissionError`
`<option>` to the Internal Status filter dropdown on `Invoices/InvoiceLists` — the backend already
filtered on this `InternalStatusId` correctly, and the dashboard's "Connection Failed" tile already
deep-linked to `?InternalStatus=TransmissionError`, but the dropdown never listed it as a selectable
option. Live-verified via local dev (shares the Staging database) and again post-redeploy: both tiles
are correctly absent for accounts with zero qualifying invoices.

dotnet build: 0 errors. dotnet test: 242/242 pass (pure Razor markup change).

## 📅 2026-08-10 — Fix demo Buyer login: point at a real company, not a placeholder TIN

Continuing the same QA pass, re-verified live on Staging after the TIN-missing fix's redeploy:
`supplier@einvworld.com` now logged in cleanly, but `buyer@einvworld.com` progressed to a new, different
failure: `Login succeeded but failed to retrieve system token: LHDN rejected intermediary token for
EI00000000020. Reason: {"error":"unauthorized_client"}`.

Traced via Admin → Companies: `PartyInfoId 3` (`EI00000000020`) is **"Foreign Buyer / Shipping
Recipient"** — one of LHDN's official generic placeholder TINs for buyers without a real Malaysian TIN,
not a real onboarded company with its own MyInvois intermediary authorization. It was never going to
complete an OAuth token exchange — expected LHDN behavior for a synthetic TIN, not a code bug. The
hardcoded company id was just always the wrong choice for a login-able demo persona.

Operator confirmed the replacement: `PartyInfoId 12`, **"Datamation (M) Sdn. Bhd."** (TIN
`C2899917070`) — a real onboarded company already used successfully by `supplier@einvworld.com`. Also
removed the erroneous `UserCompanies` row (`buyer@einvworld.com` → `PartyInfoId 3`) directly from the
live Staging database. Confirmed post-redeploy: Buyer logs in and reaches a fully working dashboard with
real data (7 invoices received, RM 3,132 valid/payable, spend trends chart).

dotnet build: 0 errors. dotnet test: 242/242 pass, no regressions.

## 📅 2026-08-10 — Fix demo Supplier/Buyer login stuck on "company TIN is missing"

Found during a full production-verification QA pass on staging.einvworld.com using the credentials the
operator provided: both `supplier@einvworld.com` and `buyer@einvworld.com` were completely unable to
log in, rejected with "Your company TIN is missing. Please contact support." This exact symptom was
already flagged in this changelog on 2026-08-01 as a "pre-existing seeded-data issue" and was never
actually fixed.

Root cause: `RoleSeeder.SeedUserAsync`'s company-link step (`UserCompanies` → `PartyInfo`, which is
exactly what the login page's TIN check reads) only ever ran at the moment a demo user is first
created. On a freshly-created database the required `PartyInfo` rows didn't exist yet, so the link was
correctly skipped with a warning at seed time — but because the demo users already existed as of that
point, the link was never retried on any later startup, even once those `PartyInfo` rows landed in a
subsequent deploy. Permanently company-less demo accounts.

Fixed by decoupling "create the user" from "ensure the company link exists" — the latter now runs on
every startup (still gated behind `Seeding:SeedDefaultUsers`, off in Production) and only adds a link
that's actually missing, self-healing once the target `PartyInfo` exists. No existing `UserCompanies`
rows are touched or removed. Once deployed and the app restarts, `supplier@einvworld.com`/
`buyer@einvworld.com` should self-heal without any manual DB fix.

dotnet build: 0 errors. dotnet test: 242/242 pass, no regressions.

## 📅 2026-08-10 — Fix false-positive tamper detection in audit chain verification

Found during a full production-verification QA pass on staging.einvworld.com: **Admin → Audit Trail →
"Verify chain integrity" reports "Row 1: contents were altered after it was written"** on a database
with no actual tampering.

`AuditService.ComputeRowHash` hashes `CreatedAtUtc.ToString("O")`. At write time `CreatedAtUtc =
DateTime.UtcNow` has `Kind=Utc`, so `ToString("O")` ends in `Z`. SQL Server's `datetime2` has no
`DateTimeKind` concept, so when `VerifyChainAsync` streams rows back via EF Core, `CreatedAtUtc` comes
back with `Kind=Unspecified` — `ToString("O")` then omits the `Z`, producing a different string and
therefore a different SHA-256 hash for every single row. `VerifyChainAsync` returns on the first
mismatch, so this always surfaced as "row 1 altered" regardless of whether every row was equally
affected (it was).

Fixed by normalizing `CreatedAtUtc` to `Kind=Utc` via `DateTime.SpecifyKind` (a reinterpretation, not a
conversion — the stored value already represents UTC) before hashing. No-op at write time; makes the
verify-time recomputation match. Existing stored `RowHash` values are unaffected — no data migration
needed. Proven with a real-SQL-Server integration test
(`EINVWORLD.Tests/Integration/AuditServiceTests.cs`): reverting the fix reproduces the exact Staging
error on a brand-new, never-touched database; restoring it passes — confirming this was always a false
positive, never a real tamper event.

dotnet build: 0 errors. dotnet test: 242/242 pass, including real SQL Server LocalDB integration tests.

## 📅 2026-08-10 — Create/Edit Invoice: 2dp number display + Step 1 Invoice Summary fix

Reported with staging screenshots: on `InvoiceEdit.cshtml` (and, on inspection, identically on
`CreateInvoice.cshtml`), the Quantity, Unit Price, and Tax Percentage inputs — and the header Exchange
Rate field — rendered whatever raw precision their `decimal(18,6)`/`decimal(18,4)` database column
happened to carry (`1.000000`, `1500.0000`) instead of the 2 decimal places every other amount on the same
form already showed. The Review & Submit step's Item Summary table had the identical gap for its QTY
column (Unit Price there was already correctly formatted).

- **Fix (display only):** all 4 inputs now render via a small `Fmt2(decimal?)` Razor helper
  (`value.ToString("F2", CultureInfo.InvariantCulture)`) passed as an explicit `value="…"` attribute
  alongside `asp-for` — ASP.NET Core's `InputTagHelper` never overwrites an explicitly-set `value`, so this
  only changes what's rendered, not the model binding path, the POST payload shape, or the underlying
  `decimal(18,6)`/`decimal(18,4)` column precision (per the "correct decimal precision" architecture note
  in `CLAUDE.md` — nothing there regressed). Matches the `step="0.01"` these inputs already declared.
  Applied to `CreateInvoice.cshtml` too since it reaches the same raw-precision values via its
  `templateId`/`cloneId`/`invoiceNo` prefill paths (a blank new invoice was never affected — its fields
  start empty).
- The Review & Submit Item Summary table's QTY cell now goes through `parseFloat(qty).toFixed(2)`,
  matching the pattern its own Unit Price cell already used.
- **Real defect found and fixed in the same pass:** the Step 1 sticky "Invoice Summary" card
  (`step1SummarySubtotal`/`step1SummaryTax`/`step1SummaryTotal`) on both forms was declared in the markup
  but never referenced anywhere in `calculateTotals()` — it stayed frozen at its initial `RM 0.00` no
  matter how many items or how large the actual total was, which is exactly the "RM 42.98 running total
  vs. `RM 0.00` Invoice Summary" mismatch shown in the reported screenshots. `calculateTotals()` now
  updates all three elements alongside the KPI tile and the Step 2 card it already kept in sync.

Verified live against a real Staging draft invoice via local dev (Admin login, `EINV00017`): Quantity/Unit
Price both display `1.00` instead of `1.000000`/`1.0000` on Step 2, the Item Summary table shows `1.00` on
Step 3, the Step 1 Invoice Summary card tracks the real total instead of staying at `RM 0.00`, and
resaving via "Update Draft" round-trips correctly (`RM 1.08` unchanged on reload) with no console errors.
`dotnet build`: 0 errors. `dotnet test`: 241/241 pass (no test changes needed — pure Razor/JS display
fix, no calculation logic touched). No schema, config, or LHDN-submission changes.

## 📅 2026-08-09 — Automatic Tesseract OCR trained-data sync

Replaces the fully-manual "download `eng.traineddata`/`msa.traineddata` from GitHub and copy them into
the `tessdata` folder yourself" step (`IIS-DEPLOYMENT-GUIDE.md` PART 17a-OCR) with a new background
worker, `Services/Background/TessdataSyncWorker.cs`, mirroring the existing `CodeTableSyncWorker` pattern
(config-gated `BackgroundService`, named `HttpClient`, startup delay + interval loop, per-item try/catch
isolation).

- **Source:** the official, FOSS (Apache-2.0) `tesseract-ocr/tessdata` GitHub repository, fetched via
  `raw.githubusercontent.com` — the same repo the user asked to track (`.../blob/main/eng.traineddata`,
  `.../blob/main/msa.traineddata`). Previously the deployment guide pointed at `tessdata_fast` (a
  smaller/lower-accuracy variant); this switches the documented/automated source to the standard,
  best-accuracy repo.
- **Which languages:** derived from the existing `DocumentCapture:OcrLanguage` setting (split on `+`, e.g.
  `eng+msa`) rather than hardcoded — a deployment that only enables `eng` never fetches `msa`, and vice
  versa.
- **Change detection:** a cheap `HEAD` request compares upstream `Content-Length` to the local file's size;
  a file already up to date is never re-downloaded — a normal app-pool recycle costs one HEAD request per
  language, not a repeat multi-MB download.
- **Corruption safety:** downloads write to a `.downloading` temp file, then atomically `File.Move(...,
  overwrite: true)` into place — a cancelled/interrupted download can never leave a partial file for a
  concurrent OCR call to load. A plausibility floor (< 100 KB is almost certainly an HTML error page, not
  real trained data) discards an implausible response instead of applying it.
- **Fail-safe/toggleable:** entirely inert unless `DocumentCapture:OcrEnabled=true` (off by default) *and*
  `TessdataSync:Enabled` (new section, defaults to `true`, independently switchable off) — a server with
  restricted/no outbound internet access sets `TessdataSync__Enabled=false` and keeps staging files
  manually, exactly as before. One language's failure (network, 404, disk) is logged and skipped; it never
  blocks another language or crashes the app.
- New config section `TessdataSync` (`appsettings.json`): `Enabled` (default `true`), `BaseUrl` (default
  `https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/`), `IntervalHours` (default `24`),
  `StartupDelayMinutes` (default `2`).
- `IIS-DEPLOYMENT-GUIDE.md` PART 17a-OCR updated: the `tessdata` folder now needs app-pool **Modify**
  (not just Read) rights since the worker writes into it; manual download is now documented as the
  air-gapped fallback, pointing at `tesseract-ocr/tessdata` (not `tessdata_fast`).

No schema change, no LHDN/invoice-calculation impact — purely an operational improvement to an
already-optional, already-off-by-default OCR feature. 5 new unit tests
(`EINVWORLD.Tests/Services/TessdataSyncWorkerTests.cs`) cover: download-when-missing, skip-when-unchanged,
discard-when-implausibly-small, multi-language fetch, and one-language-failure-doesn't-block-another —
all against a stubbed `HttpMessageHandler` and a real temp directory, no network or database involved.

## 📅 2026-08-09 — InvoiceEdit brought to full parity with Create e-Invoice

Follow-up to the same-day IDOR fix below: the user reported (with staging screenshots) that the invoice
editor Smart Capture hands drafts off to still "looked old" compared to Create e-Invoice, even after
confirming the routing itself was correct. Comparing the two Razor views side by side confirmed it —
`InvoiceEdit.cshtml` never received the richer redesign `CreateInvoice.cshtml` got in earlier "Stitch
parity" commits (`db90425`, `206a5a2`); it only got a lighter "consistent header" touch-up and the
shared brand-token restyle (`#154`, `#162`), which explains why both looked "restyled" in git history
but not actually alike on screen.

**Step 1 — Basic Information**: added the Bento KPI row (Buyer Status/Currency/Running Total/Document
Type, live-updated), wrapped the existing fields in collapsible "Document Setup" / "Buyer & Supplier"
sections (matching Create e-Invoice's exact field boundaries — no new fields added), restyled
"Additional Party Information" into its own collapsed-by-default section with the "PRE-FILLED"/"SYSTEM
PRE-FILLED" badges, and added the sticky right-hand Invoice Summary + Validation Checklist sidebar. Also
added the SVDP Notice banner (config-gated, same as Create e-Invoice) that InvoiceEdit was missing
entirely.

**Step 2 — Invoice Items**: added a "Row Total" box next to each line's "Subtotal" box, moved "+ Add
Item" to the top of the card (matching Create e-Invoice), and added the Invoice Summary / Amount Payable
block at the bottom. Applied to both the server-rendered items and the two JS templates used when adding
a new row / re-adding a removed row, plus the row-reindexing logic.

**Step 3 — Review & Submit**: replaced the plain two-table layout with Invoice Details / Buyer & Supplier
cards, an Item Summary table (built client-side from the live item rows, no server round-trip), a
Submission Readiness checklist, a Payment & Terms card, and a sticky Total Amount sidebar — while leaving
InvoiceEdit's own action buttons (Save as Draft / Save as Template / Submit to LHDN, and the
`IsTemplateMode` "Update Template" variant) completely untouched; only Create e-Invoice's simpler "Submit
Draft/Template" button set was *not* copied over, since InvoiceEdit's own handlers are real, different,
already-correct backend behavior specific to editing an existing draft.

**Real bug found and fixed along the way**: `calculateTotals()` computed each line's subtotal internally
but only ever wrote it to the DOM's Row Total display — the separate per-row "Subtotal" box was actually
maintained by a different function (`calculateSubtotal()`) triggered only by the user typing into a
qty/price field. For a freshly-opened *existing* draft, that never fires, so the Subtotal box silently
stayed at its "0.00" placeholder — and the new Step 3 Item Summary table, which derives its Tax column as
`Row Total − Subtotal`, showed the full row total as "tax" instead of the real tax amount. Fixed by
having `calculateTotals()` keep the per-row Subtotal box in sync directly, the same way it already did
for Row Total — this also fixes the pre-existing Step 2 display bug for existing drafts, not just the
new Step 3 table.

No schema change, no backend/`.cs` changes at all — pure `.cshtml` markup and inline JavaScript. Verified
live against the real Staging database (local dev environment, per `local-f5-staging-db` — Turnstile uses
Cloudflare's public test keys locally, so automated login works here unlike against the deployed Staging
host) by logging in as Admin, opening a real draft invoice (`EINV100464`), and walking all 3 steps: KPI
tiles and Validation Checklist correctly reflected the invoice's real supplier/buyer/currency, the Item
Summary table showed the correct Tax/Total after the fix, Submission Readiness showed all green, and the
existing Save Draft / Save as Template / Submit to LHDN buttons rendered with their original behavior
intact. The invoice was only viewed and navigated — never saved/submitted — so no shared data was
modified. `dotnet build`/`dotnet test`: unaffected (236/236 pass) since this is a pure front-end change
with no C# logic touched; verification here was via live browser testing rather than new unit tests.

## 📅 2026-08-09 — Critical IDOR fix on InvoiceEdit + AI Document Capture removed from nav

Requested check: confirm Smart Capture's "Open it in the invoice editor" link points at the current,
actively-maintained Create/Edit Invoice form. It does — `InvoiceEdit.cshtml` and `CreateInvoice.cshtml`
were restyled together in the same commit (#162) and both use the Tabler layout; no routing bug. While
verifying that, inspection of the target page turned up a real, pre-existing, **Critical** authorization
gap unrelated to Smart Capture itself but reachable by every draft it creates:

- `InvoiceEdit.cshtml` uses `@page "{id?}"` — `id` is a route value (`/Invoices/InvoiceEdit/INV-000123`),
  not a query-string value, and it's an `InvoiceNo` string, not a `PartyInfoId` int.
- `InvoiceEditModel : SupplierBasePage`. `SupplierBasePage.OnPageHandlerExecutionAsync` — the *only*
  cross-cutting authorization on this page — tries `int.TryParse(Request.Query["id"], ...)` to check the
  requested company's permission. Because this page's `id` is neither a query value nor an int, that
  parse always silently fails, and the check falls back to verifying the *current user's own* primary/any
  company — completely independent of which invoice was actually requested.
- `OnGetAsync` (load for edit) and `OnPostAsync` (save) then loaded/wrote the invoice by `id` with **no
  further ownership check at all**. `OnPostSubmitDocumentsAsync` (the LHDN-submit handler on the same
  page) already had the correct guard — `EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync`, the
  same helper used consistently by `InvoiceDetails2`, `CreateInvoice`/`CreateCN`/`CreateSBCN`/`CreateSBI`'s
  submit handlers, and `InvoiceLists` — making the gap on load/save conspicuous by omission.

**Net effect: any authenticated Supplier-role user with edit rights on any one company could view AND
overwrite any other company's draft invoice** by navigating directly to
`/Invoices/InvoiceEdit/{someOtherCompanysInvoiceNo}` — a cross-tenant financial-data read+write
vulnerability on the exact page every Smart Capture confirmation redirects to.

**Fixed** by adding the same `CanAccessInvoiceAsync` guard already used everywhere else in the app:
- `OnGetAsync` — applied unconditionally right after the invoice is loaded (this handler already 404s if
  `id` is missing or the invoice doesn't exist, so there's no dual-purpose "new invoice" case to special-case).
- `OnPostAsync` — applied **only** when `id` is non-empty *and* an invoice with that number already
  exists in the database. This handler is also used to save a brand-new invoice for the first time (a
  pre-generated `InvoiceNo` that legitimately doesn't exist yet), and `CanAccessInvoiceAsync` correctly
  returns `false` for a non-existent invoice — applying it unconditionally would have broken that
  legitimate flow. AJAX saves get a 403 JSON response (`AjaxFail`, matching the page's existing
  convention); non-AJAX gets a standard `Forbid()`.
- Deliberately reused `CanAccessInvoiceAsync`'s existing supplier-OR-customer-OR-public-customer
  semantics rather than tightening to supplier-only: self-billed invoice types (`11`-`14`) treat the
  *customer* as the effective issuer/editor (mirrors `CreateInvoice.cshtml.cs`'s own TIN-selection logic
  for who may submit them), so a supplier-only check would have incorrectly broken self-billed editing.
  Admin bypass is preserved (the helper short-circuits `true` for Admins, as it already does everywhere
  else it's used).

**Also removed "AI Document Capture" from the sidebar and admin nav menus** (`Pages/Shared/_Sidebar.cshtml`
×2, `_AdminNavigation.cshtml`, `_SupplierNavigation.cshtml`) — functionally superseded by Smart Capture
("Create from Document") across all 5 shipped stages, sharing the same extraction/AI/validation services
and the same `InvoiceDraftService.SaveDraft` path. Matches the convergence plan already documented in
`CLAUDE.md` § "Invoice-input mechanisms": the `/Invoices/CreateFromFile` route and its page/services are
**not** deleted — kept alive as the documented rollback path — only the nav links were removed.

No schema change. `dotnet test`: 236/236 pass against real SQL Server LocalDB, including 3 new tests
against `CanAccessInvoiceAsync` directly (cross-tenant denial, same-tenant + self-billed-customer
allowance, and confirming it correctly returns `false` for a not-yet-existing `InvoiceNo` — the exact
precondition `OnPostAsync`'s guard relies on).

## 📅 2026-08-09 — Smart Capture full re-verification pass: 2 real defects found and fixed

A fresh "check if Smart Capture has been fully implemented" pass (roast → plan → fix → test) across all 5
already-shipped stages. Both real, concrete findings were in Stage 4's auto-submit cancellation path —
everything else (tenant isolation on the Stage 2/4 tables, Admin-page authorization, the
`OnPostCancelAutoSubmitAsync` ownership check, migration additivity, hint-value prompt-injection surface)
held up under review with no changes needed.

- **`SmartCaptureAutoSubmitEligibilityService.CancelAsync` TOCTOU race (real, not theoretical).**
  `SyncJob` has no concurrency token, and the durable worker claims a job via an atomic
  `UPDATE ... WHERE Status = Queued` inside a short transaction, then releases the row lock and runs the
  handler (a real LHDN submission) to completion in a separate later write. The old `CancelAsync` read the
  job, checked its status in C#, then unconditionally wrote `Status = Cancelled` — if the worker's claim
  and completion happened in between that read and that write, Cancel's own write would silently overwrite
  `Completed` back to `Cancelled`, showing the user a false "cancelled" state for an invoice that had
  actually already gone to MyInvois. Fixed with a single atomic conditional UPDATE
  (`_context.SyncJobs.Where(j => j.Id == jobId && j.Status == Queued).ExecuteUpdateAsync(...)`), whose
  affected-row-count tells the caller which case actually happened — no intermediate read to go stale.
- **Disabling a company's auto-submit didn't retract already-scheduled jobs.** An Admin flipping
  `Enabled` to `false` on `/Admin/SmartCaptureAutoSubmit` only stopped *future* schedulings — any job
  already queued during its delay window kept its `NextRunAtUtc` and would still fire. Added
  `SmartCaptureAutoSubmitEligibilityService.CancelAllPendingForCompanyAsync`, called on the true→false
  transition, using the same atomic-UPDATE pattern in bulk (a job the worker already claimed is simply
  outside the `WHERE Status = Queued` filter, so it's correctly left untouched either way).

Also removed one unused local variable (`checksJson` in `ApplyAsync`) found during the pass. No schema
change, no behavior change to the eligibility/scheduling logic itself — this is purely a cancellation-path
correctness fix. `dotnet test`: 233/233 pass against real SQL Server LocalDB, including 2 new tests
reproducing the exact race (job already `Completed` when Cancel runs — never overwritten) and the bulk
retraction (2 pending jobs cancelled, 1 already-`Completed` job for the same company left alone).

## 📅 2026-08-09 — Smart Capture Stage 4 (reduced first cut): conditional automatic submission

The first Smart Capture capability that can submit to MyInvois without a manual click, built as a narrow,
multi-layer-gated exception rather than a new submission path (see `CLAUDE.md` § "Invoice-input
mechanisms" for the full reasoning). Three independent layers must all agree:

1. **Global kill switch** — `SmartCapture:AutoSubmitEnabled` (config, default **false** in every
   committed appsettings file). A company's opt-in has zero effect while this is false.
2. **Per-company opt-in** — `SmartCaptureAutoSubmitSettings`, one row per company, set only from
   `/Admin/SmartCaptureAutoSubmit` (`[Authorize(Roles = "Admin")]`) — never self-service on the
   company's own Supplier workspace. Configures an LHDN doc-type allowlist (default `01` only), a
   required value ceiling (`MaxAutoSubmitValue` — no "unlimited" tier), and a delay in minutes.
3. **Per-document deterministic gate** (`SmartCaptureAutoSubmitEligibilityService`, re-evaluated on
   every single confirmation) — confirmed doc type is in the company's allowlist, the review checklist
   has zero warnings *and* zero errors (reuses Stage 1.5's existing signal), the confirmed buyer has a
   real TIN, and the invoice's total is under the company's ceiling. No fuzzy "confidence score" —
   today's AI provider returns none, so every condition is a plain deterministic check, and the full
   check list (pass/fail + the actual values compared) is written to the audit trail either way.

When all three pass, `SmartCaptureReviewModel.OnPostConfirmAsync` enqueues a `SyncJobType.SubmitDocument`
job — the exact same job type and handler already used to retry a failed interactive submission
(`InvoiceSubmissionHelper.SubmitInvoiceAsync`, with its existing payload-hash idempotency guard, XAdES
signing, retry/backoff, and audit chain, all completely unchanged) — with `NextRunAtUtc` set
`DelayMinutes` in the future. During that window the Smart Capture list page shows an "Auto-submit
HH:mm" badge with a **Cancel** button per document; cancelling just flips the job's `Status` to
`Cancelled`, which the durable worker's existing `WHERE Status = Queued` claim query already excludes —
no new worker logic needed. Evaluation and scheduling are both best-effort or try/caught around the
existing draft-creation flow: any failure here is logged and swallowed, never turns an already-successful
Confirm into a 500, and simply falls back to the pre-existing manual "Submit" flow on `InvoiceEdit`.

New additive migration: `SmartCaptureAutoSubmitSettings` table (unique index on `CompanyPartyInfoId`,
cascade FK to `PartyInfos`) plus a nullable `SmartCaptureDocuments.PendingAutoSubmitJobId` column.

**Deliberately out of scope for this first cut** (can be added later without a schema change): per-company
email notification on scheduling/outcome, a richer confidence signal than "zero review issues", and
per-supplier (not just per-company) granularity.

## 📅 2026-08-09 — Smart Capture Stage 3 (reduced first cut): bulk upload

The Smart Capture upload form now accepts multiple files in one submission (`<input multiple>`), capped
at a configurable `SmartCapture:MaxFilesPerBulkUpload` (default 20 per batch). Each file is passed through
`SmartCaptureDocumentService.UploadAsync` exactly as before — same per-file signature/size/quota
validation, same tenant-scoped storage, same one-durable-job-per-document queuing — so a single upload is
just a batch of one and nothing about the existing per-file pipeline changed. Results are aggregated
per-file: a batch reports "N of M documents uploaded" plus a list of which files failed and why, so one
bad file (wrong signature, over quota, unsupported type) never blocks the rest of the batch. Also fixed a
stale claim on the Smart Capture page ("Scanned for malware before storage") left over from the v1.17.2
ClamAV removal — replaced with an accurate description of the file-type/signature validation it actually
performs.

**Known limitation, not addressed in this cut**: the monthly processing quota is measured in
*successfully extracted* pages (`PageCount`, set only once background extraction completes), not upload
count — so a large bulk batch can queue well past the quota before any of it finishes processing and the
quota check reflects it. This is an existing Stage 1 characteristic, not new to bulk; bulk just makes the
burst more visible. `MaxFilesPerBulkUpload` bounds the worst case per batch. Revisit if real Staging usage
shows this needs a reservation-based quota instead.

Still produces drafts one at a time through the unchanged, always-confirm review screen — no batch draft
creation, no auto-submission. That remains explicitly out of scope until Stage 4, which requires its own
scoped approval given the LHDN-submission blast radius.

## 📅 2026-08-09 — Smart Capture Stage 2 (reduced first cut): learned per-company hints

Each company's Smart Capture extractions now learn from that company's own confirmed drafts: a new
`SmartCaptureCompanyHints` row (one per company) tracks the most commonly confirmed LHDN document type,
currency, tax type, and tax rate using a streaming Boyer-Moore majority-vote counter per field — no
per-confirmation history table needed, and a single early or outlier confirmation can't dominate it. Once
a company has confirmed at least 3 drafts, `SmartCaptureCompanyHintService.GetAsync` surfaces the current
majority as advisory-only context appended to the AI suggestion prompt ("this company's invoices are
usually type X — use this only if the document doesn't clearly indicate otherwise"); the model is free to
disagree, and the review/confirm screen behaviour is completely unchanged. Recorded once per successful
Confirm (`SmartCaptureReviewModel.OnPostConfirmAsync`), never before — a document that fails or is
abandoned never influences future suggestions. `IEInvoiceAssistantService.SuggestInvoiceAsync` gained a
new optional `companyHints` parameter (backward compatible; the AI Assistant chat page and the legacy
`CreateFromFile` page pass none). Deliberately scoped down from the full Stage 2 roadmap (per-supplier
templates with field-level provenance, a 3-tier deterministic/OCR/AI extraction pipeline) — this is a
company-level, fully-automatic, zero-UI first cut; the richer template model remains a future increment
if real Staging usage shows it's worth the complexity.

## 📅 2026-08-08 — Smart Capture Stage 1.5 (reduced first cut): duplicate detection + condensed review

`SmartCaptureExtractionJobHandler` now flags an exact-content re-upload within the same company as a
review-checklist **Warning** (never a block) using the already-indexed `FileHash` column — a different
company uploading byte-identical content (e.g. a shared template) is not flagged, preserving tenant
isolation. On the review screen, an extraction with zero errors and zero warnings now shows a condensed
"All checks passed" summary with the full checklist/raw-suggestion collapsed behind a toggle, instead of
always expanding the full review — the Confirm click is still always required either way; nothing is
auto-decided. This was deliberately scoped down (via `/roast`) from the user's original "Smart Review"
proposal, which would have auto-created drafts for high-confidence extractions with no review screen at
all — that tier was deferred pending a real confidence signal (today's AI provider returns none) and real
Staging usage data. PR #173.

## 📅 2026-08-08 — Smart Capture: remove application-level malware scanning (ClamAV)

**Deliberate architecture decision, explicitly requested and confirmed**, not a bug fix. Smart Capture no
longer scans uploaded files with ClamAV. Upload security instead relies entirely on: file extension
allowlist, magic-byte/file-signature validation (rejects a renamed file whose content doesn't match its
claimed type), configurable file-size/page-count limits, monthly per-company processing quota, storage
outside `wwwroot` under a random internal filename, `SafePath` path-traversal protection, tenant/company
ownership enforcement on every read, an IDOR-protected download endpoint, tiered retention/deletion, and
audit logging — plus normal server-level protection (least-privilege app pool, Windows Server endpoint
protection). This does **not** provide the same guarantee as content-level antivirus scanning; a
well-formed PDF can still carry an embedded exploit that format/signature validation cannot detect. If a
deployment's risk profile requires content scanning, add it at the network/endpoint layer (e.g. scan the
`FilePathConfig:SmartCaptureFolder` directory via Windows Defender/EDR) — EINVWORLD does not provide one.

- **Removed**: `Services/Security/IMalwareScanner.cs`, `Services/Security/ClamAvMalwareScanner.cs`,
  `SmartCaptureOptions.MalwareScanRequired`/`ClamAvHost`/`ClamAvPort`/`ClamAvTimeoutSeconds`, the
  `MalwareDetected`/`ScannerRequiredButUnavailable` upload-failure reasons, the
  `SmartCaptureMalwareDetected`/`SmartCaptureMalwareScanSkipped` audit actions, the `MalwareScanRequired`
  field from the startup log line, and the ClamAV install/verify steps from `IIS-DEPLOYMENT-GUIDE.md`
  PART 17d and the EICAR test step from `POST-DEPLOY-CHECKLIST.md`.
- **Updated**: all three `appsettings*.json` `SmartCapture` sections (no more `MalwareScanRequired`/
  `ClamAv*` keys), `README.md`, `IIS-DEPLOYMENT-GUIDE.md` PART 17d (now documents the upload-security
  controls Smart Capture relies on instead, and a renamed-file-rejection verification step in place of
  the EICAR test), `POST-DEPLOY-CHECKLIST.md`, and `CLAUDE.md` (new "Upload security (Smart Capture — no
  malware scanning)" note alongside the existing invoice-input-mechanism architecture rule).
- `dotnet test`: 220/220 pass (unchanged — no test asserted on ClamAV behavior beyond a fake "always
  clean" double, which is simply no longer needed).

## 📅 2026-08-08 — Smart Capture: fix Critical "Confirm" failure (`PrefixedID` NOT NULL)

While closing a testing gap (no live run in this environment ever reached the actual "Confirm → create
draft" step, since there's no reachable Ollama here — every live attempt terminated earlier at
`NoTextExtracted`), added an integration test that exercises the **real** extraction → review → confirm
pipeline end-to-end (only the AI provider network call is faked; every EINVWORLD service and table
involved is real, against real SQL Server). It failed immediately: `InvoiceDraftService.SaveDraft` — a
**pre-existing service, not touched by the Smart Capture port** (traces back to the repo's first commit)
— has always thrown when creating a new invoice, because it never sets `InvoiceHeaders.PrefixedID`, a
NOT NULL column. It was simply never called by anything until Smart Capture added its one and only
caller (`SmartCaptureReviewModel.OnPostConfirmAsync`). **Every Smart Capture "Confirm" click would have
failed** with "Failed to create the draft invoice. Please try again." — deterministically, with no way
to recover — while `CreateInvoice.cshtml.cs` was unaffected because it uses a separate save path that
already sets `PrefixedID = Invoice.InvoiceNo` correctly.

- **Fix**: `Services/InvoiceDraftService.cs` — set `PrefixedID = model.InvoiceNo ?? string.Empty` in the
  new-invoice branch, matching the existing pattern in `CreateInvoice.cshtml.cs`. One line.
- **New test**: `Successful_Extraction_Through_Confirm_Creates_A_Real_Draft_Invoice` — the first test in
  the suite to exercise the full pipeline with a successful (faked-provider) extraction, proving a real
  `InvoiceHeader` is created with correct totals, doc type, and line items, and the `SmartCaptureDocument`
  is correctly linked (`Status=DraftCreated`, `RelatedInvoiceHeaderInvoiceNo` set).
- `dotnet test`: 220/220 pass against real SQL Server LocalDB (219 prior + this new one).
- This does **not** change the outstanding Stage 1 gate — real Ollama extraction, real ClamAV scanning,
  and full Staging end-to-end sign-off are still required before production. It does mean that gate is
  now worth pursuing: the step it was ultimately gating (draft creation) is confirmed working.

## 📅 2026-08-08 — Smart Capture (Stage 1): ported from the stale, never-merged PR #164

Merged to `main` via **PR #170** (squash), which closed the original **PR #164** as superseded. Smart
Capture Stage 1 (persisted, async supplier-invoice capture → draft) was fully built and locally verified
back on 2026-08-04 on branch `fix/lhdn-getdoc-burst-429` (PR #164), but that branch was never merged and
went stale — `main` picked up unrelated work in the meantime (the LHDN full-import widen fix, the
Quantity/ExchangeRate display fix, and the Manage Resources CMS/SEO-GEO redesign above), none of which
PR #164 ever incorporated.

- **Ported cleanly onto current `main`**, file-by-file and hunk-by-hunk — not a raw branch merge — so
  none of PR #164's staleness (reverted Resources pages, reverted LHDN lookback default, reverted
  CreateInvoice/InvoiceEdit CSS token rebrand) came along with it. Regenerated the EF migration
  (`Migrations/20260808063519_AddSmartCaptureDocument`) fresh against `main`'s current model instead of
  reusing the old branch's Designer/ModelSnapshot, so it chains correctly after
  `20260807120000_AddSeoGeoFieldsToResourceItem`; table shape verified byte-identical to the original.
- **Two config defaults fixed during the port**: the base `appsettings.json` `SmartCapture` section had
  `Enabled=true`/`MalwareScanRequired=true`, contradicting its own comment and meaning Staging (which
  never overrides `Enabled`) would have silently shipped the feature on. `appsettings.Production.json`
  had both `SmartCapture.Enabled` and unrelated `AI.Enabled` flipped `true` with no corresponding
  Staging/Ollama/ClamAV sign-off. Both reverted to the project's stated default (`false`).
  Deploy/config reference is unchanged (`README.md`, `POST-DEPLOY-CHECKLIST.md` already document the
  `SmartCapture` section and the ClamAV/EICAR verification step — no new doc entries needed there).
- **One defect found and fixed via `/roast`** (Medium): `SmartCaptureReviewModel.OnPostConfirmAsync` had
  no double-submit guard — two concurrent "Confirm" POSTs could both create a draft invoice, and the
  losing request would throw an unhandled `DbUpdateConcurrencyException`. Fixed by catching the conflict
  and redirecting to whichever invoice actually won, reusing the existing `SmartCaptureDocument.RowVersion`
  concurrency token — no new abstraction, no schema change. Regression-tested against real SQL Server.
- **Two pre-existing Playwright test bugs fixed** in `tests/playwright/13-smart-capture.spec.js`
  (inherited from the original branch, unrelated to the port itself): a hardcoded `localhost:5210` in the
  anonymous-download check (now uses the configured `baseURL`), and a 60s terminal-state wait that didn't
  account for `AI:TimeoutSeconds` (180s) when Ollama is unreachable (extended to 200s/240s).
- **Verified this session**: `dotnet build` clean; `dotnet test` 219/219 against real SQL Server LocalDB
  (not the in-memory provider); a focused security review (tenant isolation, IDOR, malware fail-closed
  behavior, secrets/PII handling — no findings); the migration applied to the shared Staging database
  (idempotent — reconciled pre-existing schema drift from an earlier unmerged deploy attempt rather than
  re-creating anything); and the full `13-smart-capture.spec.js` Playwright spec, 4/4 pass on a clean,
  verified single app instance.
- **Still outstanding** (the user's own stated Stage 1 production gate, not achievable from this
  environment): a successful extraction using real Ollama output, real ClamAV clean/infected-file
  scanning, and full Staging end-to-end sign-off. **Not production-ready until those pass.**
- **`appsettings.Staging.json`: `SmartCapture:Enabled` flipped `true`.** Note: per `IIS-DEPLOYMENT-GUIDE.md`
  Part 2 / Part 10 and `DEPLOY-NOTES.md`, the real Staging server runs with `ASPNETCORE_ENVIRONMENT=Production`
  (the "Staging" distinction is DB/URL only, via manually-edited `appsettings.json` values on that box) —
  so `appsettings.Staging.json` is **not loaded there** and this file change alone does not enable the
  feature on the real server. **To actually enable it on Staging, set the `SmartCapture__*` environment
  variables** in IIS (Part 10) per the new PART 17d below, then `iisreset`. The `appsettings.Staging.json`
  change only takes effect if something is explicitly run with `ASPNETCORE_ENVIRONMENT=Staging` (e.g. a
  future differently-configured environment, or local `dotnet run` in that mode).
- **`IIS-DEPLOYMENT-GUIDE.md` PART 17d added** (was referenced by README/`POST-DEPLOY-CHECKLIST.md` but
  missing from the port) — ClamAV install/verify steps and the `SmartCapture__*`/`FilePathConfig__*` env
  vars needed to enable this feature on a real server.

## 📅 2026-08-07 — Manage Resources (CMS): SEO/GEO redesign

- **New SEO/GEO metadata on `ResourceItem`**: `MetaTitle`, `MetaDescription`, `FocusKeyword`,
  `CanonicalUrl`, `OgText`, `ImageAlt`, `Author`, `Tldr` (AI-answer-engine summary), `SchemaType`
  (Article/FAQ/HowTo), and `FaqItemsJson` (FAQ Q&A pairs, stored as JSON — not a child table). All
  nullable/defaulted — additive migration
  (`Migrations/20260807120000_AddSeoGeoFieldsToResourceItem` + `Apply_AddSeoGeoFieldsToResourceItem.sql`),
  targets `WebsiteDbContext` (its own, separate `__EFMigrationsHistory`).
- **New `Helpers/ResourceSeoScorer`**: single source of truth for the 0-100 SEO/GEO readiness score
  (slug, meta title/description length, focus keyword, canonical URL, image alt, author, TL;DR, schema
  type, and FAQ-question-count-when-FAQ). Mirrored in `wwwroot/js/resource-seo.js` for the live gauge
  on Create/Edit (no round trip) — the two are kept in sync intentionally; update both if the checklist
  changes.
- **Manage Resources (list)**: added a per-row SEO score chip and "AI SEO Assistant" sidebar (Global
  Site Health % + static suggestions), plus **real server-side pagination** (previously loaded every
  resource on one page). Existing filters, Add Type modal, and Backfill Slugs untouched.
- **Create/Edit Resource**: two-column layout — SEO/GEO panel (live gauge, char counters, Meta
  Title/Description/Focus Keyword/Canonical URL/Social Share Text), Alt Text, AI Summary/TL;DR, and a
  Structured Data section with a Schema Type select + FAQ Q&A builder (shown only when Schema Type =
  FAQ). Title→slug auto-derive, TinyMCE, and the ImageMagick image pipeline are unchanged.
- **Resource Types**: added a computed "SEO Prefix" (`/{code}/`) column; fixed a pre-existing bug where
  creating a type with a duplicate Code threw an unhandled 500 instead of a validation error (now
  matches the existing duplicate-check already used by the Manage Resources "Add Type" modal).
- **Public Article page**: `<title>`/meta-description now fall back to the new MetaTitle/MetaDescription
  when set (else Title/Summary as before) — the only change to the public-facing page.
- **Hygiene**: `Create.cshtml.cs` now has an explicit `[Authorize(Roles="Admin")]` matching its siblings
  (the `/Admin` folder policy already covered it — defence-in-depth, not a live vulnerability fix).
- **Fixed `appsettings.json`**: a missing comma after `CompanyLogosFolder` (introduced in a recent
  appsettings update) made the entire file invalid JSON, so the app failed to start locally at all.
  Restored valid JSON; no config values changed.
- New unit tests: `ResourceSeoScorerTests` (score/tier boundaries, FAQ JSON round-trip) and
  `WebsiteDbContextModelTests` (catches EF model-validation regressions — e.g. a public `List<T>`
  property on an entity being mis-mapped as a navigation — without needing a live DB connection).

## 📅 2026-08-05 — Format Quantity/ExchangeRate to 2 decimal places in invoice display/PDF

- **Quantity showed 6 decimal places instead of 2** (`1.000000` instead of `1.00`) on the Invoice
  Details page and in Print/Download PDF — reported with a screenshot. `@item.Quantity` rendered raw
  in three places while the sibling `UnitPrice`/`TaxAmount`/`Total` columns in the same tables already
  used `.ToString("N2")`. Fixed to match: `Pages/Invoices/InvoiceDetails2.cshtml`,
  `Views/Invoices/PdfTemplate_v2.cshtml` (the live template `PDFGeneratorService` renders for Print/
  Download PDF), and `Pages/Invoices/PdfTemplate.cshtml` (legacy, not currently wired into any code
  path, fixed anyway). Audited every other decimal field in those three files — all already correctly
  formatted; Quantity was the only outlier.
- **`InvoiceTemplate.ExchangeRate` had no `[DisplayFormat]`**, so `@Html.DisplayFor` on
  `Pages/Templates/Details.cshtml`/`Delete.cshtml` rendered it at raw precision too (same bug class).
  Added `[DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = false)]` on the model
  property (`Models/Templates/InvoiceTemplate.cs`) — fixes both pages at once, doesn't affect the
  editable `<input>` in Create/Edit. Confirmed by grep this is the only read-only render of
  `ExchangeRate` anywhere in the app.

## 📅 2026-08-05 — Widen admin "Import All Invoices from LHDN" to every registered company

- **`Pages/Admin/InvoiceSync.cshtml.cs` `OnPostFullImportAllAsync`** previously enqueued a `FullImport`
  job only for TINs linked to the clicking admin's own account (`User.GetUserCompanies`), so a manual
  deep backfill silently missed every other company registered in EINVWORLD. Investigated after a user
  report of external-ERP-submitted invoices not appearing in the Buyer "Received" tab; confirmed the
  *scheduled* background import (`InvoiceStatusUpdater.RunLhdnImportAsync`, every 10th poll cycle) was
  already correctly iterating all TINs in `UserCompanies` system-wide — only the manual admin trigger
  was narrower than intended. Changed to query `UserCompanies` distinct TINs directly, matching the
  scheduled job's scope.
- Still one `SyncJob` row per TIN, drained sequentially by the single `DurableSyncJobWorker` instance
  and paced within each run by `LhdnRateLimitHandler` — widening the TIN list only increases queue
  depth, not request concurrency, so LHDN rate-limit exposure is unchanged.
- **`InvoiceStatusUpdaterSettings:BackgroundImportLookbackDays`** widened `3` → `7` days
  (`appsettings.json`, default in `Models/Settings/InvoiceStatusUpdaterSettings.cs`) so the scheduled
  background import (`InvoiceStatusUpdater.RunLhdnImportAsync`) doesn't miss an external-ERP invoice
  that lands a few days late. 7 days still fits inside `GetAllUuidsForTinAsync`'s single 10-day
  date-chunk, so this adds zero extra `/documents/search` calls per cycle per TIN — no change in LHDN
  request volume, only in how far back each existing call looks.

## 📅 2026-08-03 — Restyle: finish rolling the "EinvWorld Professional" tokens onto Create/Edit Invoice

> A Stitch mockup (`stitch_einvworld_tabler_redesign/`) proposed a much larger "AI Workspace" product
> redesign (per-field AI suggestion cards, inline compliance checks, a collaborative audit timeline,
> etc.) — that's a multi-phase product initiative with no existing data model or AI backend to support
> it, not a UI ticket, so it was **not** implemented (see the written assessment from this
> conversation). What *was* in scope: `CreateInvoice.cshtml`/`InvoiceEdit.cshtml` still had leftover
> hardcoded Bootstrap-default colors (`#dc3545`/`#198754` for validation, `#e9ecef`/`#ced4da` for
> borders) predating the earlier "EinvWorld Professional" rebrand (`einvworld-tokens.css`), and a local
> `.card, .card-body { box-shadow: none; }` override that flattened these two pages' cards relative to
> the rest of the app. Visual-only fix — same data, same backend, no new features.

### Fixed
- `CreateInvoice.cshtml`/`InvoiceEdit.cshtml`: replaced hardcoded validation colors
  (`#dc3545`/`#198754` + their `rgba()` focus-ring shadows) with `var(--einv-error)`/
  `var(--einv-success)`, and border colors (`#e9ecef`/`#ced4da`) with `var(--einv-border)`, in both
  the CSS and the two JS validation-state assignments (`field.style.borderColor = ...`).
- `CreateInvoice.cshtml`: removed a local `.card, .card-body { box-shadow: none; }` override that
  killed the app-wide soft card shadow (`0 2px 4px rgba(0,0,0,.02)`, from `einvworld-tokens.css`) on
  this page only — restored to match; also fixed `.page-title-box`/`.form-group-card`/
  `.table-responsive`/`.tax-row`/`.progress` to the same border/shadow tokens.
- Checked `CreateCN.cshtml`/`CreateSBI.cshtml`/`CreateSBCN.cshtml` — none have this legacy CSS
  (simpler pages), no changes needed. Left the SweetAlert submission-success popup's own color set
  untouched — a self-contained decorative treatment, out of scope for this pass.

## 📅 2026-08-03 — CodeQL follow-up: path-traversal + log-injection hardening on the new reject/cancel code

> CI's CodeQL scan flagged 8 new alerts (2 high, 6 medium) in the PR above — all genuinely in the new
> code, not pre-existing findings resurfacing. Fixed all 8 before merging.

### Fixed
- **`PDFGeneratorService.GeneratePdfFromHtmlAsync`** (high, CWE-22 path traversal) — `invoiceNo`
  reaches this method from user-facing routes (e.g. the PDF-download handler) and was combined into
  the file path with a raw `Path.Combine`. Switched to `SafePath.TryResolve` (the same guard already
  used for resource/logo/editor-upload paths) so it can never escape the PDF folder.
- **6 new log statements** (medium, CWE-117 log injection) added by the reject/cancel concurrency-retry
  and PDF-retry code in the PR above logged a route/query value (`documentId`, the resolved PDF path)
  without stripping CR/LF, which could forge extra-looking lines in the text log file. Added
  `LogSanitizer.ForLog` (strips CR/LF; existing `MaskTin`/`MaskId` are for PII, a different concern)
  and applied it at all 6 sites.

## 📅 2026-08-03 — Fix Rejection/Cancellation email delivery + related reject/cancel reliability bugs

> Investigated a production report: "RejectionEmails and CancellationEmails not received by both
> Supplier and Buyer" despite MyInvois's own emails arriving immediately, plus a review of the day's
> log file for other errors worth fixing. Root cause of the email issue: `SendRejectionNotificationEmail`/
> `SendCancellationNotificationEmail` threw `InvalidOperationException: GlobalBccEmail is empty`
> **before reaching the buyer/supplier send logic at all** — confirmed by every single occurrence in
> the log. This exact class of bug (an optional admin-CC address treated as required) was already
> found and fixed for the Valid-status email in an earlier session, but the fix was never applied to
> Rejection/Cancellation.
>
> Investigating further surfaced a second, more serious bug: in `InvoiceListsModel.
> UpdateLocalDatabaseForRejection`, the rejection email was sent *before* `SaveChangesAsync()`, inside
> the same try block — so the GlobalBccEmail exception (or any other email failure) aborted the local
> database update entirely, even though LHDN had already accepted the rejection. The interactive
> request then returned HTTP 500 to the user, who — seeing an apparent failure — retried the same
> reject action, which LHDN correctly rejected a second time with `IncorrectState` ("already requested
> for rejection"), matching a repeated failure pattern for the same document seen in the log across
> several hours. The equivalent Cancel handler in the same file already saved first and emailed
> best-effort afterward (with its own concurrency retry) — this fix brings Reject up to the same
> standard, and extends it to both handlers in `InvoiceDetails2.cshtml.cs` for consistency.

### Fixed
- **`EInvoiceNotificationService.SendRejectionNotificationEmail`/`SendCancellationNotificationEmail`**
  — no longer throw when `GlobalBccEmail` is blank; log a warning and send without the BCC, matching
  `SendValidatedNotificationEmail`. Converted to `async Task<bool>` (was `void`, synchronous,
  catch-and-swallow) so the caller can tell success from failure and retry — same contract as
  `SendNewInvoiceReceivedNotificationEmail`. Sends to whichever of buyer/supplier has a valid email
  independently (previously some call sites required *both* to be present or sent to neither).
- **`InvoiceListsModel.UpdateLocalDatabaseForRejection`** — the local status-update save no longer
  depends on the email succeeding; it's saved first (with a concurrency-conflict retry, matching the
  Cancel handler's existing pattern), then the email is attempted best-effort afterward.
- **`InvoiceDetails2Model.OnPutRejectDocumentAsync`/`OnPutCancelDocumentAsync`** — added the same
  concurrency-conflict retry-once as the `InvoiceLists` handlers, for parity/defense in depth against
  a concurrent background status-sync write.
- **`PDFGeneratorService.GeneratePdfFromHtmlAsync`** — retries once after a short delay on `IOException`
  writing the PDF file, mitigating a transient Windows file-sharing-violation seen once in the log
  (another process briefly holding the same freshly-written PDF).
- **`appsettings.Staging.json`** — added a `LHDNApiConfig:RateLimits` override, roughly halving
  `SearchPerMinute`/`GetDocPerMinute`, after the log showed ~20 real 429 responses from LHDN across
  the day (all recovered within the existing retry/backoff budget — no permanent failures, but
  frequent delays). This is a conservative empirical adjustment, not a guaranteed fix; keep
  monitoring and tune further if 429s are still frequent.

### Added
- **`InvoiceHeader.IsRejectionEmailSent`/`RejectionEmailSentAt`/`RejectionEmailSentTo`** and
  **`IsCancellationEmailSent`/`CancellationEmailSentAt`/`CancellationEmailSentTo`** (migration
  `20260803200000_AddRejectionCancellationEmailTrackingToInvoiceHeader`, additive, 4 artifacts incl.
  idempotent `Apply_*.sql`) — same "`true` = not applicable" default pattern as
  `IsNewInvoiceReceivedEmailSent`, so every existing invoice is automatically exempt; the reject/cancel
  handlers set the relevant flag to `false` in the *same save* as the status transition, opting that
  row into the retry pipeline below.
- **`InvoiceHeader.CancellationReason`** — persisted (Reject already had `RejectedReason`) so a
  background retry of a failed cancellation email can rebuild the email body without the original
  interactive request's in-memory value.
- **`IInvoiceFinalizer.SendRejectionEmailAsync`/`SendCancellationEmailAsync`** — same atomic-claim
  (`ExecuteUpdateAsync WHERE !IsXEmailSent`) and rollback-on-failure pattern as
  `SendNewInvoiceReceivedEmailAsync`, so a failed immediate send is retried by the next background
  pass instead of being lost. The interactive handlers still attempt the send immediately for a
  snappy, MyInvois-like experience — these are the safety net for when that attempt fails.
- **`InvoiceStatusUpdater.RunRejectionCancellationFinalizerAsync`** — new step in the existing
  background loop, alongside the pre-existing Valid-status and new-invoice-received finalizer passes.

## 📅 2026-08-03 — Diagnose & mitigate E-Invoice Assistant timeout on Staging

> User report: `Admin/AiSettings` "Test Connection" showed Reachable + Model Ready in ~2s, but
> asking a real question in `/Assistant` timed out after 120s ("AI chat failed via Ollama/gemma3:12b
> (Timeout)"). Root cause: the two calls do very different things. "Test Connection" hits Ollama's
> `/api/tags` — a cheap metadata list, no model loading. A real chat hits `/api/chat`, which forces
> Ollama to load the full `gemma3:12b` (12B params) from disk into memory first if it isn't already
> resident — and Ollama unloads an idle model after 5 minutes by default, so a question asked after
> any gap pays that full cold-load cost again. On Staging's hardware that's evidently taking longer
> than the 120s timeout.

### Added
- **`AI:KeepAliveMinutes`** (default 30, `AiSettings.cs`) — sent as Ollama's own `keep_alive`
  parameter on every chat request (`OllamaAiProvider.ChatAsync`), keeping the model resident for
  this long after each use instead of Ollama's 5-minute default. Only a genuinely long idle gap
  pays the cold-load cost again; every question asked within the window reuses the loaded model.
  Surfaced read-only on `Admin/AiSettings` next to the other AI config values.

### Operator action (no deploy needed, do this now if Staging is still timing out)
- Bump `AI__TimeoutSeconds` (env var) or `AI:TimeoutSeconds` (`appsettings.Production.json`/
  `appsettings.Staging.json`) on the Staging server to something like `240`–`300` and restart the
  app pool, so *today's* first cold load has enough time to finish. The `KeepAliveMinutes` fix
  above (once deployed) prevents this from recurring on every subsequent question, but doesn't
  change how long that unavoidable first cold load takes.

## 📅 2026-08-03 — Fix stale resource-image 404 tolerance in `10-tabler-modules.spec.js` (test-only)

> Investigated the last remaining Playwright failure — turned out to be a real, fixable test bug,
> not an environment gap as first suspected. The test author had already anticipated "some
> article/company images are missing on staging (404)" and added a tolerance regex for it
> (`/\/images\/resources\/|\/Companies\/Logos\//i`), but that regex was written for the **old**
> static-file route. `ResourcesMigrationController`/`Pages/Admin/Resources/Create.cshtml.cs`/
> `Edit.cshtml.cs` all moved resource images to a new API-served route,
> `/api/resources/images/{category}/{size}/{fileName}` (segment order reversed, `/api/` prefix
> added) — so the tolerance silently stopped matching anything created after that migration, and
> `/Admin/Resources/Manage` started failing on 404s it was always meant to ignore.

### Fixed
- `tests/playwright/10-tabler-modules.spec.js` — extended the tolerance regex to also match
  `/api/resources/images/`, alongside the still-valid old `/images/resources/` pattern (for any
  resource rows that predate the migration) and `/Companies/Logos/`.

## 📅 2026-08-03 — Investigate & skip admin-2FA demo-data drift (test-only)

> Root-caused the last remaining pre-existing Playwright failure from the full-suite pass above.
> Not a code bug: `Login.cshtml.cs`'s 2FA redirect (`result.RequiresTwoFactor`) is standard,
> correct ASP.NET Identity behavior driven entirely by the account's `TwoFactorEnabled` flag.
> Confirmed via the account's own Manage > Two-factor authentication page ("Add authenticator
> app", not "Disable 2FA") that the shared demo `admin@einvworld.com` account currently has 2FA
> **disabled** in this DB — the test's name/assumption ("admin with 2FA enrolled") predates that.

### Fixed
- `tests/playwright/02-auth.spec.js` — the admin-2FA test now accepts landing on either
  `LoginWith2fa` (enrolled) or `Dashboard` (not enrolled) and skips gracefully in the latter case,
  matching the honest-skip pattern already used elsewhere in this suite, instead of failing on a
  precondition the test can't control. Deliberately did **not** enable 2FA on the account directly
  — TOTP enrollment is a one-time interactive step (scan a QR code, can't be seeded via
  migration/script) on a *shared* demo account, and doing it silently would break anyone else's
  manual login with that account, who'd suddenly be asked for a code they don't have. Enrolling it
  through the app's own UI, by whoever owns that shared credential, re-enables the real check.

## 📅 2026-08-03 — Full Playwright suite pass: 82/112 → 106/110 (test-only)

> Ran the entire suite for the first time in a while. Went from 82 passed/28 failed/2 skipped to
> 106 passed/2 failed/2 skipped. The 2 remaining failures are confirmed pre-existing/environment,
> not app bugs (see below) — nothing in this pass touched app code.

### Fixed
- `auth-layout-fix.spec.js` — hardcoded its own `BASE = 'http://localhost:5280'` fallback instead
  of using the shared `playwright.config.js` `baseURL` every other spec relies on, so all 23 of its
  tests were failing with `ERR_CONNECTION_REFUSED` against the real dev server (port 5210). Switched
  to relative `page.goto()` calls. Also updated a stale assertion — `auth: forgot-password shows
  "What happens next?" reassurance` expected an info box that no longer exists; the page went
  through the Stitch auth-pages restyle since this test was written and now uses a single
  descriptive sentence instead. Updated the assertion to match the current, intentional design.
- `stitch-create-invoice-stepper.spec.js` — same hardcoged-port problem (`BASE_URL` env var,
  defaulting to port 5261), *and* asserted markup (`#stepDot1..3`, `#formProgressLine`) that no
  longer exists in `CreateInvoice.cshtml` — fully superseded by `12-create-invoice-parity.spec.js`
  (which already covers the stepper plus full wizard navigation, and actually logs in). Deleted
  rather than patched, to avoid two overlapping stepper tests.
- `11-company-details-parity.spec.js` — the "Verified" status pill assertion failed because
  `COMPANY_ID=1` (the test's default) isn't owned by the logged-in `supplier@einvworld.com` account
  in this environment's DB, so the per-company IDOR guard correctly denied access ("Access denied")
  — the guard working as intended, not a bug. Added the same honest-skip pattern the test already
  uses for a missing PartyInfo row (404/500), so an access denial skips gracefully too instead of
  failing.

### Confirmed pre-existing, not fixed (out of scope — no app code involved)
- `02-auth.spec.js` admin-2FA test — documented demo-data drift from an earlier session.
- `10-tabler-modules.spec.js` — `/Admin/Resources/Manage` 404s on `/api/resources/images/...`: the
  app correctly points `ResourceImagesFolder` at Staging's `E:\EINVWORLD_STAGING\...` path (shared
  DB), but that folder only exists on the real Staging server, not a local dev machine — an
  environment/file-storage gap, not a code defect.

## 📅 2026-08-03 — Fix pre-existing `12-create-invoice-parity.spec.js` failure (test-only)

> Flagged in the prior review pass as pre-existing (reproduced identically on pre-#154 file
> versions). Root cause: the test clicked "Next: Invoice Items" without filling every
> `[required]` field in Step 1 first — `validateCurrentStep()` correctly blocks the wizard from
> advancing until they're set, exactly like it would for a real user. Not an app bug; the test
> never simulated filling them in.

### Fixed
- `tests/playwright/12-create-invoice-parity.spec.js` — Select2 hides the underlying `<select>`
  (`display:none`), which fails Playwright's `.selectOption()` actionability check, so added a
  `selectFirstRealOption()` helper that sets `.value` + dispatches `change` directly (matching what
  the wizard's own validation JS reads). Used it to pick a Document Type, Currency, and Buyer before
  advancing from Step 1, and to fill the default blank line item's classification/description/
  quantity/unit/price/tax before advancing from Step 2. Also fixed an unrelated locator collision —
  `#step2 .btn-primary` matched both the "+ Tax" button and "Next: Review & Submit"; scoped to
  `button[onclick="nextStep()"]`.
- Verified stable over repeated runs, and re-ran the public + auth Playwright suites alongside it —
  no other regressions.

## 📅 2026-08-02 — Review cleanup: dead variable + duplicate audit row in the new-invoice-received email

> Follow-up from a full review of this session's recent PRs (#150-154): confirmed correct via a
> fresh build, full test run, and Playwright checks (public pages, supplier/buyer login, the
> reverted-file A/B check below). Two small, low-severity findings fixed; nothing else needed
> changing. No functional behavior change to the notification itself.

### Fixed
- **`InvoiceFullSyncHelper.cs`** — removed an unused `isNewInvoice` local variable left over from
  an earlier draft of the new-invoice-received email logic; its comment claimed to "drive" the
  notification but the actual behavior is governed entirely by the `if (invoice == null)` branch
  structure a few lines below. Dead code, not a functional bug.
- **`EInvoiceNotificationService.SendNewInvoiceReceivedNotificationEmail`** — removed a duplicate
  `InvoiceHistory` write. The method wrote its own "NewInvoiceReceivedEmailSent" history row, and
  its caller (`InvoiceFinalizer.SendNewInvoiceReceivedEmailAsync`) *also* wrote one after a
  successful send — every send produced two near-identical audit rows on the invoice's activity
  timeline. (The pre-existing `SendValidatedNotificationEmail`/`FinalizeInvoiceAsync` pair has the
  same double-write; left untouched here as out of scope for this cleanup — flagged as a follow-up.)

### Verified (no code change needed)
- Migration `20260802130000_AddNewInvoiceReceivedEmailTrackingToInvoiceHeader` — additive, correct
  `DEFAULT 1` backfill, `.Designer.cs`/`ModelSnapshot.cs` consistent.
- `InvoiceFinalizer`'s atomic-claim-then-send pattern (both the existing Valid-email flow and the
  new one) — correctly claims via `ExecuteUpdateAsync WHERE <flag> = false` and rolls back on a
  thrown exception for indefinite retry.
- Dark mode cookie handling (`_LayoutTabler.cshtml`) — the cookie value is validated against an
  exact `"dark"`/`"light"` allowlist before being interpolated into the `<html>` tag; no injection
  path even if the cookie were tampered with.
- A pre-existing, unrelated Playwright failure (`12-create-invoice-parity.spec.js`, step-1 → step-2
  navigation) was confirmed present on the pre-PR-154 file versions too (reverted-file A/B test) —
  not a regression from this session's UI fixes.
- Full test suite (186/186) and `dotnet build` pass; ad hoc Playwright checks confirmed the Step 1
  KPI tile updates live and all three E-Invoice Assistant mode panels hold a consistent height, with
  zero browser console errors on either page.

## 📅 2026-08-02 — UI balance/layout fixes: Create/Edit Invoice wizard, E-Invoice Assistant

> User-reported, screenshot-annotated UI issues across the invoice-creation wizard and the AI
> assistant page. No functional/data changes — CSS and markup only.

### Fixed
- **`CreateInvoice.cshtml`** — Step 1 "Running Total" KPI tile now updates live as items are added
  (`calculateTotals()` previously never wrote to it, so it stayed frozen at "RM 0.00"). An unscoped
  `.form-control:valid`/`.form-select:valid` rule painted every optional field (Reference Document
  Number, PO/DO No, Exchange Rate) with a permanent green border, since HTML5 treats a
  constraint-free field as always-valid even when empty — scoped to `[required]` fields only,
  matching the pattern already used for the invalid-state rules. The Reference UUID Select2 dropdown
  (Credit Note flow) had its internal padding zeroed out, so a long UUID ran straight into the clear
  (×) and dropdown-arrow icons — reserved space for both icons and added ellipsis truncation. The
  line-item tax row's percentage input was squeezed into a 50–65px box (conflicting
  `min-width`/`flex-basis`) — widened to a consistent 90px with more breathing room. Step 1/Step 2
  Previous/Next button footers had no visual separation from the content above — added a top border.
- **`InvoiceEdit.cshtml`** — same tax-row width/spacing and button-footer separator fixes (shares the
  same wizard markup as `CreateInvoice.cshtml`); its `:valid` CSS was already correctly scoped, no
  change needed there.
- **`Assistant/Index.cshtml`** (E-Invoice Assistant) — the "Create from Description" and "Fix an
  Error" mode panels are just a header + one input row until a result appears, while "Ask a
  Question" is a fixed-height chat panel; switching to either of the shorter panels collapsed the
  card to a sliver at the top of the page. Gave all three mode panels a shared minimum height so
  switching modes no longer causes a jarring size collapse.
- `CreateCN.cshtml`, `CreateSBI.cshtml`, `CreateSBCN.cshtml` were checked and don't share any of the
  above code paths (simpler single-form pages) — no changes needed.

## 📅 2026-08-02 — Email notification (with retry) for new e-invoices received from external ERPs

> Follow-up to the LHDN-sync questions above. `InvoiceFullSyncHelper` was already correctly detecting
> and importing e-invoices submitted directly to LHDN by an external ERP (buyer-side sync), but it
> deliberately set `IsValidationEmailSent = true` on creation specifically to suppress the "Validated"
> email — reusing that email for a first-time-received invoice would have been misleading copy ("your
> invoice has been validated" implies we submitted it). Added a distinct notification, then — per a
> follow-up question about whether the existing Valid-status email has a completion/retry check — gave
> it the same atomic-claim-and-retry robustness as that flow, rather than shipping it best-effort.

### Added
- **`InvoiceHeader.IsNewInvoiceReceivedEmailSent`/`NewInvoiceReceivedEmailSentAt`/`NewInvoiceReceivedEmailSentTo`**
  (migration `20260802130000_AddNewInvoiceReceivedEmailTrackingToInvoiceHeader`, additive, 4 artifacts
  incl. idempotent `Apply_*.sql`) — mirrors the existing `IsValidationEmailSent` trio exactly. Defaults
  to `true` ("not applicable") both in the C# model and via the migration's column default, so every
  other invoice-creation path in the app (normal Sent-invoice submission, Credit/Debit notes, and every
  pre-existing row backfilled by the migration) is automatically exempt without touching those files.
  `InvoiceFullSyncHelper` explicitly sets it to `false` only for a genuinely new, buyer-side invoice
  synced from LHDN, opting that one row into the retry pipeline below.
- **`IInvoiceFinalizer.SendNewInvoiceReceivedEmailAsync`** (`InvoiceFinalizer.cs`) — same
  atomic-claim-then-send, roll-back-on-failure pattern already used for the Valid-status email:
  claims the row (`ExecuteUpdateAsync WHERE !IsNewInvoiceReceivedEmailSent`), attempts the send, and on
  a thrown exception (SMTP down, bad config) rolls the claim back so the **next background pass retries
  it — indefinitely, no age cutoff**, exactly like the existing Valid-email safety net. Distinguishes
  "no valid buyer email" (permanent, marks done, never retried) from a transient send failure (retried).
  Also handles the disabled-feature and expired-recency cases by claiming and marking done without
  sending, so neither keeps reappearing in every future pass.
- **`InvoiceStatusUpdater.RunNewInvoiceReceivedFinalizerAsync`** — new step in the existing background
  loop (runs every poll cycle, alongside the pre-existing `RunFinalizerAsync` safety net for the
  Valid-status email), querying simply `WHERE !IsNewInvoiceReceivedEmailSent` — no other precondition
  needed, since the flag itself is only ever set `false` for the exact rows that should get this email.
- **`SendNewInvoiceReceivedNotificationEmail`** (`IEInvoiceNotificationService`/`EInvoiceNotificationService`)
  — buyer-only (the supplier already knows they sent it), new template
  `wwwroot/EmailTemplates/NewInvoiceReceivedEmailTemplate.html`, new
  `EmailConfiguration:NewInvoiceReceivedEmailSettings:Subject` config (environment-prefixed in
  Staging/Production, matching every other email type). Returns `false` (not an error) when there's no
  valid buyer email; throws on an actual send failure so `InvoiceFinalizer` can tell the two apart. Sent
  without a PDF attachment — fires before the separate PDF-generation pass has necessarily run.
- **`EmailConfiguration:Notifications:EnableNewInvoiceReceivedEmails`** kill switch (default `true`).
- **Recency guard** — `EmailConfiguration:NewInvoiceReceivedEmailSettings:MaxAgeDaysForNotification`
  (default `7`), checked in `SendNewInvoiceReceivedEmailAsync` at send time. Only genuinely recent
  invoices (by `IssueDate`/`DateTimeReceived`) get emailed; older invoices that are merely new to
  EINVWORLD's database (e.g. pulled in by the Admin's 60-day historical "Import All Invoices from LHDN")
  are claimed and marked done without sending. Without this, a large backfill would email buyers about
  invoices they already know about — a spam risk, not a helpful notification.

### Tests
- `dotnet build`: 0 errors (pre-existing unrelated warnings only).
- `dotnet test`: 186/186 passing, no regressions.
- Migration **not** applied locally against the real Staging DB (local `dotnet run` uses
  `AutoMigrateOnStartup: true` against the live shared Staging database — not something to trigger
  without being asked). CI's SQL Server LocalDB integration tests apply it with `Migrate()` for real,
  which is the intended verification path per `CLAUDE.md`.

## 📅 2026-08-02 — Configurable lookback for the automatic LHDN full-document-search import

> Follow-up to a question about whether externally-submitted e-invoices (an external ERP submitting
> directly to LHDN, with the local company as Buyer) show up in the Received tab. Confirmed they do —
> `InvoiceFullSyncHelper.SyncAllFromApiAsync` already handles the "we are the buyer" case, creating the
> supplier's `PartyInfo` from LHDN's data if EINVWORLD has never seen them. The automatic background
> import (`InvoiceStatusUpdater.RunLhdnImportAsync`, every 10th 600s poll cycle) is what catches these,
> but its lookback window was hardcoded to 3 days with no way to widen it if a sync cycle is missed for
> longer (app restart, LHDN outage).

### Added
- `InvoiceStatusUpdaterSettings.BackgroundImportLookbackDays` (default `3`, matching prior hardcoded
  behaviour — no behaviour change unless the config is edited). Wired into the
  `RunFullImportFromLhdnAsync` call in `InvoiceStatusUpdater.cs`. Kept separate from
  `LHDNApiConfig:SyncRetentionDays` (a much longer one-time deep-backfill window used only by the
  Admin-triggered manual "Import All Invoices from LHDN" — the two settings serve different purposes
  and shouldn't share a value).

## 📅 2026-08-02 — DESIGN.md system-wide audit + skip-navigation links

> Audited the app against the just-expanded `docs/DESIGN.md` (TABLES/FORMS/ACCESSIBILITY/
> COMPONENT LIBRARY sections). The Velzon→Tabler migration itself is effectively complete — every
> functional module already defaults to `_LayoutTabler`/`_LoginLayoutTabler` via folder-level
> `_ViewStart.cshtml`; only the public marketing pages (Home, Resources) intentionally use a
> different layout. Two real gaps found; one fixed here, one deferred pending scoping.

### Fixed
- **No skip-navigation link anywhere** (WCAG 2.2 AA gap). Added a "Skip to main content" link — the
  first focusable element on the page — to `_LayoutTabler.cshtml`, `_LoginLayoutTabler.cshtml`, and
  `_HomeLayout.cshtml`, targeting a `tabindex="-1"` anchor (`#einv-main-content`) on each layout's
  content container. Uses Bootstrap's standard `.visually-hidden-focusable` utility, already present
  in both `tabler.min.css` and the public site's `bootstrap.min.css` — no new CSS. Verified in a real
  browser: hidden by default, becomes visible on focus, `#einv-main-content` target resolves.

### Deferred (needs scoping, not fixed here)
- **49 files use inline `style="..."` attributes**, spread across every module (Invoices 11,
  Suppliers 8, Admin 8, PublicCustomer 5, Items 4, RecurringInvoices 3, Dashboard 3, Templates 2,
  Profile 2, Lead 2, Assistant 1) — violates `CLAUDE-UI-RULES.md` §14. Each needs individual review
  (some may be legitimate dynamic/computed values) rather than a blind find-replace; not actioned in
  this change per CLAUDE.md's "surface a scoped plan and get agreement first" for high-blast-radius
  changes touching every module.
- Minor: the FAB button's hover transition (`einvworld-tokens.css`) has no `prefers-reduced-motion`
  guard — low priority, single rule.

## 📅 2026-08-02 — Dark mode for the Tabler app (authenticated pages only)

> Adds a light/dark toggle to the Tabler-based authenticated app (Admin/Supplier/Buyer). Public
> marketing pages and the Velzon fallback layout are unchanged.

### Added
- **Theme toggle** in the topbar (`_TablerTopbar.cshtml`) — sun/moon icon button using Tabler's
  built-in `.hide-theme-dark` / `.hide-theme-light` convention (already shipped in `tabler.min.css`,
  no new CSS needed for the icon swap).
- **`_LayoutTabler.cshtml`**: reads an `einv-theme` cookie server-side and renders `data-bs-theme`
  directly on `<html>` for returning visitors — zero flash of the wrong theme, works even before JS
  runs. First-time visitors (no cookie) get a blocking inline script, first thing in `<head>`, that
  follows `prefers-color-scheme` instead (Tabler's own CSS has no pure-CSS auto-dark fallback, so this
  has to be JS). Cookie value is allowlisted (`"dark"`/`"light"` only) before touching the HTML attribute.
- **`einvworld-ui.js`**: `initThemeToggle()` — click handler flips `data-bs-theme` and persists the
  choice as a 1-year cookie so the next server render already knows it.
- **`einvworld-tokens.css`**: `--einv-page-bg`/`--einv-surface`/`--einv-text`/`--einv-text-muted`/
  `--einv-border` now flip under `[data-bs-theme="dark"]`, matched to Tabler's own dark gray-900/800/700
  palette so the rebrand blends with Tabler's built-in dark components instead of introducing a second
  dark palette. A handful of rules used hardcoded (non-variable) light colors and needed explicit dark
  overrides: the green-tinted table headers (`.table thead th`, `#invoiceTable`/`.einv-mobile-stack`
  `thead.einv-table-head th`) and the topbar search pill.
- Verified in a real browser (dev server, self-hosted asset files) against a smoke-test page built from
  the actual shipped markup/CSS/JS: light→dark→light toggle, cookie persistence, icon swap, card/table/
  badge/sidebar/pagination legibility all confirmed correct.

### Not changed (deliberately)
- Public marketing pages and Velzon fallback pages are out of scope — Velzon already has its own,
  separate dark-mode toggle from before the Tabler migration.

### QA note (unrelated to this change)
- The local/Staging demo accounts `buyer@einvworld.com` and `supplier@einvworld.com` both currently fail
  login with "Your company TIN is missing. Please contact support." — a pre-existing seeded-data issue on
  this Staging DB snapshot, not caused by this change. Blocked authenticated-page Playwright verification
  for this PR; flagging in case it's blocking other QA too.

## 📅 2026-08-01 — Diagnosed remaining page-load stall: Cloudflare Web Analytics beacon (not CSP)

> Investigated a "site still feels slow" report using staging HAR captures (`/`, `/login` ×2) and
> `SystemLogs`. The reported hypothesis (CSP causing the slowdown) does not hold: CSP ships as
> `Content-Security-Policy-**Report-Only**` (`Program.cs`), which never blocks a request — it only logs
> violations to `/csp-report`. It cannot be the cause of a network-level stall.

### Root cause (confirmed via HAR, no code bug)
- All three captured page loads show the identical pattern: `DOMContentLoaded` delayed **21–34 s** by
  `https://static.cloudflareinsights.com/beacon.min.js` (status 0, fully spent in the browser's `blocked`
  phase — a hung/slow fetch, not a CSP block), followed by `www.googletagmanager.com/gtm.js` adding another
  20-35 s to `onLoad`. The GTM half is already mitigated (`_GoogleAnalytics.cshtml` injects GTM only after
  `DOMContentLoaded`, per the 2026-07-09/#144 fixes) — the CF beacon is a **new, third instance** of the
  same failure class as the Rocket Loader issue in v1.9.6: an uncontrolled third-party script gating the
  page lifecycle.
- `static.cloudflareinsights.com/beacon.min.js` is **not referenced anywhere in our HTML/JS** (confirmed —
  only a Playwright analytics-noise filter matches the hostname). It is injected directly by the
  **Cloudflare edge** (the zone's "Web Analytics"/"Browser Insights" auto-beacon), the same mechanism as
  Rocket Loader, and is outside application code entirely.

### Required operator action (Cloudflare dashboard — cannot be fixed from code)
- **Disable Web Analytics / Browser Insights auto-injection** for the zone (*Analytics & Logs → Web
  Analytics*, or the equivalent toggle exposing the auto-beacon). The app already has its own GTM-based
  analytics, so this beacon is redundant — same reasoning as disabling Rocket Loader in v1.9.6. See
  `POST-DEPLOY-CHECKLIST.md`.
- **Done and verified 2026-08-01.** RUM was set to "Disable" for the `einvworld.com` Web Analytics site.
  Re-captured HAR on `/` and `/login` confirms `DOMContentLoaded` dropped from 21-34 s to **0.9-2.5 s**,
  and `static.cloudflareinsights.com/beacon.min.js` no longer appears at all.
- **`gtm.js` still hung 23-57 s** in the same network path (same `status: 0`, fully-`blocked` pattern as the
  CF beacon had) even after the Cloudflare fix above — it no longer stalls `DOMContentLoaded` (the existing
  post-DCL-injection fix already isolates it), but it delayed `onLoad`. **Fix:** `appsettings.Staging.json`
  now overrides `GoogleAnalytics.MeasurementId` to empty, the same mitigation already in place for
  `appsettings.Development.json` — GTM never loads on Staging, so this hang can't happen there either.
  **Trade-off (accepted):** Staging can no longer be used to smoke-test GTM/GA tag behavior before a
  Production release; Production is unaffected — this override applies to Staging only, and Production
  never had a mitigation removing it.

### Not changed (deliberately)
- CSP was **not** promoted from Report-Only to enforcing, and `cloudflareinsights.com` was **not** added to
  the CSP allowlist — Report-Only never blocked the beacon in the first place (so allowlisting it would not
  improve speed), and promoting to enforcing is a separate, higher-blast-radius change gated behind the CDN
  cleanup already noted in `Program.cs`.

## 📅 2026-07-29 — Role Management, company user removal, LHDN SDK 1.0 compliance, bug fixes

> Five-PR stacked release, all merged to `main`. Additive migrations only (`AddRoleModulePermissions`,
> `AddCompanyRolePartyInfoScope`) — no drops, no breaking changes. See `DEPLOY-NOTES.md` §1 for the
> manual-apply scripts (Production requires them; Staging/dev auto-migrate).

- **Fixed: supplier invitation "Create Account & Join" silently failing.** `AcceptInvite`'s forms were
  missing hidden `Id`/`Token` fields — `asp-page-handler` posts lost the route data, so every submission
  looked like an invalid/expired invitation even with a fresh link.
- **Fixed: no email on invoice Reject/Cancel.** The `InvoiceDetails2` reject/cancel handlers updated LHDN
  and logged history but never sent the notification email (that logic only existed on the other
  invoice-list page). Now sends immediately once the LHDN status update is committed.
- **Fixed: RBAC access-denied bugs** — Recurring Invoices denied Owner-role access (checked legacy
  `HasCompanyAccess`/`IsViewOnly` flags, ignored `CompanyRole` assignments); Company Profile was
  Admin-only by omission even though its sibling Company Management tabs already allow Supplier, with
  proper tenant-membership + `EditProfile` permission checks added.
- **Added: Supplier Owner/Admin can remove a company member** (blocks self-removal and removing the last
  Owner) — the capability existed for invite/revoke but not removal.
- **UI**: one-click "Enable 2FA" for a user who disabled it but kept their authenticator (previously
  forced a full re-setup); Received/Buyer tab actions cleaned up (removed supplier-only actions leaking
  into the buyer view, added the missing Print action); E-Invoice Assistant restyled into a
  scrolling message-bubble chat thread, and disabled/unreachable states now render as a warning instead
  of an indistinguishable-from-a-crash red error alert.
- **New: Admin → Role Management** (User Management → Role Management) — manage the global Identity role
  catalog (create/delete roles assignable via Manage Users' "Change Role"; `Admin`/`Supplier`/`Buyer` are
  protected as core roles) and a **Module Access** grid restricting which app modules the Supplier/Buyer
  roles can reach, enforced by a new `ModuleAccessPageFilter` layered on top of each page's existing
  `[Authorize(Roles=...)]` gate. A module with no configured row defaults to allowed — purely additive.
- **New: company-scoped custom roles.** A Supplier Owner/Admin can create a custom role (name + the 4
  permission flags) scoped to just their own company (`CompanyRole.PartyInfoId`, nullable — `null` =
  the shared system roles every company sees), alongside Roles & Permissions' existing role assignment.
- **LHDN MyInvois SDK 1.0 compliance** (audited against `sdk.myinvois.hasil.gov.my/sdk-1-0-release`):
  - **Unit-of-measure validation**: `InvoiceMapper` now validates every invoice line's unit code against
    the official LHDN code list before building the UBL JSON — previously only enforced client-side by
    the Create Invoice form's dropdown, with no server-side check and no check at all for CSV import,
    templates, or recurring invoices.
  - **Signed SVDP 1.3**: document version is now correctly computed from `SigningEnabled`/`DocVersion` —
    previously hardcoded to "1.0"/"1.2" regardless of config, so turning on v1.1 signing (once the
    certificate is purchased) would inject a valid signature but the document would still declare the
    unsigned version, and signed SVDP (1.3) was unreachable entirely.
  - **Configurable rate limits**: `LhdnRateLimitHandler`'s per-endpoint limits moved from hardcoded
    constants to `LHDNApiConfig:RateLimits:*` in `appsettings.json` (existing values kept as defaults),
    so production and sandbox tiers can differ without a code change.

## 📅 2026-07-27 — Tabler migration completion (Invoices, Admin, Company/Lead), homepage redirect fix, responsive QA fixes

> Nine-PR stacked release, all merged to `main`. No breaking changes, no schema changes.
> - **Invoices & Templates**: InvoiceDetails2/CreateSBI/CreateCN/CreateSBCN/BulkImport/ImportCSV and
>   TemplateLists/InvoiceEdit restyled to Tabler; extracted a shared `invoice-line-items.js` module; fixed
>   a Razor bug rendering subtotal as literal `qty * price` text instead of the computed product.
> - **Admin subsystem** (ops/monitoring, Notifications, Resources + Types, Users, 9 Codes list pages,
>   RecurringInvoices): full Tabler restyle, removing remaining emoji headers, FontAwesome icons, and
>   lord-icon CDN widgets.
> - **Company/Lead management**: Suppliers/Index (List of Companies), Import, AssignBuyers, Lead/List
>   restyled. Fixed a real cross-cutting bug — `einvworld-tokens.css` shimmed Velzon's `.avatar-title` but
>   never its `avatar-xxs`–`xl` size scale, so ~90 avatar-sized images app-wide rendered unconstrained.
> - **Fix**: `Pages/Index.cshtml` (public homepage) had no auth check — a logged-in user hitting `/` saw
>   the marketing page rendered inside the legacy Velzon `_Layout` fallback, exposing its dev-only Theme
>   Customizer panel. Now redirects authenticated users to `/Dashboard/Dashboard`.
> - **Responsive QA**: ran `tests/playwright/05-responsive.spec.js` and `10-tabler-modules.spec.js` with
>   real viewport sizing against a live instance for the first time (prior browser-automation tooling
>   couldn't actually resize the viewport). Found and fixed four overflow bugs: a Dashboard gutter/container
>   mismatch, a CSS rule that disabled horizontal-scroll containment above the mobile breakpoint on
>   Items/Index and PublicCustomer/List, an unwrapped button group plus a non-shrinking flex label/value
>   pair on PublicCustomer/List, and TemplateLists' table wrapped in Velzon-only dead CSS classes.
> - **Cleanup**: removed 189 unused Velzon component-showcase demo pages (`wwwroot/assets/libs/*.html`,
>   42MB) — confirmed zero references anywhere in the app before deleting.

## 📅 2026-07-26 — Company Management workspace, Buyer/Items/AI Tabler migration, Admin sidebar off-canvas

> Five-PR stacked release. No breaking changes; the migration is additive. See `DEPLOY-NOTES.md` §1
> for the squash rationale (a backlog of previously-unapplied migrations found on Production) and the
> required post-migration PII-encryption backfill step.

- **Buyer Management** (`Pages/PublicCustomer/*`) restyled to Tabler (KPI cards, sortable table, mobile
  card-stacking). Adds a read-only **Duplicate Review** page for TIN/name collisions. Fixes a Delete IDOR
  (unlinking a shared buyer record was hard-deleting it even when other suppliers still referenced it)
  and a pre-existing import-preview bug (`PreviewRecords != null` was always true).
- **Company Management workspace**: "My Company" is now a tabbed workspace — Overview, Profile, **Users**
  (token-based invitations; invitees always set their own password), **Roles & Permissions**
  (company-scoped Owner/Admin/Editor/Viewer roles, falling back to the legacy access flags for
  unassigned members), **Invoice Branding** (accent color/footer/bank-visibility — settings only, not
  yet wired into PDF rendering), **Security** (2FA status, recent activity), and **Audit** (paginated,
  TIN-scoped `AuditLog` view). Removed the old "Create User" modal that let an admin set another user's
  password directly.
- **AI Assistant / Document Capture** pages restyled to Tabler; new read-only **Processing History** page.
- **Items & Services**: added **Unit** (LHDN unit-of-measure code) and **Unit Price** (`decimal(18,4)`)
  across create/edit/list/import/invoice-line-picker. Restyled to Tabler.
- **Admin sidebar**: mobile nav is now a true Bootstrap off-canvas drawer (was a plain inline collapse);
  desktop collapsed (icon-only) state gained tooltips.
- **Database:** a production backup audit during this release's rollout found the live database was
  ~3.5 months behind head (last applied: `RemovePreFix`, 2026-04-15) — 22 pending migrations, 2 of which
  (`AddLhdnIntermediaryRejectedFlag`, `FixPendingModelChanges`) had never had a hand-authored apply
  script. Rather than ship 22 more individually-numbered migrations on top of that gap, all 22
  (this release's 4 plus the 18-migration backlog) were **squashed into one**:
  `20260726135229_ConsolidatedSchemaCatchup_v1_11_0`. Rehearsed against a full restore of the actual
  production backup in three states — fully behind, fully caught-up (simulating an already-auto-migrated
  Staging), and re-run for idempotency — zero errors in every case, and confirmed `SystemLogs` (111k+
  existing rows, owned by the Serilog sink, not EF) is never dropped. See `DEPLOY-NOTES.md` §1 for why
  Staging needs the new script run manually once before its next deploy.

## 📅 2026-07-26 — Bigger logo, cleaner login header, collapsible sidebar, local AI enabled

> Presentation + local-dev-config pass across the Tabler shell and the Identity auth pages. No
> schema/migration change; Production/Staging AI defaults are unchanged (still OFF).

- **Brand logo, bigger everywhere:** sidebar brand (`_TablerSidebar`) grown 2.25rem→3rem on desktop
  (2rem→2.5rem on mobile); all 10 Identity auth pages (Login, Register, Forgot/Reset Password, 2FA,
  Recovery Code, Resend/Register Confirmation) grown 100px→140px.
- **Login header decluttered:** removed the redundant `<p>eInvWorld</p>` wordmark under the logo on all
  10 auth pages — the logo image already carries the brand name.
- **Sidebar collapse-to-icons (desktop):** new toggle button in `_TablerSidebar` shrinks the sidebar to a
  4.5rem icon rail and back, persisted via `localStorage` and applied synchronously on load (no flash of
  the expanded sidebar) via an inline script in `_LayoutTabler`. Mobile's existing Bootstrap
  auto-collapse below the `lg` breakpoint is unchanged.
- **AI Assistant + AI Document Capture enabled for local Development only**
  (`appsettings.Development.json`), pointed at whatever Ollama model is actually pulled on the dev
  machine. Verified end-to-end against a running local Ollama instance. Production/Staging remain OFF
  by default per existing policy — `AI:Enabled` is not required for invoicing to keep working.
- **Bug fix:** `einvworld-ui.js` was missing `asp-append-version`, so browsers could cache it indefinitely
  across deploys and silently run stale JS. Added, matching the CSS links in the same layout.

## 📅 2026-07-23 — Company Details (My Company) Stitch-parity restyle

> Presentation-only restyle of `Pages/Suppliers/Details.cshtml` to the Stitch design mockup
> (`screen.png`). All handlers, role checks, assign/unassign modals and the `PartyInfo` data
> binding are preserved. No schema/migration change.

- **Identity card:** Verified / Active / Supplier status pills added (Verified ← `PartyInfo.IsApproved`,
  Active ← `IsActive`; no new fields invented).
- **Two-column data grid:** the flat field list is split into **Legal & Registration** and
  **Contact & Finance** sections matching the mockup.
- **Assigned Buyers:** the `<ul>` list is replaced by a table (Buyer Entity / TIN / Industry /
  Manage + Unassign) reusing the existing `unassignBuyer` handler and assign modals. "Manage" is a
  `mailto:` (no buyer-detail route exists on this page — left as a link, not a dead button).
- **Verified by Playwright:** new `tests/playwright/11-company-details-parity.spec.js` asserts the pills,
  both grid sections and the buyers table, and screenshots the page for visual diff. Run via
  `npm run qa` against a live instance (`EINVWORLD_BASE_URL`, `COMPANY_ID`).

## 📅 2026-07-23 — Create e-Invoice wizard → Stitch parity (UI migration)

> Full UI migration of `Pages/Invoices/CreateInvoice.cshtml` to the Stitch design system (3-step
> mockup). Markup rebuilt with reusable Tabler components; **all** business logic, `asp-for` bindings,
> validation, JS (`nextStep`/`prevStep`/`calculateTotals`/`addItemRow`/auto-fill), LHDN handlers and
> permissions preserved. No schema/migration change.

- **Stepper:** the thin Bootstrap progress bar is replaced by a Stitch 3-node step indicator
  (`.ci-stepper`) that still drives the existing `#formProgress` fill and `InvoiceManager.updateProgress()`.
- **SVDP notice:** Stitch info banner, gated on `LHDNApiConfig:SvdpEnabled` (unchanged behaviour).
- **Step 1 (Basic Information):** card header restyled to icon style; Additional Party Information
  card gets a primary left-accent border + `SYSTEM PRE-FILLED` badge; footer split into
  **Discard Draft** + green **Next** (matches mockup; "Reset" renamed, same `location.reload()`).
- **Step 2 (Invoice Items):** header icon style; **Add Item** paired with a live subtotal/tax/total
  readout (`#step2Subtotal/.../Total`) beside the line-items table (rows/columns/bindings unchanged).
- **Step 3 (Review & Submit):** summary rebuilt as two Stitch accent cards (`.ci-summary-row`) keeping
  every `summary*` id; action row = Previous / Save as Draft / Save as Template / Submit to LHDN.
- **Branding:** EINVWORLD green (`--einv-primary:#006948`) only; no mockup brand text, no Tailwind/
  Material-Symbols/Inter CDN imported (Tabler + Remix icons used).
- **Verified by Playwright:** `tests/playwright/12-create-invoice-parity.spec.js` asserts stepper,
  step visibility toggles, add-item, review summary ids and submit handlers (appearance + function).

## 📅 2026-07-16 — Stitch batch 6: invoice list module redesign (All/Draft/Sent/Received)

> Full Stitch-parity redesign of the invoice list pages (references 7–10). Presentation-layer
> restructure of `Pages/Invoices/InvoiceLists.cshtml` plus one scoped page-model change. All
> handlers, GET query names, role checks, anti-forgery and LHDN behaviour preserved. No schema change.

- **New "All Invoices" tab** (Suppliers/Admins; Buyers stay Received-only): a fourth
  `invoiceDirection=All` view showing every document where one of the user's company TINs is a
  party. **Security fix included:** previously an unrecognised `invoiceDirection` query value
  skipped every ownership branch in `OnGetAsync` and would list other companies' invoices; the
  direction filter now falls back to mandatory TIN scoping (same guard the export handler had).
- **Single underline tab row** — All Invoices / Draft / Sent / Received; the equal-width
  "… e-Invoices" tab bar and the second status-pill row are gone (LHDN status remains available in
  the filter card).
- **Stitch page header:** breadcrumb → title → per-tab description on the left; page actions
  (Filter, Refresh from API, Export, Customize, Create Invoice) right-aligned. Refresh is hidden on
  the local-only Draft tab; Create Invoice shows on All/Draft for non-view-only Suppliers.
- **Instruction clutter removed:** the "How to Use the e-Invoice Actions" card
  (`_HelpInstructions.cshtml` deleted — it was only used here), the Draft quick-tip and the Sent
  how-to callout are gone from the list pages.
- **Lean fluid table:** columns reordered to Invoice # / Type / Buyer (Supplier on Received) /
  Date / Total (right-aligned with currency) / Internal Status / LHDN Status / Created By / Action.
  Technical metadata (UUID, Submission ID, Rejected Date, Last Updated) is hidden by default and
  available via the existing Customize modal (defaults now direction-aware; saved preferences still
  win). The JS column resizers, drag-to-scroll panning, sticky-column hacks and fixed
  `width:max-content` layout were removed — the table fits 1440 px without horizontal scrolling.
- **Stitch badges:** compact tinted pills (`.einv-badge-*`) — green valid/completed, amber
  submitted/pending, red invalid/rejected/cancelled, grey draft; Invalid stays clickable to open the
  LHDN validation-failure details.
- **Table footer:** result count + page-size on the left, compact numbered pager (1 … n) on the
  right. Sort-header and pager links now **preserve all active filters** (previously most header
  sorts dropped them).
- **Mobile:** rows collapse to labelled cards below 768 px (`data-label` CSS), tabs scroll
  horizontally on narrow screens.
- The 30-second session status sync now reads invoice numbers from the rows rather than the
  selection checkboxes, so tabs without bulk selection (All) still auto-refresh statuses.
## 📅 2026-07-16 — Stitch auth-pages parity (refs 1–4): Login, Register, Forgot Password, Resend Confirmation

> Visual parity pass against the Stitch auth references. Markup/CSS only — no handler, route,
> validation, Turnstile or honeypot change. No schema change.

- **Login:** dark bold "Welcome Back !" title (was green); the Register / Resend-confirmation links
  now sit in a soft-green footer strip attached to the card (`.einv-auth-alt`) instead of floating
  below it (also fixes an invisible `text-white` line on the light background).
- **Forgot Password:** restyled to the reference — left-aligned dark title + plain sentence
  (truthful copy: a reset **link** is emailed), uppercase EMAIL ADDRESS label, primary
  "Send me a reset link" button with mail icon, divider and "Forget it, send me back to the login
  screen". The amber alert box and the **lord-icon CDN animation are removed** (self-hosted-only policy).
- **Resend Confirmation Email:** dark title + explanatory sentence, uppercase label,
  "Resend Email" primary button, and a previously missing "← Back to Login" link.
- **Register:** dark bold title (was green). The reference's "GDPR Compliant / ISO 27001" trust
  chips are deliberately **not** added — unverifiable compliance claims.
- Audit outcome for the other Stitch refs: Navigation Master (5) and Supplier Dashboard (6) already
  match from batches 4–5; the reference's decorative notification-bell/help topbar icons are
  intentionally omitted (no notification feature behind them).

## 📅 2026-07-15 — Stitch parity batch: inline filters, bulk bar, Created By, chart palette

> Follow-up to the restyle: closes the structural gaps against the Stitch references on the invoice
> list and dashboard. Same GET/handler semantics everywhere — no route, permission, or query changes
> beyond one new displayed column. No schema change.

- **Inline filter bar (invoice list):** the offcanvas filter panel is now an always-visible filter
  row (Search / Date Range / Document Type multi-select / LHDN Status / Internal Status / Apply /
  "Clear all filters"), collapsible via the Filter button. Same form, same field names/ids
  (`date-picker.js` keys on `#submissionDateRange`), same GET submit; selects now reflect the
  currently-applied filters; the current sort is carried through as hidden fields.
- **Floating bulk-action bar:** the contextual bulk buttons (Submit/Delete/Cancel/Request Reject)
  moved from the header into a dark bottom-center bar that appears with an "N Selected" count while
  rows are ticked — button ids unchanged, all existing handlers intact.
- **Created By column** on the invoice list (data already on `InvoiceHeader`), integrated with the
  Customize column preferences (client + server defaults) and hidden on phones like other
  secondary columns.
- **Chart palette aligned to the brand:** categorical palette now leads with the compliance green;
  the Status Breakdown donut colours are keyed by status *name* (the previous positional array
  painted whichever status came first red). `dashboard.js` now cache-busted via
  `asp-append-version`.
- Note: the Stitch dashboard's "Recent Documents" table and "Status Breakdown" donut already
  existed on the dashboard below the fold — no structural change was needed there.

## 📅 2026-07-15 — "EinvWorld Professional" UI restyle (Stitch design system)

> System-wide visual refresh to the approved Stitch design direction, applied through the central
> Tabler token file so every authenticated page + the auth pages inherit one consistent look.
> CSS/markup-attribute changes only — no logic, menu, column, action, or route changes. No schema change.

- **Brand tokens** (`wwwroot/tabler/css/einvworld-tokens.css`): primary rebranded from the bright
  `#3AA564` to the deep compliance green `#006948` (hover `#005137`, container `#00855d`); semantic
  set standardized (success `#10b981`, warning `#f59e0b`, error `#ef4444`, info `#0ea5e9`); page
  canvas `#F6F8FB` vs white cards; subtle borders `#E6E8EB`; soft card shadows; invoice-table-scoped
  zebra striping. All Tabler-derived buttons/links/tabs/badges inherit automatically.
- **Sidebar** (`_TablerSidebar`): dark → **light** per the Navigation Master reference (white surface,
  subtle divider, green active states) with the dark-text logo variant (the white-text logo would be
  invisible on white). Role-based menus, links, collapse behaviour unchanged.
- **Auth pages** (`einvworld-auth.css` + `_LoginLayoutTabler`): "Forest Tech Precision" dark-gradient
  look replaced by the Stitch light centered-card design (dotted cool-gray canvas, colour logo above
  the card, deep green primary action). **Removed the Google Fonts CDN dependency** (Hanken Grotesk)
  — policy: self-hosted only; the stack is now Inter-first with the system fallback. All form ids,
  Turnstile, validation, and antiforgery markup untouched. Auth pages now use the colour logo with
  proper alt text.
- **Cache-busting:** `asp-append-version="true"` on the Tabler/token/auth CSS links in both layouts so
  a deploy can't serve stale styles.
- **Hard-coded brand colours** in page markup/JS (`#3AA564`/`#055332`/`#2f8f56` in CreateInvoice,
  InvoiceEdit, InvoiceLists, InvoiceDetails2, Profile, _UserMenu, 3 Identity pages) updated to the new
  palette so no page shows the old green. (Velzon fallback layouts intentionally untouched — Phase 8.)

## 📅 2026-07-15 — Fix the staging 429 storm (session auto-refresh) + log noise

> Root-caused from the 14–15 Jul staging logs: 567 of 644 warnings on 15 Jul were LHDN
> `429 Too Many Requests`, with the same Valid invoice polled by 8+ concurrent request contexts.
> No schema/migration change.

- **Poll cooldowns now actually engage for unchanged Valid invoices.** The cooldown in
  `InvoiceSyncRules.ShouldSkipValidRefresh` was keyed on `InvoiceHeader.LastUpdated`, which only
  advances when LHDN data *changed* — so a long-unchanged Valid invoice was re-polled on every
  30-second UI tick. New in-memory `InvoicePollAttemptTracker` records each poll *attempt* (before
  the call, so a 429-failed attempt also cools down) and the cooldown uses the later of the persisted
  and attempt times, composed in one place (`InvoiceSyncHelper.TryBeginDetailsPoll`) for both the
  batch and single-invoice paths. No extra DB writes (avoids rowversion churn that was also producing
  "concurrency conflict" warnings). Deliberately unchanged: manual/admin-triggered syncs still always
  poll (no cooldown, as before — an explicit "sync now" should sync), and `InvoiceStatusUpdater`
  already self-heals its queue by bumping `LastUpdated` each poll. (+ unit tests)
- **`SyncActiveSession` (InvoiceLists auto-refresh) hardened:**
  - **Per-TIN scoping (IDOR fix):** the handler accepted arbitrary invoice numbers from the browser
    and synced any Valid invoice with no company check — any authenticated user could probe other
    tenants' invoice numbers and burn the shared LHDN rate budget. Now scoped to invoices where one
    of the caller's company TINs is the supplier or (public) customer.
  - **Single-flight (per user):** overlapping session syncs (every open tab posts every 30 s, and a
    rate-limited pass can run minutes) now return immediately instead of stacking. The gate is
    per-user so one user's slow pass can't starve other users' refresh.
  - **Cancellation + 429 abort:** the loop stops when the browser gives up
    (`HttpContext.RequestAborted`, now also cancelling the helper's internal 15 s LongId-retry wait)
    and aborts the pass when LHDN's rate limit persists through the helper's retries (the helper now
    rethrows the exhausted 429 as a clean warning instead of an ERROR + stack trace per invoice).
    Removed the 250 ms manual delay (`LhdnRateLimitHandler` paces). Known limit: the retry sleeps
    inside `LHDNApiService.SendWithRetryAsync` are not yet cancellable mid-wait.
  - **Client:** the 30-second timer skips a tick while the previous sync request is still in flight.
- **Log noise:** EF Core's "Savepoints are disabled because MARS is enabled" warning (hundreds of
  identical lines/day) is suppressed via `ConfigureWarnings` — the underlying behaviour is unchanged
  and already handled by app-level catch/reload paths; `InvoiceFinalizerService` heartbeat demoted
  from Warning to Debug.
- Operator notes: consider removing `MultipleActiveResultSets=true` from the server connection
  strings if nothing depends on MARS (would re-enable transaction savepoints); the startup warning
  about `MaxRequestBodySize` vs IIS `maxAllowedContentLength` needs the IIS request-filtering limit
  raised to match (`web.config`/IIS Manager), not an app change.

## 📅 2026-07-13 — Validation emails were silently never sent + submit modal hung on 429

> Found in local F5 testing: invoices showed "email sent" but nothing arrived, and a bulk submit
> left the "Submitting to LHDN…" modal hanging for 15+ minutes. No schema/migration change.

- **Validated email: empty `GlobalBccEmail` no longer kills the send.** The service threw when the
  BCC wasn't configured (it is deliberately blank in the repo config), and because the method also
  swallowed every exception, **no Supplier/Buyer email was ever sent while callers still flagged
  `IsValidationEmailSent = true`**. Now: missing BCC → warn and send without it; and send failures
  **propagate** to the caller so the finalizer's atomic claim rolls back and the email is retried.
- **Post-submit status poll is now time-boxed (20 s).** `GetDocumentDetailsAsync` can sleep for
  minutes inside the shared 429 penalty handler; polling up to 5× inside the user's submit request
  produced a 16-minute `POST` (seen live: 974 s). The poll now stops at the budget / on a persisted
  429 and defers to the background poller.
- Operator note: invoices finalized while the bug was active have `IsValidationEmailSent = 1` with
  no email actually delivered — reset the flag (`IsValidationEmailSent = 0`, `ValidationEmailSentAt/
  To = NULL`) for the affected rows and the finalizer resends within a cycle.

## 📅 2026-07-13 — Complete the submit flow inline: PDF + validation email on instant Valid

> Follow-up to the status-sync fixes below: an invoice that LHDN validates during submit now finishes
> the whole flow (Valid → QR LongId → PDF → email to Supplier/Buyer/BCC) in the same request instead
> of waiting up to 10 minutes for a background finalizer cycle. No schema/migration change.

- **New shared `IInvoiceFinalizer` / `InvoiceFinalizer` service** — the single per-invoice
  PDF-generation + validation-email implementation. The email send is guarded by an **atomic claim**
  (`UPDATE … WHERE IsValidationEmailSent = 0`), so concurrent finalizers can never double-send; a
  failed send releases the claim for retry. Flag writes use `ExecuteUpdate` (no rowversion friction).
- **Submit flow (`InvoiceLists`)**: after the post-submit status sync, a `Valid` result triggers the
  finalizer inline (best-effort — a failure never affects the submission itself).
- **Deduplicated three copy-pasted finalizer loops** — `InvoiceStatusUpdater.RunFinalizerAsync`,
  the hosted `InvoiceFinalizerService` sweep (10-min grace), and `InvoiceSyncHelper.RunFinalizerAsync`
  (manual/job trigger) now all select candidates and delegate to the shared service. None of them had
  a duplicate-send guard before. Removed the unused `RunManuallyAsync` and dead `InvoiceSyncResult`.
- Side effect worth noting: the `InvoiceStatusUpdater` copy used to re-convert the (already
  Malaysia-time) `IssueDate`/`DateTimeValidated` from UTC for the email body (+8 h skew); the shared
  implementation passes the stored values as-is, matching the other two loops.

## 📅 2026-07-13 — Status-sync fixes: half-synced "instant Valid" + poller 429/starvation

> Root-caused from a live 3-invoice submit where one invoice went Valid instantly but sat without its
> QR/PDF/email for 80 minutes, and two stayed "Submitted" (reconciled only when the app next ran —
> locally the background poller stops with the F5 session). No schema/migration change.

- **Post-submit sync now persists the full validation result.** The inline poll after a submit copied
  only `status` into the DB, discarding the `longId` and `dateTimeValidated` LHDN returned in the same
  response. An instantly-validated invoice was therefore "Valid" with no QR LongId, and the PDF/email
  finalizer (which requires `DateTimeValidated`) stayed blocked until a later full sync. The
  `ExecuteUpdate` now also writes `LongId`/`DateTimeValidated`, null-coalesced against the current
  column so missing values never overwrite a concurrent sync's write.
- **`InvoiceStatusUpdater` honours the 429 batch-abort contract.** `GetDocumentDetailsWithRetryAsync`
  re-throws 429 (after its own penalty waits) expecting the batch to stop, but the poller caught it as
  a generic per-invoice error and kept calling the rate-limited API. A persisted 429 now aborts the
  poll cycle; the queue retries next cycle.
- **Queue fairness.** An invoice whose document details aren't available yet (e.g. 404 on a
  just-submitted document) now gets its `LastUpdated` bumped, so it moves to the back of the sync
  queue instead of permanently occupying a front slot of every 10-item batch and starving newer ones.
- **Clean shutdown:** a cancellation mid-batch stops the loop quietly instead of being logged as a
  sync error.

## 📅 2026-07-13 — Submit-to-LHDN hotfixes (found in local F5 testing)

> Three defects in the draft → LHDN submission path (single row + bulk), each masking the next. No
> schema/migration change.

- **False `DbUpdateConcurrencyException`.** The atomic submission claim bumps `InvoiceHeader.RowVersion`
  via raw SQL; the code then updated the pre-loaded tracked entity, whose stale rowversion made the write
  match 0 rows. Now the submission result / status-sync / failure state are written with **`ExecuteUpdate`**
  (direct UPDATE, no change-tracker / rowversion check) — the claim already guarantees a single writer.
- **`FK_InvoiceHeaders_Statuses_InternalStatusId` violation.** The submit-failure path writes
  `InternalStatusId = "TransmissionError"`, but that status was **never seeded** (missing from `HasData`) —
  the old stale-rowversion UPDATE matched 0 rows so the FK was never exercised. `DataSeeder` now ensures the
  full status set (incl. `TransmissionError`) exists idempotently on every startup — self-healing on existing
  databases, no migration. (+ integration regression test.)
- **Post-submit failure downgraded a submitted invoice.** A failure *after* the invoice was submitted
  (status poll / file move) fell into the failure catch, wrongly flipping it to `TransmissionError` and
  queuing a duplicate resubmit. Post-submission steps are now best-effort (log-and-continue); the background
  poller reconciles the LHDN status.
- **Access-token resilience:** submit re-acquires a lost session token via `ITokenService` instead of
  forcing a re-login (e.g. after an F5 app restart).

## 📅 2026-07-12 — v1.10.0 (Bulk cancel/reject hardening)

> Defense-in-depth polish for the existing bulk **Cancel** and **Request-Reject** actions. All three
> bulk actions (delete/cancel/reject) were already ownership-guarded server-side and not cross-tenant
> exploitable; this removes remaining smells. No schema/migration change.

- **Cancel (`OnPutCancelDocumentAsync`):** resolve the issuer TIN from the document **server-side**
  (the document's supplier TIN) instead of trusting the frontend-supplied `tin`. The IDOR guard already
  proved ownership, so behaviour is unchanged for legitimate users — the client value can no longer
  influence the LHDN call. (Reject already ignored the frontend TIN.)
- **`cancel-invoice.js` / `request-rejection.js`:** URL-encode `documentId`, reason and `tin`
  (`encodeURIComponent`) so reasons/UUIDs with spaces or special characters can't corrupt the request;
  and send the **effective reason** — when "Others" is chosen, the free-text detail is now actually
  transmitted (and persisted) instead of the useless "Others" category.

## 📅 2026-07-12 — v1.10.0 (Forest Tech Precision reskin + bulk Submit-to-LHDN)

> Applying the approved **Forest Tech Precision** green design language (already on the auth pages) to
> the Supplier **Dashboard** and the **Invoice list**, and adding the bulk action the list was missing.
> No schema/migration change.

### Supplier Dashboard (`Pages/Dashboard/Dashboard.cshtml`) — presentational only
- Reskinned from the hardcoded **indigo** palette (`#4f46e5` / `#4b49ac` / `#eef2ff`) to Forest Tech
  green (`#0c5434` / `#e9f5ee`), aligned to the app brand (`--tblr-primary #3AA564`). CSS-only — no
  markup, chart, data, handler, role-gating or JS change. Admin/Supplier/Buyer variants untouched.

### Invoice list (`Pages/Invoices/InvoiceLists.cshtml`) — bulk Submit-to-LHDN
- New **Submit to LHDN** bulk action on the Draft tab (the list already had bulk Delete/Cancel/Reject).
  Hidden until rows are selected; confirms, then submits each selected **Draft** one request at a time.
- Server: extracted the fully-guarded single-submit logic into a shared **`SubmitDraftCoreAsync`**
  (IDOR + per-TIN ownership, **atomic double-submit claim / payload-hash idempotency**, status sync, and
  on failure `TransmissionError` + background retry). `OnPostSubmitFromListAsync` now delegates to it
  (single-row UX unchanged); new `OnPostBulkSubmitOneAsync` JSON handler reuses the same core — **no
  duplication of the LHDN critical path**.
- Client `wwwroot/js/bulk-submit-invoice.js` mirrors `delete-invoice.js`: a failure never aborts the
  batch (server queues a retry) and the user gets an aggregated per-invoice summary. Drafts-only;
  authorisation + idempotency enforced server-side; anti-forgery token required.
- Forest Tech “Quick Tip” banner on the Draft tab explaining bulk submit.

## 📅 2026-07-10 — v1.10.0 (Tabler UI migration — ALL authenticated pages migrated; deployed & QA'd on staging)

> Replacing the Velzon admin theme with the free MIT **Tabler** Bootstrap 5 template on the
> **authenticated** UI. Server-rendered Razor Pages throughout — **no** SPA framework. **No backend / DB /
> LHDN / calculation / PDF / authorization change** in any phase — layout chrome only. As of 2026-07-11
> the build is **deployed to staging and Playwright-verified across all three roles** (Supplier/Buyer/
> Admin); every authenticated page now renders Tabler (see the post-deploy QA section below). Velzon
> `_Layout`/`_LoginLayout` are retained as the fallback until Phase 8. Full plan, rollback and test
> strategy in `docs/TABLER-MIGRATION-AUDIT.md`.

### Assets & foundation
- Self-hosted **Tabler v1.4.0** (MIT) under `wwwroot/tabler/` (`css/tabler.min.css`, `js/tabler.min.js`) —
  no CDN, offline/IIS-friendly. Tabler's own JS is intentionally **not** loaded (avoids a double-Bootstrap
  load); interactivity is standard Bootstrap 5 from the existing bundle.
- `wwwroot/tabler/css/einvworld-tokens.css` — brand green mapped onto Tabler CSS vars + **compatibility
  shims** so existing Velzon utility classes (`page-title-box`, `btn-soft-*`, `material-shadow`,
  `avatar-title`, and the auth chrome classes) render correctly under Tabler **without per-page markup
  rewrites**.
- `wwwroot/tabler/js/einvworld-ui.js` — current-route highlighting (`aria-current` + opens the active
  dropdown) + an `einvworld.toast()` helper (vanilla JS).
- Decomposed shared partials: `_LayoutTabler`, `_TablerSidebar`, `_TablerTopbar`, `_UserMenu`,
  `_AdminNavigation` / `_SupplierNavigation` / `_BuyerNavigation`, `_Footer`, `_PageHeader`.
  Plus `_LoginLayoutTabler` for the auth area. The full functional-plugin stack (jQuery, Bootstrap bundle,
  Select2, Flatpickr, SweetAlert2, Toastr, Chart.js, TinyMCE, lord-icon) and idle-timeout + app-search
  are preserved.

### Rollout (opt-in via per-folder `_ViewStart.cshtml`, authenticated users only)
- **Pilot:** `Pages/Items/Index`.
- **Low-risk folders:** Items, Suppliers, PublicCustomer, Lead, Profile, RecurringInvoices.
- **Admin** area (40 pages incl. nested Codes/*).
- **Dashboard + Invoices** (money path). **PDF/print templates untouched** — `PdfTemplate.cshtml` and
  `InvoiceDetails.cshtml` keep `Layout = null`.
- **Templates + Assistant** (the last 2 orphans found in the consistency audit — they had fallen through
  to the Velzon default). After these, **every authenticated folder is on Tabler.**
- **Auth** (login / 2FA / register / password reset / Manage/*) → `_LoginLayoutTabler`, preserving
  Turnstile, the auth form ids, password/validation init, and the logout-reason toast.
- Public/anonymous pages (marketing, `Lead/Submit`, `Lead/Create` for anonymous) stay on the marketing
  layout (the folder `_ViewStart` is authenticated-only).
- **Revert** any area by deleting its `_ViewStart.cshtml` (or restoring one line in
  `Areas/Identity/Pages/_ViewStart.cshtml` for auth). Velzon `_Layout`/`_LoginLayout` are kept as the
  fallback.

### Invoice list UX
- `Pages/Invoices/InvoiceLists.cshtml`: bulk Delete/Cancel/Reject buttons are now **hidden until ≥1 row is
  selected**, and the header "select all" checkbox is now functional (it previously had no handler).
  Presentational only — bulk handlers still read `.invoice-checkbox:checked`.

### QA harness
- `tests/playwright/10-tabler-modules.spec.js` — authenticated verification across every module per role
  (Tabler shell present, no app console/network errors, no unusable horizontal overflow at
  375/768/1366/1920). Logs in **once per role** and reuses the session cookie (fast + reliable), ignores
  third-party analytics noise + `ERR_ABORTED`, and skips a role whose login is 2FA-gated. Requires the
  Tabler build deployed + Cloudflare Turnstile **test** keys (and `Security__EnforceAdminMfa=false` for the
  admin arm) to run.

### Post-deploy staging QA + fixes (2026-07-11)
- **Brand logo oversize** — the source PNG (~1080×723) had no CSS height cap, so Tabler sized it to the
  brand-container width (240×161 desktop, full-width mobile). Pinned to 2.25rem in `einvworld-tokens.css`.
- **Long `<code>`/token/`<pre>` overflow** on narrow screens (AI Settings env-vars, import instructions,
  Webhooks) — Velzon wrapped these, Tabler didn't; restored `overflow-wrap`/`word-break`/`pre` scroll.
- **Invoice list mobile** — the 12-column table showed only Invoice No + UUID; it now prioritises
  e-Invoice No / Buyer / Total / LHDN Status / Action `< md` via the existing `col-*` classes. Desktop and
  the "Customize" column feature are unchanged.
- **Verified**: colour utilities (`bg-primary`/`danger`/`success`/`warning`) render; **all Admin pages
  display text correctly** (no invisible/low-contrast text — an automated flag on the dark sidebar was a
  false positive).
- **Two PRE-EXISTING app bugs surfaced (NOT Tabler; left for a separate PageModel/data fix):** company
  logos emitted as `file:///E:/…png` paths (Suppliers/Index → browser-blocked) and missing resource
  images (404 on Manage Resources).
- **Known residual:** AI Settings has a small mobile overflow to refine.

### Deferred (Phase 8 — must follow a fully-green re-verification)
- **Phase 8 — Velzon removal**: delete the Velzon `_Layout`/`_LoginLayout`/`app.min.css`/theme JS, retire
  the DB-backed global-theme system (`/api/Theme/*` + `GlobalThemeService` + `GlobalThemeSettings`,
  additively — leave the table), and remove ~60 MB of unused Velzon demo assets. Deliberately held: do not
  remove the working fallback theme before the Tabler UI is validated end-to-end on staging.

## 📅 2026-07-10 — v1.9.9 (Fix: validated/rejected/cancelled emails silently failed when SMTP creds blank)

> Found in live staging QA: after a submission reached **Valid**, the Supplier/Buyer notification
> email failed with `System.ArgumentException: The value cannot be an empty string (Parameter
> 'address')` and was swallowed — no email sent.

### Root cause
- `SendEmailWithBcc` read the SMTP settings with **null-only** guards (`?? throw`). SMTP credentials
  are intentionally server-specific and ship **blank** in `appsettings.json` (set per server via
  `EmailConfiguration__Default__SmtpUsername` / `__SmtpPassword`). On a server where they are not set,
  `SmtpUsername` is an **empty string** (not null), so the guard didn't fire and `new MailAddress(smtpUser, …)`
  threw the cryptic empty-address error — masking the real cause and dropping every outbound email.

### Fixed
- SMTP server/username/password are now validated with `IsNullOrWhiteSpace` and throw a clear,
  actionable message (`SMTP not configured: EmailConfiguration:Default:SmtpUsername is empty …`) instead
  of the misleading `MailAddress` exception. (Recipient addresses were already validated correctly.)

### Required operator action (this is what actually makes emails send)
- Set the SMTP credentials on the server that runs EINVWORLD (they are **not** in the repo):
  `EmailConfiguration__Default__SmtpUsername` and `EmailConfiguration__Default__SmtpPassword`
  (see SECRETS-SETUP.md), then recycle the app pool. Until then, notifications correctly no-op with a
  clear log line instead of a crash.

## 📅 2026-07-10 — v1.9.8 (Security: harden invoice export — company scoping + CSV injection)

> Found during a full Supplier/Buyer flow verification pass.

### Fixed (security)
- **Cross-company export leak (IDOR).** `InvoiceLists.OnGetExportAsync` only applied the
  per-TIN company scope inside `if (invoiceDirection != "All")`. A Supplier/Admin calling the
  export endpoint with `invoiceDirection=All` (or blank) skipped the filter entirely and exported
  **every company's invoices**. The TIN scope is now mandatory for the All/empty case too. Buyers
  were already force-pinned to `Received` and were not affected.
- **CSV formula injection.** `EscapeCsv` quoted delimiters but did not neutralise cells beginning
  with `= + - @` / tab / CR, so a crafted item description or company name could execute as a
  formula in Excel/Sheets. Such cells now get a leading apostrophe. XLSX export was not affected
  (ClosedXML writes literal string cells, which Excel does not execute).

## 📅 2026-07-10 — v1.9.7 (Critical fix: LHDN submissions accepted but never persisted locally)

> Since v1.8.2 added optimistic concurrency (`InvoiceHeader.RowVersion`), **every UI submission
> failed to save locally**: `InvoiceSubmissionGuard.TryClaimAsync`'s raw SQL claim UPDATE bumps the
> row's rowversion, so the tracked entity loaded before the claim held a stale token and the
> post-submission `SaveChangesAsync` (UUID / SubmissionID / status) always threw
> `DbUpdateConcurrencyException`. LHDN **accepted** the documents, but locally they stayed Drafts —
> an active duplicate-submission risk once the 10-min payload dedup and 5-min claim windows expired.
> All six submit paths were affected (CreateInvoice, InvoiceEdit, InvoiceLists, CreateSBI, CreateCN,
> CreateSBCN); the recurring-invoice background path was not.

### Fixed
- `InvoiceSubmissionGuard.TryClaimAsync` now reloads any tracked `InvoiceHeader` for the invoice
  after winning the claim, refreshing its concurrency token so the post-submission save succeeds.
  Fixes all six callers centrally; documented that mutations must happen after the claim.

### Tests
- New SQL Server integration tests (real rowversion semantics): load-tracked → claim → mutate →
  save succeeds and persists; and a claim loser with a tracked entity stays blocked (the reload
  does not weaken the guard). The in-memory provider cannot catch this class of bug.

### Operator action (staging/production data fix)
- Run `scripts/Reconcile-OrphanedSubmissions.sql` **after deploying this build**: it lists invoices
  claimed-but-UUID-less, backfills UUID/SubmissionID/status for submissions confirmed at LHDN
  (idempotent, `UUID IS NULL`-guarded, writes an `InvoiceHistories` audit row), and clears stale
  claims for drafts never accepted at LHDN. Known staging orphans: EINV100360, EINV100361.

### Known follow-up (not in this release)
- `SyncJobTracker` shares the request's `DbContext`, so an unrelated dirty entity can poison its
  save and make the "retry has been queued" message false. Harden it in a separate, scoped PR.

## 📅 2026-07-09 — v1.9.6 (Speed: exempt Turnstile from Cloudflare Rocket Loader; operator action to disable it)

> Live QA against staging found every page's `DOMContentLoaded` delayed to **~21 seconds** (HTML TTFB
> is ~0.3 s). Root cause: **Cloudflare Rocket Loader is enabled on the zone** — it rewrites every
> `<script>` to a deferred type (including **Turnstile's api.js**, a documented incompatibility) and
> re-executes them itself, holding back the whole page lifecycle. Production is affected identically.

### Fixed (code)
- Added `data-cfasync="false"` to the four Turnstile `api.js` script tags (`_Layout`, `_HomeLayout`,
  `_LoginLayout`, `Contact`) so Rocket Loader can never capture the bot-protection script — per
  Cloudflare's own guidance. This restores reliable Turnstile token issuance regardless of the zone
  setting.

### Required operator action (Cloudflare dashboard — cannot be fixed from code)
- **Disable Rocket Loader** for the `einvworld.com` zone: *Speed → Optimization → Rocket Loader → Off*.
  The app already self-hosts and optimizes all assets (v1.9.1/v1.9.2), so Rocket Loader adds nothing and
  costs ~20 s of page-lifecycle delay on every page, breaks `DOMContentLoaded`-dependent code, and
  degrades Turnstile. **Done 2026-07-09** — re-measurement then exposed the second cause below.

### Fixed (code) — second cause found after Rocket Loader was disabled
- With Rocket Loader off, `DOMContentLoaded` was *still* 21–26 s on networks that black-hole analytics
  hosts (ad-block DNS / strict firewalls): the hanging `gtm.js` fetch — plus the Cloudflare Insights
  beacon the GTM container itself loads — stalled the lifecycle until the ~24 s connection timeout.
  Empirically verified: with those two hosts blocked at the network layer, the same pages complete
  `DOMContentLoaded` in 0.2–3.3 s. **`_GoogleAnalytics.cshtml` now injects GTM only after
  `DOMContentLoaded`**, so analytics can never gate the page lifecycle; on healthy networks GTM still
  loads immediately after DCL with full dataLayer timing.

## 📅 2026-07-09 — v1.9.5 (SVDP 1.2 support — Special Voluntary Disclosure Programme)

> LHDN SDK 8 Jul 2026 introduced document versions for the e-Invoice Special Voluntary Disclosure
> Programme (valid until 31 Dec 2027): SVDP **1.2** (unsigned) and SVDP **1.3** (signed). The official
> sample confirms the 1.2 payload is byte-identical to v1.0 except `InvoiceTypeCode/@listVersionID`.
> Business decision: adopt **1.2** (1.3 additionally needs the signing pipeline + certificate, still off).

### Added
- **`InvoiceHeader.IsSvdp`** flag (additive migration `20260709120000_AddSvdpFlagToInvoiceHeader`,
  4 artifacts incl. idempotent `Apply_*.sql`; existing rows default to normal invoices).
- **"SVDP e-Invoice" switch** on Invoice Create and Edit (per-invoice, off by default), shown only when
  `LHDNApiConfig:SvdpEnabled` is `true` — set it `false` to retire the option when the programme ends.
- **Mapper**: an SVDP-flagged invoice is submitted with `listVersionID = "1.2"`; everything else —
  validation, totals, idempotency, signing-off behaviour — is unchanged. Normal invoices still emit `1.0`
  (regression-tested).

### Not included (by design)
- SVDP **1.3** (needs the digital-signature pipeline: `SigningEnabled` + a purchased cert).
- SVDP flag is **not copied** to credit/debit notes created from an SVDP invoice, recurring invoices, or
  templates — a disclosure is a deliberate one-off choice each time.

## 📅 2026-07-09 — v1.9.4 (Daily LHDN code-table sync from the official SDK files)

> Until now the nine LHDN code tables (unit types, currencies, countries, states, tax types, payment
> modes, classification, MSIC, e-invoice types) were kept current by manually copying JSON from the SDK
> portal whenever a release note announced a change (e.g. CNH currency, Hectare/GT units, country
> renames). That process is now automatic.

### Added
- **`CodeTableSyncWorker`** background service: once a day (config `CodeTableSync`, ON by default)
  it downloads the official machine-readable files from
  `https://sdk.myinvois.hasil.gov.my/files/<Table>.json` and upserts them into the database —
  the **database remains the source of truth** the app reads.
  - **Additive-only policy:** new codes are inserted (active, `UpdatedBy = "sdk-sync"`); renamed
    descriptions are updated (the SDK is authoritative for wording); rows are **never deleted or
    deactivated**, and an admin's `IsActive` choice is preserved — a truncated/bad download can never
    remove reference data. Empty/implausibly small downloads are skipped with a warning.
  - Each table syncs independently (one failing file doesn't stop the rest); the run logs a
    per-table `+added/~updated` summary. Nine small GETs/day against the public static host —
    completely separate from the LHDN API client and its rate limiter.
- SQL Server integration test (`CodeTableSyncTests`) with stubbed HTTP proving the policy against a
  real database: insert, rename-update, IsActive preservation, quirky JSON keys
  (`"Payment Method"`, `"MSIC Category Reference"`), and never-delete.

### Config
- New `CodeTableSync` section: `Enabled` (default `true`), `BaseUrl`, `IntervalHours` (default 24),
  `StartupDelayMinutes` (default 5).

## 📅 2026-07-09 — v1.9.3 (LHDN SDK compliance: exchange rate, State 17, GT unit, TIN log masking)

> Result of a full LHDN MyInvois SDK release-note audit (Feb 2024 beta → 8 Jul 2026). Most rules were
> already compliant (YYYY-MM-DD dates, decimal amounts — no scientific notation, Dec-2025 field lengths
> via `UpdateSDKDec2025`, no state code 00, CNH currency, rate-limit pacing, search pagination). Four
> gaps are closed here. **SVDP document versions 1.2/1.3 (SDK 8 Jul 2026, opt-in voluntary-disclosure
> programme valid to 31 Dec 2027) are deliberately deferred pending a business decision.**

### Fixed (LHDN compliance)
- **Currency Exchange Rate enforced for non-MYR invoices** (LHDN rejects missing rates since
  1 Sep 2025). Previously a non-MYR invoice with no rate silently submitted `CalculationRate = 1` —
  wrong tax data that LHDN would *accept*. `InvoiceMapper` now fails validation with a clear message
  before submission; MYR payloads are byte-for-byte unchanged. All seven submission paths (Create
  Invoice/CN/SBI/SBCN, Edit, CSV import, recurring worker) flow through this choke point.
- **State Code 17 ("Not Applicable") restricted** per the SDK rule effective 30 Apr 2026: rejected for
  any party with country `MYS` unless the TIN is an LHDN general TIN (consolidated general public,
  foreign buyer/supplier, government). Foreign parties (e.g. self-billed imports) are unaffected.

### Added
- **Unit code `GT` (gross ton)** — SDK addition of 28 Dec 2024 — seeded via data-only idempotent
  migration `20260709000000_AddGrossTonUnitType` (4 artifacts incl. `Apply_AddGrossTonUnitType.sql`;
  inserts only if absent, `Down` removes only its own row). Reference copy `wwwroot/codes/UnitTypes.json`
  updated too.
- `Helpers/LogSanitizer.cs` — masks TIN/BRN/NRIC values for logging (first 4 + last 2 kept; LHDN
  general TINs stay readable since they are public constants).

### Fixed (security / PII logging)
- **TINs are no longer logged in plaintext.** All `{TIN}` structured-log sites across
  `LHDNApiService`, `TokenService`, `TokenRenewalService`, sync helpers, `EInvoicingController`
  (including BRN/NRIC `idValue`), invoice pages and the lead form now log masked values.

### Tests
- Mapper: non-MYR without/with/zero exchange rate, MYR regression (rate-1 payload unchanged),
  State 17 domestic-blocked / foreign-allowed / general-TIN-allowed. Helpers: `LogSanitizer` masking.

## 📅 2026-07-09 — v1.9.2 (Self-host the remaining page-level CDN assets)

> Follow-up to v1.9.1: that pass localized the shared layouts, but eight individual pages still loaded
> their own libraries from public CDNs. Now fully first-party.

### Changed
- **Repointed to existing local copies:** Chart.js (Dashboard, MainDashboard), jQuery + Select2
  (PublicCustomer/Create, Suppliers/Create — the shared layout already provides them locally).
- **Downloaded & self-hosted (FOSS):** chartjs-plugin-zoom (Dashboard), html2pdf (InvoiceDetails2),
  jsPDF + html2canvas (PdfTemplate — a `Layout=null` page, so these are essential there), and qrcodejs
  (the 2FA authenticator-setup pages) — under `wwwroot/assets/libs/…`.
- **Removed** the redundant remixicon CDN `<link>` on the Dashboard; the local `icons.min.css` already
  bundles remixicon (verified the dashboard's `ri-*` glyphs are present).

### Result
No app page loads front-end assets from a CDN anymore. Only Cloudflare Turnstile and the optional Google
Tag Manager snippet remain external (plus the Contact-page Google Map). Verified by a Playwright test that
asserts the authenticated Dashboard / Create / 2FA-setup pages request zero external asset hosts.

## 📅 2026-07-09 — v1.9.1 (Self-host all front-end assets; kill the CDN dependency)

> The UI loaded ~25 libraries and all its web fonts from public CDNs (jsDelivr, cdnjs, code.jquery.com,
> cdn.tiny.cloud, Google Fonts) at runtime. On an on-prem / air-gapped-capable government e-invoicing
> platform that is an availability, privacy and FOSS-policy problem — and it was measurably harmful:
> when a CDN was unreachable the page's `load` event (and the theme preloader that waits on it) stalled
> ~20–30s, freezing the UI behind a spinner. All first-party assets are now served locally.

### Changed — everything self-hosted
- **Repointed to the theme's already-bundled local copies:** jQuery, SweetAlert2, Flatpickr, Chart.js,
  jQuery-Validation (+unobtrusive). These were shipped in `wwwroot/assets/libs` but loaded from CDN anyway.
- **Downloaded & self-hosted (FOSS):** Select2 (+bootstrap-5 theme), Toastr, Font Awesome 6.5.0
  (CSS + webfonts), Toastify, and Flatpickr CSS — under `wwwroot/assets/libs/…`.
- **Google Fonts localized:** the 11 `@import`s in `app.min.css` (loaded on every page) were replaced
  with a single self-hosted stylesheet + 31 latin/latin-ext `woff2` files under `assets/fonts/google/`.
- **TinyMCE:** the editor JS was already self-hosted; repointed its skin CSS from `cdn.tiny.cloud`
  (which also carried a cloud API key) to the local `assets/js/tinymce/skins/…`.
- Only **Cloudflare Turnstile** (bot widget, must load from Cloudflare) and the optional **Google Tag
  Manager** analytics snippet remain external; the Contact-page Google Map still loads Google resources.

### Removed — dead CDN loads
- DataTables (core + bs5 + responsive + buttons ×3 + 2 CSS), Inputmask, jszip and pdfmake were all loaded
  from CDN but **never used** by any app page (the app renders its own server-side lists). Removed, along
  with the theme's `datatables.init.js` (which only wired up demo tables).

### Fixed
- **Preloader can no longer freeze the UI.** The theme faded the spinner out on `window.load`; if any
  resource was slow that never fired. It now hides on `DOMContentLoaded` with a hard 3s cap.

## 📅 2026-07-09 — v1.9.0 (Security: Admin area role-gated by folder convention)

> Found by automated Playwright authorization QA across Admin/Supplier/Buyer. **30 pages under
> `/Admin` shipped without their per-page `[Authorize(Roles="Admin")]` attribute** — every master-data
> Codes page (tax types, currency, MSIC, classification, unit/payment/state/country) plus all
> Notifications and Resources Create/Types pages. Any authenticated Supplier or Buyer could view and
> mutate the reference data that drives LHDN invoice generation and tax calculation. This is a
> broken-access-control / privilege-escalation defect.

### Fixed (security)
- Added an `AdminOnly` authorization policy (`RequireRole("Admin")`) and applied it with
  `Conventions.AuthorizeFolder("/Admin", "AdminOnly")` in `Program.cs`. The whole `/Admin` folder is now
  Admin-only **by default**, so a new admin page can no longer ship unprotected by forgetting an
  attribute. Composes with the existing `RequireAuthenticatedUser` fallback and the per-page attributes
  already present — no behaviour change for legitimate Admins (verified: Admin still reaches all pages;
  Supplier/Buyer now blocked to AccessDenied/login).

### Fixed (startup on a fresh database)
- `WebsiteDbContext` (owns `ResourceTypes`/`ResourceItems`) is now migrated at startup alongside the
  primary context, so `/Admin/Resources/Manage` no longer 500s with *"Invalid object name 'ResourceTypes'"*
  on a fresh DB. Additive only — no destructive migration.

## 📅 2026-07-09 — v1.8.9 (UI/UX fixes found by full-site browser QA)

> Found by automated Playwright QA of public pages, the three role dashboards, navigation, and
> responsive breakpoints (375 / 768 / 1440 px).

### Fixed
- **Dead menu links** in the user dropdown (`_Sidebar.cshtml`): removed the template placeholders
  *Help* → `pages-faqs.html`, *Settings* → `pages-profile-settings.html`, *Lock screen* →
  `auth-lockscreen-basic.html` (all 404). Fixed the non-authenticated *Login* link from `login.html`
  → `/login`. Profile and Logout are unchanged.
- **Responsive tables**: a global `.table-responsive { overflow: visible !important }` override (added
  so in-table dropdowns wouldn't clip) disabled Bootstrap's horizontal scroll everywhere, so wide tables
  pushed the whole page sideways on mobile/tablet (e.g. dashboard overflowed 461 px at 375 px wide).
  Restored `overflow-x: auto` below the `992 px` breakpoint; desktop dropdown behaviour preserved.
- **Full-bleed banner overflow**: public pages reuse `mx-n4` banners designed for the authenticated
  layout's padded `.page-content`; on `_HomeLayout` they spilled ~24 px past the viewport at every width.
  Wrapped `@RenderBody()` in an `overflow-x: clip` container.
- **Dashboard filter bar**: `#filterForm` now `flex-wrap`s so it no longer overflows 19 px at mobile width.
- **Login/Register footer**: removed `white-space: nowrap` on the long company-credit link so it wraps
  instead of overflowing 55 px at 375 px.
- **Identity validation scripts**: `_ValidationScriptsPartial.cshtml` referenced non-existent
  `~/libs/...` paths (404), so unobtrusive client validation was dead on login/register/manage pages.
  Now loads jquery-validate from cdnjs, matching the shared partial.

### Added
- A Playwright QA harness under `tests/playwright/` covering: public pages; Supplier/Buyer login-logout
  plus an Admin **2FA-enforcement** check (correct password is challenged for a second factor, not let
  straight in); authorization/role-isolation across `/Admin`; per-role navigation crawl; responsive
  overflow at 375/768/1440 px; and a full **Items CRUD lifecycle** (create → list → edit → delete,
  self-cleaning QA data). Plus `playwright.config.js` and npm `qa`/`qa-headed`/`qa-report` scripts.
  Navigation waits use `domcontentloaded` so a blocked third-party analytics host can't stall page loads.

## 📅 2026-07-08 — v1.8.8 (Remove dead showToast call on the home page)

> Found by authenticated browser QA. `Home/Index.cshtml` ran an "Example" `$(document).ready`
> block calling `showToast(...)`, a function that is defined nowhere (the app uses NToastNotify),
> so every home-page load threw `Uncaught ReferenceError: showToast is not defined`.

### Removed
- The dead example `showToast` block on the home page. It never worked (no such function); removing
  it clears the console error. No feature lost.

## 📅 2026-07-08 — v1.8.7 (CSP: allow the Contact-page Google Map frame)

> Found by browser QA of the public pages. The Contact page embeds a Google Maps iframe, but the
> Content-Security-Policy `frame-src` only allowed Cloudflare Turnstile — a report-only violation today,
> but the map would break the moment CSP is promoted to enforcing. This is exactly what the report-only
> phase exists to surface.

### Changed
- `frame-src` now includes `https://www.google.com` alongside `https://challenges.cloudflare.com`, so
  the map renders under an enforcing CSP. No other directive changed; still report-only for now.

## 📅 2026-07-08 — v1.8.6 (Validated-invoice email reaches public customers)

> Found while verifying the submit → status → PDF → email pipeline. The validated-invoice email
> (with the QR-coded PDF) only considered a registered `Customer`/`Supplier` (PartyInfo). For a
> **public/one-off customer** (`PublicCustomer`, no PartyInfo) the buyer email was silently skipped —
> confirmed on staging: 4 Valid public-customer invoices whose buyer never got the email. Also, the
> manual-sync finalizer query loaded no navigation properties at all, so its emails had no recipients
> beyond the BCC.

### Fixed
- `SendValidatedNotificationEmail` takes an optional `PublicCustomer?` and falls back to it for the
  buyer email/name when there is no registered `Customer`. All three finalizer callers
  (`InvoiceStatusUpdater`, `InvoiceFinalizerService`, `InvoiceSyncHelper`) pass `invoice.PublicCustomer`
  and now `.Include(i => i.PublicCustomer)`; the manual-sync query also gained the missing
  `.Include(Customer)`/`.Include(Supplier)`. Registered-customer behaviour is unchanged.

> Note: three separate finalizer paths still duplicate the PDF-generate + email logic — a
> consolidation candidate for a future PR, tracked here so it isn't forgotten.

## 📅 2026-07-08 — v1.8.5 (Quiet benign LHDN 404s in sync logs)

> Found by reviewing D:\EINVWORLD\Logs. The background status sync logs an LHDN document-details 404
> as `[ERR]` with a full stack trace, and re-polls the same not-on-LHDN invoices every cycle — 33 of the
> 77 total errors across the retained logs were this one benign case (document not yet submitted, or a
> stale/placeholder UUID), drowning real errors. Rate-limit (429) handling was already correct and is
> unchanged.

### Changed
- `InvoiceSyncHelper` (both details-polling catch blocks) now treats an LHDN 404 as a clean `[WRN]`
  ("document not found on LHDN; skipping") instead of an `[ERR]` + stack trace. Sync behaviour is
  unchanged (the invoice is simply skipped, as before); only the log noise is removed. Genuine
  non-404 failures still log as errors.

## 📅 2026-07-08 — v1.8.4 (Fix duplicate JS const on auth pages)

> Found by staging browser QA (authenticated). For a signed-in user, `_LoginLayout.cshtml` declared
> `const idleTimeoutMinutes` twice at page scope — once inside the `@if (SignInManager.IsSignedIn)`
> idle-timeout block and again in an orphaned `<script>` lower down — so the browser threw
> `SyntaxError: Identifier 'idleTimeoutMinutes' has already been declared`, aborting the scripts after
> it (the logout-reason toastr). Anonymous pages were unaffected (only the orphan ran).

### Fixed
- Removed the redundant/orphaned `const idleTimeoutMinutes` declaration and hoisted its `@inject
  SessionOptions` to the top of `_LoginLayout.cshtml`. One declaration remains (the idle-timeout timer).

## 📅 2026-07-08 — v1.8.3 (Fix dead client-side validation scripts)

> Found by staging browser QA. `_ValidationScriptsPartial.cshtml` referenced `~/libs/jquery-validation/...`
> files that never existed in `wwwroot` (no libman restore is wired into the build), so client-side
> unobtrusive validation was silently dead on every page using the partial — the script requests 404'd
> into the auth login redirect ("Refused to execute script" console errors). Server-side validation was
> always enforcing, so no data-integrity impact; UX-only.

### Fixed
- `_ValidationScriptsPartial.cshtml` now loads `jquery-validate` 1.19.5 + `jquery-validation-unobtrusive`
  3.2.12 from cdnjs — consistent with `_Layout.cshtml`, which already CDN-loads jQuery itself.

## 📅 2026-07-08 — v1.8.2 (InvoiceHeader optimistic concurrency)

> Closes the long-deferred backlog item: concurrent writers to the same invoice (background status sync
> vs a user cancel/edit) previously raced last-writer-wins — a stale "Valid" sync could silently bury a
> user's "Cancelled". Additive schema (one rowversion column); no behaviour change on the happy path.

### Added
- **`InvoiceHeader.RowVersion`** (`[Timestamp]` SQL Server `rowversion`) + **migration
  `20260708000000_AddInvoiceHeaderRowVersion`** (4 artifacts incl. idempotent `Apply_*.sql`). Any
  conflicting `SaveChanges` now throws `DbUpdateConcurrencyException` instead of silently overwriting.
- **Conflict policies at the real race sites** (everywhere else stays loud-by-design):
  - `InvoiceStatusUpdater` / `InvoiceStatusSyncHelper` (background sync): log a warning, reload the
    conflicting entries, skip — the next poll re-syncs from LHDN, the source of truth for status.
  - `InvoiceLists` cancel handler (user path): refresh the concurrency token, keep the cancel values,
    retry once — LHDN has already accepted the cancellation, so it must be recorded.
- **Integration test** `InvoiceHeader_RowVersion_SecondWriterConflicts_AndRetryWithFreshTokenWins`
  (real SQL Server; rowversion semantics are faked by the in-memory provider).

## 📅 2026-07-04 — v1.8.1 (Webhook config hygiene warnings)

> Small post-roadmap hardening. No schema/behaviour change.

### Added
- **`ProductionConfigValidator`** now emits startup **warnings** (never blockers) when
  `Webhooks:Enabled=true` in Production with the SSRF guard (`BlockPrivateNetworks`) or TLS requirement
  (`RequireHttps`) turned off, or with a non-positive `DeliveryTimeoutSeconds`. Surfaces an insecure
  webhook configuration as one clear line at boot instead of a silent runtime surprise.
- **`ProductionConfigValidatorWebhookTests`** — confirms the webhook checks warn but never fail startup,
  and are ignored when webhooks are disabled.

## 📅 2026-07-04 — v1.8.0 (Blueprint-gap remediation, Tier 3c: outbound webhooks)

> Adds an outbound webhook subsystem so customer ERPs can be notified over HTTP when an invoice reaches a
> terminal LHDN status (Valid / Cancelled / Rejected / Invalid) — previously status changes reached
> customers only by email. OFF by default; additive schema (new table + one nullable column); no breaking
> changes. This is the final item of the blueprint-gap roadmap.

### Added
- **`WebhookSubscription`** entity + **migration `20260704000000_AddWebhookSubscriptions`** (4 artifacts +
  idempotent `Apply_*.sql`): per-company (TIN) callback URL, HMAC signing secret (encrypted at rest via a
  dedicated DataProtection purpose), enabled flag, and last-delivery diagnostics. Also adds a nullable
  `InvoiceHeaders.WebhookNotifiedStatus` dedup marker.
- **`IWebhookDispatchService`** — scans for invoices at a terminal status not yet notified for that status
  and enqueues one durable **`SyncJobType.WebhookDelivery`** job per matching enabled subscription (matched
  by supplier or customer TIN). Runs inside the existing `InvoiceStatusUpdater` loop; fires once per status
  transition via `WebhookNotifiedStatus`.
- **`WebhookDeliveryJobHandler`** — delivers one webhook: builds the JSON payload, signs it
  (`X-EInvWorld-Signature: sha256=HMAC_SHA256(secret, rawBody)`), and POSTs via a named `IHttpClientFactory`
  client with a configurable timeout. Non-2xx / transport failure throws so the durable queue retries with
  backoff and dead-letters — visible/replayable in **Admin → Sync Jobs**.
- **`WebhookSigner`** — the HMAC-SHA256 hex signature helper (receivers verify with it).
- **SSRF mitigation** — callback URLs are validated: absolute http(s), HTTPS required by default, and
  (default) rejected if they resolve to a loopback/private/link-local address
  (`Webhooks:BlockPrivateNetworks`).
- **Admin → Webhooks** UI — register / edit / enable-disable / rotate-secret / delete / send-test. The
  signing secret is generated server-side and shown **exactly once** (on create or rotate); all mutating
  actions are audited (`WebhookSubscriptionCreated`, `WebhookSecretRotated`, `WebhookSubscription
  Enabled/Disabled/Deleted`, `WebhookTestSent`).
- **Config section `Webhooks`** (`Enabled` (default false), `DeliveryTimeoutSeconds`,
  `BlockPrivateNetworks`, `RequireHttps`) and a second DataProtection purpose
  (`eInvWorld.Secret.FieldEncryption.v1`) for the signing secret.
- **Tests** — `WebhookSignerTests` (HMAC vector, determinism, prefix, tamper-sensitivity) and real-SQL
  integration tests for the delivery handler (success marks the subscription; non-2xx throws for retry;
  disabled subscription is a no-op and never contacts the receiver).

### Delivery semantics
- **At-least-once.** A crash between enqueue and the dedup-marker commit can re-enqueue, so receivers must
  treat `invoiceNo` + `status` as an idempotency key and verify the HMAC signature before acting.

### Operational notes
- The signing secrets are encrypted with the DataProtection key-ring, so the key-ring remains a critical
  backup target (see SECRETS-SETUP.md). Rotating a secret invalidates the old one immediately — update the
  receiver in step.

## 📅 2026-07-04 — v1.7.2 (Blueprint-gap remediation, Tier 3b: field-level PII encryption)

> Encrypts the most sensitive free-text PII at rest — bank account numbers and secondary/tertiary address
> lines — transparently via the existing DataProtection key-ring. Newly created and edited records are
> encrypted automatically; existing rows are encrypted by a one-time, admin-triggered, idempotent
> backfill. Additive schema change (columns widened, no data destroyed); no breaking changes.

### Scope (deliberately narrow)
- **Encrypted:** `BankAccountNo` (InvoiceHeader, PartyInfo, PublicCustomer, InvoiceTemplate) and
  `Addr2`/`Addr3` (PartyInfo, PublicCustomer). These are free-text and are **never** used in a query
  predicate (no `WHERE`/`JOIN`/`Any`), so transparent value-converter encryption is safe.
- **NOT encrypted (by design):** `TIN` (filtered on throughout — encrypting it would break every tenant
  query), and `Addr1`/`CityName`/`StateCode`/`PostalCode` (feed reporting and PDF rendering). TIN is a
  semi-public tax identifier, not comparable in sensitivity to a bank account number.

### Added
- **`ProtectedStringConverter`** (`Services/Security/`) — an EF Core value converter that encrypts on
  write and decrypts on read via an `IDataProtector`. Reads are **lenient**: a value that cannot be
  decrypted (a legacy plaintext row not yet backfilled) is returned verbatim, so the app stays fully
  functional during a partial backfill and the backfill is safely re-runnable.
- **`PiiEncryptionBackfillService`** (`Services/Security/`) — a one-time, **idempotent** backfill that
  encrypts existing plaintext values in place using raw SQL (so it can distinguish already-encrypted rows
  from plaintext and skip them). Triggered from **Admin → System Health → "Encrypt existing PII"**; the
  outcome is written to the tamper-evident audit trail (`PiiEncryptionBackfill`).
- **Migration `20260703000000_EncryptPiiFields`** — widens the seven affected `nvarchar(150)` columns to
  `nvarchar(max)` so they can hold ciphertext (`InvoiceTemplates.BankAccountNo` was already `nvarchar(max)`).
  Purely additive; idempotent `Apply_EncryptPiiFields.sql` provided.
- **Tests** — `ProtectedStringConverterTests` (round-trip, non-deterministic ciphertext, lenient
  plaintext/garbage/empty/foreign-purpose reads) and a real-SQL integration test
  (`PiiBackfill_EncryptsExistingPlaintext_InPlace_AndIsIdempotent`) exercising the raw-SQL backfill.

### Operational notes (important)
- **Key-ring custody is now load-bearing for data, not just sessions.** Losing the DataProtection
  key-ring makes these columns **permanently unreadable**. Back up the key-ring folder
  (`DataProtection:KeyRingPath`) routinely — see SECRETS-SETUP.md.
- **Before running the backfill:** take a full database backup (per CLAUDE.md). The button warns about
  this and the operation is safe to re-run.
- The `PiiProtectionPurpose` DataProtection purpose (`eInvWorld.Pii.FieldEncryption.v1`) is versioned and
  must never change without a re-encryption migration.

## 📅 2026-07-03 — v1.7.1 (Blueprint-gap remediation, Tier 3a: signing-key custody seam)

> Prepares the signing private key for a custody upgrade (vault/HSM) before signing is ever enabled in
> production. Pure refactor behind a new seam — signing is still OFF by default and the File-based
> loading behaviour is preserved verbatim. No schema changes; no breaking changes.

### Added
- **`ICertificateProvider`** (`Services/Signing/`) — a pluggable source of the XAdES signing certificate,
  selected by the new **`LHDNApiConfig:SigningKeyProvider`** config key (default `"File"`), mirroring the
  `IAiProvider` pattern. `DocumentSigningService` now resolves its certificate through this seam and
  **throws** (never silently no-ops) if signing is enabled but no provider matches.
- **`FileCertificateProvider`** — the previous `DocumentSigningService.GetCertificate()` file-loading
  logic extracted verbatim (blank-`CertPath` error, content-root path resolution, missing-file error,
  `X509CertificateLoader.LoadPkcs12FromFile`, load log line). Registered as a **singleton**, so the
  certificate caches process-wide — consistent with the cert-rotation runbook's `iisreset` step.
- **Vault/HSM drop-in documented** (SECRETS-SETUP.md "Signing-key custody", DOCUMENTATION.md, RUNBOOKS.md):
  a future `AzureKeyVaultCertificateProvider` is one class (`Azure.Security.KeyVault.Certificates` +
  managed identity), one DI registration, and one config value — no signing-service change.
- **`DocumentSigningServiceTests`** — disabled pass-through returns the input reference unchanged and
  never touches a provider; provider selection by name (case-insensitive, blank → `File`); unknown
  provider throws listing what IS registered; `FileCertificateProvider` blank-path/missing-file errors.

### Notes
- Deliberate design deviation from the original plan sketch: the provider method is **synchronous**
  (`GetSigningCertificate()`, not async) because `PrepareDocumentForSubmission` is sync end-to-end and
  behaviour preservation was the stated priority; the Azure SDK offers sync variants, and the seam can be
  widened if a strictly-async provider ever appears.
- The Admin → System Health cert check and `CertExpiryAlertService` still read the cert file directly —
  they are read-only diagnostics, deliberately independent of the signing path so they can report a
  broken configuration without throwing.

## 📅 2026-07-03 — v1.7.0 (Blueprint-gap remediation, Tier 2)

> Second tranche of the blueprint-gap roadmap: submission failures become visible and replayable
> instead of relying on the user to notice, encryption-in-transit is stated explicitly, and Admin
> cross-tenant reads join the tamper-evident audit trail. No schema changes; no breaking changes.

### Added
- **Submission dead-letter visibility + automatic retry.** A new durable job type,
  `SyncJobType.SubmitDocument`, is queued whenever an interactive LHDN submission throws (network blip,
  LHDN outage): the existing `DurableSyncJobWorker` retries it with backoff via a new
  `SubmitDocumentJobHandler` (which reuses `InvoiceSubmissionHelper.SubmitInvoiceAsync` — re-reads the
  invoice + draft JSON fresh, safe to replay later; no-ops if the invoice is no longer Draft, so it can
  never double-submit). If every attempt fails it lands in **Admin → Sync Jobs (Failed)** for manual
  replay — the existing dead-letter UI, `SyncFailureAlertService` email, and audit entries all apply
  automatically. Wired into all six interactive submission paths (`CreateInvoice`, `CreateCN`,
  `CreateSBI`, `CreateSBCN`, `InvoiceEdit`, `InvoiceLists`); the user-facing error message now says a
  retry has been queued. (The recurring-invoice worker and the raw REST controller were deliberately
  left out: the worker already self-heals by reverting to Draft for its next scheduled pass, and the
  controller has no invoice-scoped claim to correlate a retry with.)
- **`InvoiceViewedCrossTenant` audit entries** — when a user views an invoice none of whose parties
  belong to their own companies (post-IDOR-guard, that can only be an Admin reading another tenant's
  document), a tamper-evident audit row is written. Same-tenant views are deliberately not audited to
  avoid flooding the chain. Best-effort: an audit failure never breaks the page view.
- **`SyncJobPayloadTests`** — pure round-trip tests for the durable-job payload helpers (LookbackDays +
  the new InvoiceNo field), including cross-shape tolerance (a payload written for one job type parses
  safely for another).

### Changed
- **Explicit `Encrypt=True`** in all production connection-string guidance (IIS-DEPLOYMENT-GUIDE,
  SECRETS-SETUP, appsettings comment) so SQL Server encryption-in-transit is a visible, auditable
  setting rather than an implicit driver default; the `TrustServerCertificate=True` trade-off is
  documented alongside it.

## 📅 2026-07-02 — v1.6.0 (Blueprint-gap remediation, Tier 1)

> An external technical review of MyInvois-intermediary best practices was checked against the actual
> codebase (three parallel audits + direct verification). Most of the review's concerns were already
> handled (token caching, IDOR/object-level auth, idempotency, a durable dead-letter job queue, a
> tamper-evident audit trail) — this release closes the genuine, low-risk gaps found. Larger items
> (submission-pipeline dead-letter visibility, signing-key custody, PII field encryption, a webhook
> subsystem) are scoped for follow-up releases.

### Fixed
- **`LHDNApiService.SendWithRetryAsync`** — the 429 retry wait now grows per attempt and adds jitter
  (still never shortening LHDN's own `Retry-After`), instead of retrying 3× at the exact same delay —
  reduces the chance many concurrently-retrying submissions all wake up and re-trigger the limit together.

### Added
- **`CertExpiryAlertService`** — proactively emails an admin as the LHDN XAdES signing certificate
  approaches expiry (config: `CertExpiryAlerts:{Enabled,RecipientEmail,WarnDays,CheckHours,CooldownHours}`,
  off by default). Previously this was only visible by manually checking Admin → System Health.
- **`SECURITY.md`** — vulnerability-disclosure policy and scope.
- **`RETENTION-POLICY.md`** — makes the Income Tax Act s.82A 7-year document-retention guarantee explicit
  (invoices/UBL/PDFs/validation records are never purged by any job — only diagnostic `SystemLogs` are),
  and is honest about what it does *not* yet guarantee (no WORM/immutability, no separate cold archive).
- **`RUNBOOKS.md`** — operator procedures for signing-certificate rotation, LHDN downtime, and failed-job
  (dead-letter) replay, writing up mechanisms that already existed but weren't documented as procedures.
- **`.github/dependabot.yml`** — weekly NuGet + GitHub Actions dependency updates (grouped Microsoft/EF
  Core and Serilog point releases into single PRs to avoid CI thrash).
- **`.github/workflows/codeql.yml`** — CodeQL (`security-extended`) SAST scanning on push/PR/weekly.

### Changed
- **`Pages/Shared/_Layout.cshtml`** — the `AppInfo:Version` footer string is now Admin-only (was visible
  to any authenticated Buyer/Supplier) — a minor information-disclosure reduction.

## 📅 2026-07-02 — CI: SQL Server integration tests (LocalDB)

### Added
- **Integration tests against a real SQL Server** (`EINVWORLD.Tests/Integration/SqlServerIntegrationTests.cs`),
  run in CI via **SQL Server Express LocalDB** (pre-installed on the `windows-latest` runner). These close
  a runtime gap the in-memory provider can't cover:
  - **Migrations apply cleanly** — a fresh throwaway database is created with `Migrate()` (validating every
    migration, FK and `HasData` seed against real SQL Server), then dropped on dispose; schema is queryable.
  - **`InvoiceSubmissionGuard`** — its atomic claim/release is **raw SQL** (`ExecuteSqlInterpolatedAsync`),
    so it needs a real DB: verifies one claimant wins, a second is blocked while the claim is fresh, release
    re-opens it, and an already-submitted invoice (UUID present) is never claimed.
- CI: a step starts the `MSSQLLocalDB` instance and passes `INTEGRATION_SQLSERVER` to `dotnet test`; the
  test project references `Microsoft.EntityFrameworkCore.SqlServer` (version-matched to the app).
- **Safe everywhere:** if `INTEGRATION_SQLSERVER` is unset (e.g. no SQL Server available), the integration
  tests **no-op** so the suite still passes. Each CI run uses a uniquely-named database (no cross-job clash).

## 📅 2026-07-02 — Docs: post-deployment verification checklist

### Added
- **POST-DEPLOY-CHECKLIST.md** — a per-feature smoke test to run on the server after every deploy
  (startup/config fail-fast gates, DB/migrations, auth/IDOR, full invoice lifecycle + all 8 doc types,
  LHDN submit/sync/cancel/validate, bulk import, AI + AI-down safety, email, admin/audit/observability,
  Cloudflare Tunnel). CI proves compilation + unit tests only; this closes the runtime-verification gap
  that cannot be exercised in CI (no DB/LHDN/PDF/email/OCR/Ollama there). Linked from README.

## 📅 2026-07-01 — Ops: AI env-var rename helper script

### Added
- **`scripts/Rename-AiEnvVars.ps1`** — helper to migrate retired `AIAssistant__*` environment variables
  to `AI__*` on a Windows server. Finds them at Machine/User scope, creates the `AI__*` equivalents with
  the same values, removes the old ones (won't clobber an existing `AI__*` unless `-Force`), supports
  `-WhatIf` preview and an optional `-AppPool` recycle. DEPLOY-NOTES §0 now references it. Env vars set in
  the IIS app-pool dialog or a server `web.config` must still be renamed by hand.

## 📅 2026-07-01 — v1.5.2 (Post-audit reliability & hardening batch)

### Fixed
- **`LHDNApiService.ValidateTaxpayerAsync` 429 handling was broken.** On a rate-limit it did a fixed 5s
  delay (ignoring `Retry-After`) and re-sent the **same** `HttpRequestMessage`, which throws
  (`InvalidOperationException` — a sent request can't be reused). It now routes through the shared
  `SendWithRetryAsync` helper: clones the request per attempt, honours the LHDN `Retry-After`, retries 3×,
  and ensures success. Behaviour is otherwise unchanged (no `onbehalfof`; taxpayer's own token).

### Changed
- **`AsNoTracking()`** added to three read-only dashboard queries (`GetTopProductsAsync`,
  `GetInvoicesByCustomerAsync`, `GetInvoiceTypesAsync`) to match the others — less tracking overhead on
  read-only chart data.
- **Observability:** added `app.UseSerilogRequestLogging()` (one tidy line per request) and a one-line
  startup summary logging environment / PDF engine / AI / DocumentCapture / OCR / AutoMigrate flags
  (no secrets), so an operator can confirm from the logs exactly what an instance loaded.
- **Config hygiene:** blanked the committed test SMTP username and BCC in `appsettings.json` — these are
  environment-specific and must be supplied via env vars / user-secrets.

### Security
- **`InvoiceLists` toast messages** now inject `TempData` into the `Swal.fire` JavaScript via
  `@Json.Serialize(...)` (safe, escaped JS string) instead of `@Html.Raw(...)` inside a quoted string —
  defensive hardening against any future case where those messages carry untrusted text.

### Notes
- Reviewed but **intentionally not changed:** invoice-number generation (`InvoiceService.CurrentMaxNumber`)
  — it only runs on invoice creation and projects the `EINV` suffixes, and a refactor of the money-numbering
  path carries more correctness risk than the marginal performance gain justifies.

## 📅 2026-07-01 — Docs: existing-install upgrade checklist

### Added
- **DEPLOY-NOTES.md §0 "Upgrading an existing installation"** — an ordered operator checklist for moving a
  running server to a newer build: back up DB + `App\` + verify the DataProtection key ring persists,
  stop the site, deploy, rename retired `AIAssistant__*` env vars to `AI__*`, let additive migrations run,
  start, smoke-test (health / sign-in / create+submit / AI Test connection), and roll back if needed.

## 📅 2026-07-01 — v1.5.1 (Retire legacy AIAssistant config; AI cleanup)

### Changed / Removed
- **Retired the legacy `AIAssistant` configuration section.** AI configuration now lives **only** in the
  `AI` section. The one-release fallback that read `AIAssistant` is removed from `Program.cs` and
  `ProductionConfigValidator`, and `AiSettings.LegacySectionName` is deleted. Stale in-app hints and docs
  that still referenced `AIAssistant:Enabled` / `ollama pull llama3.1` now point at `AI:Enabled` /
  `gemma3:12b`.
  > **⚠️ Action on upgrade:** if a server sets `AIAssistant__*` environment variables, **rename them to
  > `AI__*`** (e.g. `AIAssistant__Enabled` → `AI__Enabled`, `AIAssistant__Model` → `AI__Model`).
  > Otherwise AI simply stays **off** after upgrading — invoicing is unaffected either way.
- Validator tests updated to assert the retired `AIAssistant` section is ignored.
- **Database:** no changes — the AI features are fully stateless (no AI tables/columns exist), so there is
  nothing to migrate or drop.

## 📅 2026-07-01 — v1.5.0 (Provider-agnostic AI; admin Test-connection)

> Ships the built-in-by-default, provider-agnostic AI layer (Ollama today; OpenAI/Azure/Claude/Gemini
> ready) with an admin **Test connection** page. AI stays optional and off by default — invoicing is
> unaffected if AI is disabled or unreachable. Additive only: **no migration, no breaking changes.**
> Also folds in the same-day CLAUDE.md engineering guide and the expanded submitter-TIN / self-billed
> UBL test coverage (entries below).

### Added
- **Provider-agnostic AI layer** (`Services/AI`). Business logic now depends only on `IAiService`, never a
  concrete backend, so OpenAI/Azure/Claude/Gemini can be added as drop-in `IAiProvider` registrations
  without touching callers. Ships with the local, on-prem **Ollama** provider. Typed
  `AiChatRequest`/`AiChatResult`/`AiProbeResult` DTOs; `AiService` owns the master enable switch, provider
  selection by name, default temperature/max-tokens, and a **non-throwing guarantee** — if AI is disabled,
  misconfigured or the provider errors, callers get a typed failure and **invoice creation/submission is
  unaffected**. Logging is metadata-only (provider, model, outcome, duration) — **no prompts, keys or
  tokens are logged**. Covered by new `AiServiceTests`.
- **Admin → AI Settings** (`/Admin/AiSettings`) — read-only view of the active AI config plus a **Test
  connection** probe reporting reachable / model-pulled / latency and the provider's available models.
  Never displays the API key; audits the outcome only (`AiConnectionTested`).

### Changed
- **Canonical config section is now `AI`** (adds `Temperature`/`MaxTokens`; default model **`gemma3:12b`**).
  The legacy **`AIAssistant`** section is still read as a **fallback for one release** (logs a deprecation
  warning). Recommended local models: **`gemma3:12b` / `gemma3:27b` / `qwen3:32b`** — models are **not
  bundled**; pull them with Ollama (`ollama pull gemma3:12b`). `ProductionConfigValidator` validates
  whichever section is active. `EInvoiceAssistantService` keeps its LHDN prompts/grounding/validation but
  now delegates model calls to `IAiService` (no HTTP in domain code). Docs updated (README, DOCUMENTATION,
  IIS guide PART O, DEPLOY-NOTES, SECRETS-SETUP).

## 📅 2026-07-01 — More test coverage (submitter-TIN rule; self-billed UBL)

### Added
- **`TinHelperTests`** — locks down the LHDN submitter-TIN rule (`IsSelfBilledDocType`; `ResolveSubmitterTin`
  → Customer TIN for self-billed 11–14, Supplier TIN otherwise, incl. null-navigation and null-arg cases).
- **`InvoiceMapperTests`** — added a self-billed (doc type 11) case asserting the `BillingReference` carries
  **both** `InvoiceDocumentReference` and `AdditionalDocumentReference`. Pure tests, no new dependencies.

## 📅 2026-07-01 — Engineering guide

### Added
- **`CLAUDE.md`** — the enterprise engineering standard for the project: the mandatory engineering loop,
  production/security/performance/DB/LHDN/logging/docs standards, the CI-is-the-compiler + hand-authored-
  migration realities, the branch/PR/secrets workflow, architecture strengths to protect, and the known
  improvement backlog. Read it before changing code.

## 📅 2026-06-25 — v1.4.1 (AI Document Capture OCR; remove legacy Extract Invoice)

### Added
- **Scanned-PDF OCR in AI Document Capture.** When an uploaded PDF has no text layer, it's now rasterized
  (PDFtoImage/PDFium on the existing SkiaSharp) and OCR'd (Tesseract, Apache-2.0), then fed into the same
  LLM suggestion path — so scanned invoices work, not just digital PDFs. **OFF by default**
  (`DocumentCapture:OcrEnabled`); requires a `TessdataPath` (e.g. `eng.traineddata`) and the native
  runtimes. The native libs load only when OCR is enabled, so default installs are unaffected. Deploy
  steps in IIS guide **Part 17a-OCR**. (Note: CI verifies compilation only — OCR must be verified on the
  server.)

### Removed
- **Legacy "Extract Invoice (Beta)" page** and its `ExtractInvoice` config — it depended on an external
  Python OCR service (`127.0.0.1:8000`) and is superseded by AI Document Capture's built-in OCR.

## 📅 2026-06-25 — v1.4.0 (Unify bulk invoice import)

### Changed
- **One "Bulk Invoice Import".** The full importer (`/Invoices/ImportCSV` — validate → confirm → create
  drafts) now accepts **`.xlsx` as well as `.csv`** (same column schema, mapped by header name), with a
  **"Download Excel Template"** button. The separate validate-only **"Bulk Import (validate)"** menu item is
  retired (its `BulkInvoiceImportService` + XLSX template are kept — they still power the
  `POST /api/import/validate` REST API). Menu labels unified (the importer was inconsistently shown as
  "Import Invoices" vs "Bulk Invoice Import").

## 📅 2026-06-25 — v1.3.9 (HTTPS-redirect smart default — tunnel loop fix)

### Fixed
- **Redirect loop behind a TLS-terminating proxy / Cloudflare Tunnel.** The HTTP→HTTPS redirect now
  defaults **OFF when `ForwardedHeaders` is enabled** (the app has declared it's behind an edge that
  terminates TLS and forwards plain HTTP) — an in-app redirect there loops `http→https→http`. For a direct
  IIS HTTPS binding it still defaults to `443`. An explicit `Security:HttpsRedirectPort` always wins
  (a port = on, `0` = off), and `UseHttpsRedirection` is now skipped entirely when the redirect is off.
  Removed the hardcoded `HttpsRedirectPort: 443` from `appsettings.json` so the smart default applies.

## 📅 2026-06-25 — v1.3.8 (Optional hardening — safe set)

### Added
- **Stricter `/Admin/InvoiceSync` rate limit** — a per-user `admin-sync` policy (default 10/min,
  `RateLimiting:AdminSyncPerMinute`) so one admin can't flood the durable job queue; the global per-IP
  limiter is unchanged.
- **Decimal precision validation** — new `[MaxDecimalPlaces]` attribute applied to invoice line
  Quantity (6), Unit Price (4), Discount (2) and Tax % (4, plus `[Range(0,100)]`), so over-precise input
  is rejected instead of silently rounded to the column scale.
- **Wider audit coverage** — admin sync triggers (`SyncStatusTriggered`, `FullImportTriggered`) and Sync
  Jobs actions (`SyncJobRetried`, `SyncJobCancelled`, `SyncJobsBulkRetried`) now write to the audit chain.
- **Proactive failure-alert email** — optional `SyncFailureAlertService` emails an admin when failed sync
  jobs cross a threshold (off by default; `SyncFailureAlerts` config; throttled so it never spams).
- **PDF render timeout** — DinkToPdf renders run with a configurable timeout
  (`PDFGenerationSettings:TimeoutSeconds`, default 60) so a hung wkhtmltopdf render can't block the request.
- **Docs** — README/DOCUMENTATION note the single-instance (per-process) LHDN rate-limiter assumption.

### Deferred (high-risk; intentionally not in this set)
- Global `InvoiceHeader` `RowVersion` optimistic concurrency (20+ unguarded SaveChanges sites — needs its
  own concurrency-tested change); splitting the 1,263-line `InvoiceMapper` (critical money path);
  OpenTelemetry (no metrics backend on a single on-prem node).

## 📅 2026-06-25 — v1.3.7 (Dead-letter visibility — review Batch C round 3)

### Added
- **Failed-job (dead-letter) view on Admin → Sync Jobs.** A red "N failed" badge in the header links to a
  `?status=Failed` view that lists **all** failed jobs (up to 500, so failures that fell past the latest-100
  window are still reachable), with a **"Retry all failed"** bulk action to re-queue the whole dead-letter
  queue at once. Full Running/Queued/Failed counts are now computed across the table, not just the page.

> Note: the Admin → System Health dashboard already surfaced the failed-job count (red, with a "review"
> link) and the oldest-queued age — this round adds the drill-down + bulk recovery. A proactive email/alert
> on repeated failures remains a deferred option.

## 📅 2026-06-25 — v1.3.6 (Correlation IDs — review Batch C round 2)

### Added
- **End-to-end correlation IDs.** Every request gets a correlation id (`CorrelationIdMiddleware`, placed
  early in the pipeline) — taken from an incoming `X-Correlation-ID` header or the framework
  `TraceIdentifier`, echoed back in the response header, and pushed to Serilog's `LogContext`. Since
  `Enrich:FromLogContext` was already on, every log line for the request now carries it; the file sink
  shows it (`[{CorrelationId}]`) and the `SystemLogs` sink captures it in the `LogEvent` column — no
  schema change.
- **Background jobs are correlated too** — `DurableSyncJobWorker` tags all logs for a job with
  `syncjob-{id}`, and `InvoiceStatusUpdater` tags each invoice's sync with `statussync-{invoiceNo}`.
- **Audit rows inherit the request correlation** — `AuditService` falls back to the request
  `TraceIdentifier` when a caller doesn't pass a `CorrelationId`, so an audit entry ties back to the
  request's log lines (`AuditLog.CorrelationId` already existed).

## 📅 2026-06-25 — v1.3.5 (Tests — review Batch C round 1)

### Added (test coverage — no production code changes)
- **Money math tests** (`InvoiceCalculationTests`) — line totals (Qty×Price, discount, multi/zero/exempt
  tax) and header aggregation, covering the core financial-correctness logic.
- **UBL mapper tests** (`InvoiceMapperTests`) — drive `InvoiceMapper.MapToJsonModel` from in-memory
  invoices and assert legal monetary totals + rounding, the BillingReference doc-type dispatch
  (01 vs 02), "NA" party-identification filtering, and that missing required party fields throw.
- **Helper tests** (`HelperTests`) — `GeneralTINHelper.IsGeneralTIN`, `DateTimeHelper.ToMalaysiaTime`,
  `AmountInWordsHelper.ToWordsEnglish`.
- All use the existing xUnit project with **no new package dependencies**; they run in the same CI
  `dotnet test` step that gates merges.

### Fixed (surfaced by the new mapper tests)
- **`InvoiceMapper.MapLineAllowanceCharges` null-safety** — it dereferenced `line.InvoiceHeader.Currency`
  directly while every sibling line in the same method already used `line.InvoiceHeader?.Currency ?? "MYR"`.
  Aligned the lone outlier to be null-safe.

> Deferred Batch C follow-ons (their own PRs): correlation-ID log enricher; failure/dead-letter
> Admin visibility.

## 📅 2026-06-25 — v1.3.4 (Data-integrity — review Batch B)

### Fixed
- **Idempotency vs signing toggle** — the submission dedup hash now folds in `SigningEnabled`, so flipping
  signing on/off can never replay a cached response from the other signing state (`LHDNApiService`).
- **Backoff overflow guard** — `DurableSyncJobWorker` clamps the retry exponent before `Math.Pow`, so a
  large `MaxAttempts` can't produce `Infinity/NaN` backoff.
- **Token cleanup robustness** — `TokenRenewalService` uses `GeneralTINHelper.IsGeneralTIN` (exact match,
  all 4 general TINs) instead of a fragile substring check, and the revoked-token delete is wrapped so a
  failed delete logs instead of looping forever.
- **Sync visibility** — `InvoiceStatusUpdater` logs (instead of silently skipping) when an invoice's
  submitter TIN can't be resolved.

### Added
- **Status-sync hot-path index** — migration `AddInvoiceStatusSyncIndexes` adds a composite index on
  `InvoiceHeaders (LHDNStatusId, LastUpdated)` for the background poller (additive/idempotent; apply via
  AutoMigrate or `Apply_AddInvoiceStatusSyncIndexes.sql`).

### Deferred (examined, intentionally not changed)
- Explicit transactions on invoice save (header+lines already save in one atomic `SaveChanges`);
  a unique constraint on `SubmissionRecords` (wrong fix for a time-windowed cache; concurrent double-submit
  already guarded by `InvoiceSubmissionGuard`); a global `InvoiceHeader` `RowVersion` (20+ unguarded
  `SaveChanges` sites — needs its own dedicated change). See review notes.

## 📅 2026-06-24 — v1.3.3 (Security hardening — review Batch A)

### Security
- **No default demo users in Production** — `admin@/supplier@/buyer@einvworld.com` seeding is now gated
  behind `Seeding:SeedDefaultUsers` (base `true` for dev, forced **`false`** in `appsettings.Production.json`).
  Seed passwords are overridable via `Seeding:Default*Password`. Existing installs are unaffected; admins
  are still forced to enrol 2FA on first login.
- **`SameSite=Lax`** added to the session and Identity auth cookies (CSRF defence-in-depth; HttpOnly + Secure
  already set).
- **No PII / token in logs** — `LHDNApiService` logs the user id (not email) and only the request method+URI
  (never the `HttpRequestMessage`).
- **Email header-injection guard** — CR/LF stripped from `CompanyName` before it goes into a mail subject
  (`Pages/Lead/Submit`).
- **Stored-XSS hardening** — user-supplied address and item-description fields are HTML-encoded before being
  rendered (`InvoiceDetails2`, `PdfTemplate`, `PublicCustomer/Details`) instead of raw output.
- **Upload/DoS limits** — bulk import capped at 20k rows; request body / multipart size bounded to 32 MB
  (Kestrel + IIS + `FormOptions`).
- **Socket-exhaustion fix** — Cloudflare Turnstile verification uses `IHttpClientFactory` instead of
  `new HttpClient()` per request (`Pages/Contact`).

## 📅 2026-06-24 — v1.3.2 (Staging-log fixes)

### Fixed
- **`SystemLogs` cleanup timeout** — `LogCleanupService` ran a single unbounded `DELETE`, which escalated
  to a table lock and hit the command timeout on a large table (`Execution Timeout Expired`, deleting
  nothing each cycle). Now deletes in batches of `LogCleanupSettings:BatchSize` (default 5000) with a
  120 s `CommandTimeout`; a large backlog drains gradually over a few runs.
- **Invoice list pagination order** — `InvoiceLists` ran `Skip/Take` with no guaranteed `OrderBy` on a
  plain page load (no filter/sort), causing the EF "Skip/Take without OrderBy" warning and
  non-deterministic paging. A deterministic order (default `InvoiceNo`) is now always applied.
- **HTTPS redirect port** — set explicitly via `Security:HttpsRedirectPort` (default `443`) so IIS
  deployments no longer log "Failed to determine the https port for redirect". `0` leaves it auto/off.

### Added
- **Reverse-proxy / Cloudflare Tunnel support** — new `ForwardedHeaders` section (on by default). When TLS
  is terminated upstream and the app is reached over plain HTTP (e.g. a Cloudflare Tunnel to
  `http://localhost`), the app now honours `X-Forwarded-Proto` (original scheme = https → correct Secure
  cookies, HSTS, no redirect loop) and `X-Forwarded-For` (real client IP → correct per-IP rate limiting and
  audit/log IPs instead of `127.0.0.1`). Only headers from a trusted proxy (loopback by default) are
  honoured. `Security:HttpsRedirectPort=0` disables in-app HTTPS redirects for tunnel/edge-TLS setups.
  New IIS guide **Part 8b** documents the full Cloudflare Tunnel deployment.

### Changed
- **Default AI model is now `llama3.2:3b`** (was `llama3.1`). The smaller ~2 GB model fits a modest
  server's RAM; the larger 8B model could fail to allocate memory and time out (`TaskCanceledException`).
  Docs updated to recommend sizing the model to available RAM.

---

## 📅 2026-06-22 — v1.3.1 (Resilience · Cleanup)

### Added
- **Inbound rate limiting** — a generous per-IP backstop (`RateLimiting` config; health probes exempt)
  against runaway/abusive traffic. Login brute force is already capped by Identity lockout.
- **Outbound resilience on token acquisition** — `AddStandardResilienceHandler` (retry + timeouts) on the
  OAuth token client only, so transient LHDN/network blips don't fail a sync cycle. Deliberately NOT
  applied to the document-submission client (a retried POST could create a duplicate).

### Changed
- **Legacy "Extract Invoice" OCR URL is now configurable** (`ExtractInvoice:ServiceUrl`) instead of a
  hardcoded `http://127.0.0.1:8000`. (Overlaps the newer AI Document Capture; retire it once Capture
  covers the need.)

### Removed (earlier in this cycle, PR #10)
- The dead in-memory background queue (replaced by the durable SQL worker), an unused model, a large
  commented block, and `DEBUG` text in user-facing messages; added logging to silent catch blocks.

---

## 📅 2026-06-22 — v1.3 (Durable ops · Security · Audit · Ingestion)

> Build clean on .NET 10 (CI: restore + build + tests green on windows-latest). Production-hardening
> release: makes background work durable, adds tamper-evident auditing and admin MFA, and introduces a
> draft-safe invoice-ingestion suite (document capture, bulk import, watched folder, REST validate API).
> All new features are **OFF/safe by default** and add **no destructive migrations** (existing data is
> preserved). New DB objects are applied automatically on startup (auto-migrate) or via the idempotent
> `Apply_*.sql` scripts.

### Added — durability & operations

- **Durable SQL-backed background queue.** Manual sync/import/refresh no longer ride an in-memory queue
  of closures that vanished on an app-pool recycle/reboot. The `SyncJobs` row **is** the work item:
  `DurableSyncJobWorker` polls Queued rows, atomically claims one (`UPDLOCK`/`READPAST`), dispatches it
  by `JobType` to a handler that rebuilds the work from data, retries with exponential backoff up to
  `MaxAttempts`, and on startup recovers any job left `Running` by a killed process. New durability
  columns on `SyncJobs` (migration `AddSyncJobDurability`).
- **Sync Jobs Retry/Cancel** controls on `/Admin/SyncJobs`.
- **Liveness/readiness health split** — `/health/live` (process up, for IIS App Initialization) and
  `/health/ready` (DB + a writable-folders check for Documents/GeneratedPdf/DataProtection key ring).
  `/health` retained.
- **Admin → System Health** dashboard — queue depth / failed / oldest-queued job, audit + submission
  row counts, DataProtection key-ring writability, Documents-drive free space, and signing-cert expiry.

### Added — security & compliance

- **Admin two-factor authentication enforced (block-until-enrolled).** An authenticated Admin without
  2FA is redirected to the authenticator-setup page until enrolled; the `/Identity` area, health, and
  static assets stay reachable, so there is no hard lockout. Gated by `Security:EnforceAdminMfa`
  (default `true`) as an emergency escape hatch.
- **Tamper-evident, hash-chained audit trail.** New append-only `AuditLogs` table where each row stores
  the previous row's hash plus a SHA-256 of its own contents chained onto it — recomputing the chain
  detects any insert/delete/edit. `AuditService` (serialised appends, isolated DbContext, never throws
  to the caller) is wired into the LHDN mutations (InvoiceSubmitted / DocumentCancelled /
  DocumentRejected). **Admin → Audit Trail** lists entries and runs one-click chain verification.
  Migration `AddAuditLog`.
- **Local duplicate-submission idempotency.** At the single submission chokepoint, the (pre-signing)
  payload is hashed and an identical resubmission within a 10-minute window replays the prior response
  instead of creating a duplicate at LHDN (mirrors MyInvois' 422 DuplicateSubmission). New
  `SubmissionRecords` table (migration `AddSubmissionRecords`). Complements the atomic
  `SubmissionClaimedAtUtc` claim.
- **Fail-fast production config validation** (`ProductionConfigValidator`) at startup: blank connection
  string, missing `DataProtection:KeyRingPath`, signing enabled without a cert, localhost PDF/email
  URLs in Production, preprod LHDN host in Production, or AI assistant enabled without URL/model now
  stop boot with one clear message instead of failing vaguely at runtime.
- **CSP violation reporting** — the existing Report-Only policy now points `report-uri` at a new
  anonymous `/csp-report` endpoint that logs violations, so the policy can be tightened from real data
  before being promoted to enforcing.

### Added — invoice ingestion (all draft-safe: validate/suggest only, never auto-create or submit)

- **AI Document Capture (Phase 1)** at `/Invoices/CreateFromFile` — upload a digital invoice PDF,
  extract its text (**PdfPig**, MIT) and turn it into a reviewed invoice suggestion via the local
  Ollama LLM, reusing the assistant's `SuggestInvoiceAsync` + `ReviewSuggestion` + known-buyer
  grounding. Scanned images (no text layer) are reported as "needs OCR" (a later phase). Config
  `DocumentCapture` (OFF; requires `AIAssistant:Enabled`).
- **Bulk import (validate-only)** at `/Invoices/BulkImport` — upload CSV/XLSX (one row per invoice
  line) for a per-row validation report against the real LHDN reference codes (classification, tax,
  currency, unit) plus required/numeric/doc-type rules; downloadable `.xlsx` template.
- **Watched-folder importer (validate-only)** — `WatchedFolderImportWorker` validates CSV/XLSX dropped
  into an Inbox, writes a `.report.json`, and sorts files into `Processed/`/`Rejected/`. OFF by default
  (`WatchedFolderImport`).
- **REST validate API** — `POST /api/import/validate` for an external ERP, authenticated with a static
  `X-Api-Key` (constant-time compare) against `Api:Key`; disabled until the key is configured.

### Changed

- **Hardened `ImageController`** — replaced the hardcoded `E:\…\Logos` path with
  `FilePathConfig.CompanyLogosFolder`, swapped the weak `StartsWith` traversal check for the canonical
  `SafePath.TryResolve` guard, and added an image extension allow-list.
- **`appsettings.Production.json`** now ships with `DatabaseSettings:AutoMigrateOnStartup = true` and a
  preset `DataProtection:KeyRingPath` (`E:\EINVWORLD\Keys`). New-version migrations are additive, so
  auto-migrate preserves existing data — **take a full DB backup first** and ensure the runtime SQL
  login has DDL rights. The manual `Apply_*.sql` path remains available (`AutoMigrateOnStartup = false`).
- **`CancelDocumentAsync`** now uses `GetAccessTokenForTIN(tin)` + the `onbehalfof` header (was relying
  on session state), matching `RejectDocumentAsync` — fixes intermediary/on-behalf-of cancellations.

### Migrations (additive — no `Up()` drops data)

`AddInvoiceSubmissionClaim`, `AddSyncJobDurability`, `AddSubmissionRecords`, `AddAuditLog` (plus the
earlier `SyncModelAfterNet10Upgrade`, `AddSyncJobTable`, `DecoupleSystemLogsFromEf`,
`FixInvoiceDecimalPrecision`, `AddInvoiceHotPathIndexes`). Each has an idempotent
`Migrations/Apply_*.sql`. See `DEPLOY-NOTES.md` for the order.

### Docs

- Refreshed `README.md`, `SECRETS-SETUP.md`, `DEPLOY-NOTES.md`, and `IIS-DEPLOYMENT-GUIDE.md` for the
  above: DataProtection key-ring requirement, auto-migration + backup-first, admin-2FA enrolment, System
  Health, the `Api:Key` secret, and the optional ingestion features.

---

## 📅 2026-06-19 — v1.2 (Background jobs · Job visibility · AI assistant · Docs)

> Build clean (0 errors) with **58 passing unit tests** on .NET 10. Follow-up to the v1.1 modernization: moves the heavy manual LHDN operations onto a paced background queue, adds job visibility, an optional on-prem AI assistant, and refreshes the documentation.

### Added

- **Background "Sync Jobs" admin page** (`/Admin/SyncJobs`) — every manual sync/import/refresh now writes a `SyncJobs` row (Queued → Running → Completed/Failed) with timing, result message and who triggered it, so users can confirm a backgrounded job actually ran instead of it disappearing into the queue. The page auto-refreshes while work is active. Backed by a new `ISyncJobTracker` service and the additive `SyncJobs` table (migration `AddSyncJobTable`; idempotent script `Migrations/Apply_AddSyncJobTable.sql`).
- **AI E-Invoice Assistant (config-gated, OFF by default)** — a local-LLM assistant at `/Assistant` that (a) answers Malaysian e-invoicing / LHDN questions and (b) turns a plain-English transaction description into a suggested invoice (document type, lines, tax) for the user to review. Runs entirely on-prem via **Ollama** (FOSS, open-weight models) so **no invoice data leaves the server**; it only suggests and never submits. The suggestion prompt is grounded with the real LHDN classification codes (loaded from `wwwroot/codes/ClassificationCodes.json`) so it emits valid codes. A **"Use in Create Invoice form"** button carries the suggestion (via `sessionStorage`) into the real Create Invoice form and pre-fills document type + line items client-side — the user still selects the actual supplier/customer and reviews every field before saving through the existing, tested path; nothing is persisted or submitted automatically. Enable via the `AIAssistant` config section after installing Ollama and pulling a model; fails gracefully when disabled/unreachable.
- **Unit tests expanded 49 → 58** — added coverage for the per-TIN background queue (incl. the re-enqueue-after-drain regression) and the AI assistant disabled-state guard.

### Changed

- **Manual LHDN operations now run in the background** — the admin **"Run Invoice Sync Now"** / **"Import All Invoices from LHDN"** buttons and the supplier **"Refresh from API"** button previously ran the whole LHDN pull synchronously inside the HTTP request (blocking the page, risking timeouts, bursting LHDN calls). They now **enqueue work onto the existing `IBackgroundTaskQueue`** (one paced job per company TIN, General TINs excluded) and return immediately; the work runs in the background, evenly paced by `LhdnRateLimitHandler`.
- **Sync lookback windows are now explicit** — new `lookbackDays` parameter on `RunFullImportFromLhdnAsync` / `GetAllUuidsForTinAsync`. The supplier "Refresh from API" is capped to **7 days** (and keeps its 5-minute per-session cooldown); the admin "Import All" now uses the previously-dead `LHDNApiConfig:SyncRetentionDays` setting (default 60 days) so that config finally has an effect; other callers default to 3 days.
- **Removed redundant manual `Task.Delay` pacing** inside the sync loops (pacing is centralized in `LhdnRateLimitHandler`); the functional 15-second wait for LHDN to generate the `LongId`/QR code is retained. The old synchronous `RefreshInvoicesFromApi` (and its bespoke retry helpers) was removed in favour of the shared `InvoiceSyncHelper.RunFullImportFromLhdnAsync`.
- **`SystemLogs` log table is now owned by the Serilog sink, not EF** — set `autoCreateSqlTable: true` so the Serilog MSSqlServer sink creates/owns the table; removed the EF `DbSet`/entity mapping (`SystemLog` is now a plain read DTO). The two original EF migrations (`AddSystemLogsTable` / `AddUserNameToLogs`) were neutralised and a no-drop `DecoupleSystemLogsFromEf` migration removes it from the EF model snapshot — **the existing table and its rows are preserved** (idempotent script `Migrations/Apply_DecoupleSystemLogsFromEf.sql`). The Admin → System Logs page now reads via `Database.SqlQueryRaw<SystemLog>` (filters/paging unchanged). This removes the fresh-DB create race entirely (only the sink creates the table).

### Fixed

- **Background queue silently dropped repeat jobs per TIN** — `BackgroundTaskQueue.EnqueueAsync` registered a TIN in the round-robin rotation only inside the `GetOrAdd` factory (runs once per TIN), but `DequeueAsync` removes a drained TIN from the rotation while leaving its queue entry. So the **2nd and every later** job for the same TIN was enqueued and released the semaphore, but `DequeueAsync` could never find it and returned `null` — the job was lost. `EnqueueAsync` now re-registers the TIN on every enqueue. This surfaced once the manual buttons were routed through the queue (a supplier's 2nd "Refresh from API" would have done nothing). Covered by a regression test.
- **Source directories wrongly excluded from git** — the `.gitignore` rule `logs/` (intended for the Serilog output dir) also matched the **source** folders `Pages/Admin/Logs/` (the admin System Logs page) and `Models/Logs/` because git is case-insensitive on Windows. Those files were never committed, so a fresh clone would not compile. Anchored the rule to `/logs/` (repo root only) and added the missing source.

### Docs

- Renamed `IIS-DEPLOYMENT-GUIDE-v1.1.md` → **`IIS-DEPLOYMENT-GUIDE.md`** (and updated its in-document title) and added **PART O — (Optional) AI E-Invoice Assistant** with Ollama install/enable steps + a troubleshooting entry.
- Added **`SECRETS-SETUP.md`** documenting every secret and how to configure it via user-secrets (dev) and IIS environment variables (server).
- Rewrote **`README.md`** (overview, tech stack, features, getting started, configuration table, docs index).
- Documented the `SystemLogs` table's purpose: a queryable system/audit log (the Serilog MSSqlServer sink) surfaced on the **Admin → System Logs** page, with custom `IPAddress` / `UserName` columns.

---

## 📅 2026-06-14 — v1.1 (Production-Readiness · .NET 10 · Security · FOSS)

> Major hardening and modernization release. Build is clean (0 errors) with **49 passing unit tests** on .NET 10. Secrets are externalized; the deployment procedure is in `IIS-DEPLOYMENT-GUIDE.md` and secret setup in `SECRETS-SETUP.md`.

### Added

- **.NET 10 readiness**: `global.json` pinning the SDK band; `LangVersion=latest`.
- **Health endpoint** `/health` (DB connectivity via `AddDbContextCheck`) for uptime monitoring; allowed anonymous and excluded from the no-cache middleware.
- **Security response headers** middleware (applied to all responses): `X-Content-Type-Options=nosniff`, `X-Frame-Options=SAMEORIGIN`, `Referrer-Policy=strict-origin-when-cross-origin`, `X-Permitted-Cross-Domain-Policies=none`.
- **v1.1 digital-signature capability (config-gated, OFF by default)**: `IDocumentSigningService` / `DocumentSigningService` implementing the MyInvois XAdES-JSON signature, wired centrally into `LHDNApiService.SubmitDocumentsAsync` (no caller changes). Enable via `LHDNApiConfig:SigningEnabled=true` + `DocVersion="1.1"` + a signing certificate. By-default no-op; fails closed when enabled-but-misconfigured. Validate against MyInvois PREPROD before go-live.
- **Switchable PDF engine**: `IPdfRenderer` abstraction with `DinkToPdfRenderer` (default, unchanged output) and `PuppeteerPdfRenderer` (headless Chromium, MIT, no native DLL), selected by `PDFGenerationSettings:Engine` (+ optional `ChromiumExecutablePath`).
- **DIP/testability interfaces** (registered via forwarders so runtime resolution is unchanged): `ILHDNApiService`, `IPdfGeneratorService`, `IEInvoiceNotificationService`, `IJsonFileService`.
- **`.editorconfig`** with code-style, analyzer, and naming conventions (IDE-level, non-breaking).
- **Unit test project expanded to 49 tests** covering invoice numbering, submitter-TIN resolution, status-refresh cooldown rules, submission guard behaviour, and the signing no-op/fail-closed guarantees.
- **IIS Deployment Guide** — beginner-friendly production setup (`IIS-DEPLOYMENT-GUIDE.md`).

### Changed

- **Framework upgrade**: .NET 8 → **.NET 10 (LTS)**. All Microsoft / EF Core / Identity / Serilog packages → 10.x; third-party packages updated to latest compatible.
- **LHDN rate limiting consolidated** into a single `LhdnRateLimitHandler` covering every endpoint (token, validate, submit, poll, search, get-document, cancel/reject), attached to both the LHDN and token HTTP clients. `/documents/raw` lowered to 50/min; the token endpoint is now throttled; `TokenRenewalService` spaces renewals 5s apart to respect the 12 RPM token limit. The duplicate `LHDNApiService` HttpClient registration was removed.
- **Token cache** moved from leak-prone static dictionaries to `IMemoryCache` (auto-evicts at token expiry; no de-sync), with double-checked locking.
- **Invoice numbering consolidated** — 6 copy-pasted generators now delegate to one `InvoiceService`; submitter-TIN logic (5 sites) → `TinHelper.ResolveSubmitterTin`; terminal-status + re-poll cooldown rules → `InvoiceSyncRules`.
- **Secrets externalized** out of `appsettings.json` to user-secrets (dev) / environment variables (server). Config precedence corrected so env/user-secrets always override placeholders.
- **EF migrations** now gated by `DatabaseSettings:AutoMigrateOnStartup` (default `true`); set `false` in production to run migrations as a controlled deploy step.
- **Duplicate invoice-detail page** consolidated — the legacy `InvoiceDetails` is now a thin redirect to the active `InvoiceDetails2`; new email links point at `InvoiceDetails2`.

### Fixed

- **LHDN 429 "Too Many Requests" storm + delayed QR/LongId capture** — the client rate limiter used token buckets sized at the full per-minute limit (e.g. `TokenLimit = 50`), so it released a 50-request **burst** that LHDN's stricter window rejected with `429 "try again in 59 seconds"`, stalling the whole status-sync (and therefore delaying `LongId`/QR-code capture for hours). Per MyInvois SDK guidance, `LhdnRateLimitHandler` now **paces requests evenly** — one release every `(60s / rate)` with only a tiny burst (`PacedBucket`) — staying under each endpoint's limit at all times so 429s no longer occur; excess requests queue and wait instead of failing. With the sync no longer 429-stalled, validated invoices get their `LongId` (QR code) populated promptly. *(Note: the status sync still polls `GET /documents/{uuid}/raw` per invoice; a future optimization is to use the bulk "Get Recent Documents" endpoint for status checks.)*
- **DataProtection key ring wiped on redeploy** — keys were persisted to `{App}\DataProtectionKeys`, which the deploy procedure clears, resetting the key ring on every release. That caused `"The key {…} was not found in the key ring"`, mass logouts, antiforgery failures, and the intermittent **"TIN not found in session"** submission error. The key-ring path is now configurable via `DataProtection:KeyRingPath` (or env var `DataProtection__KeyRingPath`) — point it at a stable folder **outside** `App\` (e.g. `D:\EINVWORLD\Keys`) so keys survive deployments.
- **LHDN submission rejected with "Validation Error / TooFewItems" after the upgrade** — the generated MyInvois document was emitting **empty arrays** (`"Percent": []`, buyer `"IndustryClassificationCode": []`, header `"MultiplierFactorNumeric": []`, `"InvoiceDocumentReference": []`, top-level `"AdditionalDocumentReference": []`) for unpopulated optional fields. `NullValueHandling.Ignore` only drops nulls, not empty `List<>` (the JSON models default to `= new()`), so LHDN read them as "TooFewItems" and rejected the document. Added `SkipEmptyCollectionsContractResolver` and applied it to the document serialization in `InvoiceMapper`, so the document now contains **only fields that have data** (empty collections are omitted) — restoring the long-standing "submit required/populated fields only" behaviour. Covered by unit tests. **Note:** existing draft `.json` files generated by the broken build must be re-saved (re-opened and saved) to regenerate clean JSON before resubmitting.
- **EF Core 10 startup crash — `PendingModelChangesWarning`** (`Database.Migrate()` threw on boot after the .NET 10 upgrade because EF Core 9/10 promote this warning to a hard error). Root-caused and resolved without risk to the existing database:
  - Added an **`IDesignTimeDbContextFactory`** (`Data/ApplicationDbContextFactory.cs`) so the EF CLI builds the context **without** running `Program.Main` (which migrates, seeds and loads the native wkhtmltox DLL).
  - **Pinned the ASP.NET Identity key columns to `nvarchar(128)`** in `OnModelCreating` (`AspNetUserTokens.LoginProvider/Name`, `AspNetUserLogins.LoginProvider/ProviderKey`) — matching the existing DB and preventing EF's auto-widening to `nvarchar(450)`, which would have blown past SQL Server's 900-byte clustered-index key limit and **failed** on apply.
  - Added migration **`SyncModelAfterNet10Upgrade`** containing only **23 non-destructive `ALTER COLUMN … NULL`** operations (pre-existing NOT NULL→NULL model/DB mismatches that EF 8 silently tolerated) — no drops, no Identity changes, no data loss.
  - Provided an **idempotent SQL script** (`Migrations/Apply_SyncModelAfterNet10Upgrade.sql`) that only applies what's missing (checks `__EFMigrationsHistory`) — the safe way to update the production DB during deploy.
- **`PollSubmissionStatusAsync` 401 handling** — a 401 now fails fast (throws `UnauthorizedAccessException`) instead of being swallowed and retried through all 10 attempts.
- **Invoice numbering beyond `EINV99999`** — replaced string-sort ordering with a numeric max; fixed an `int.Parse` crash on non-standard numbers such as `EINV00042(1)` (now defensive `TryParse`).
- **CA2017 logging bug** in `InvoiceDetails2.OnPutCancelDocumentAsync` (parameter/placeholder mismatch).

### Security

- **IDOR fixed across 17 invoice endpoints** — invoice view, PDF download, history export, submit, delete, cancel, and reject now enforce TIN ownership via `UserExtensions.CanAccessInvoiceAsync` / `CanAccessInvoiceByUuidAsync`. A user can only access documents belonging to their company's TIN(s); Admins can access all. (Previously any authenticated user could enumerate sequential invoice numbers and read/act on other companies' documents.)
- **Secrets removed from `appsettings.json`** (DB passwords, LHDN client secrets, cert password, SMTP, Turnstile) — supplied via user-secrets / environment variables.
- **API body logging hardened** — `LogApiTransaction` logs metadata only at Information; full request/response bodies only at Debug, after a `Redact()` pass that strips bearer/`access_token` values and masks IC numbers.
- **Client-side rate limiting** protects all LHDN calls from tripping server-side limits.

### Removed

- **Commercial-licensed packages replaced with FOSS**: **EPPlus → ClosedXML (MIT)** for the invoice Excel export; **SixLabors.ImageSharp → Magick.NET (Apache-2.0)** for image resize/WebP.
- **7 unused NuGet packages**: OpenTK, CopilotDev.NET.Api, Microsoft.EntityFrameworkCore.Sqlite, Razor.Templating.Core, X.PagedList, X.PagedList.Mvc.Core, X.Web.PagedList, plus the legacy `toastr` package (which transitively pulled the vulnerable jQuery 1.6.3).
- **5 orphan code files**: `IItemService`, `UserService`, `JsonUtils`, and the unused custom `EmailSender` / `IEmailSender` pair (the framework `IEmailSender` is used; real mail goes via `EmailService`).
- Dead commented-out code, the dead `/documents` static-file guard, and stray build/scaffolding artifacts.

## 📅 2025-08-28

### Fixed

- **LHDN API to Database Synchronization**: Fixed missing background import functionality
  - **Problem**: LHDN API data wasn't automatically syncing to database - only status updates were working
  - **Root Cause**: Background service (`InvoiceStatusUpdater`) only handled status updates, never called import functionality
  - **Solution**: Added automatic LHDN import to background service (`InvoiceStatusUpdater.cs:68-73, 209-266`)
    - **Import Schedule**: Runs every 5 background cycles to avoid API overload
    - **User Company Discovery**: Automatically finds all user company TINs to import for
    - **Complete Sync**: Uses existing `RunFullImportFromLhdnAsync` with full invoice header, lines, and tax sync
    - **Error Handling**: Comprehensive logging and per-TIN error isolation
  - **Impact**: LHDN API data now automatically flows to database without manual intervention
  - **Background Integration**: Seamlessly integrated with existing status update cycle

- **Universal EINV Invoice Numbering**: Fixed adjustment documents generating wrong prefixes (SCN, SDN, SRN)
  - **Problem**: Self-billed credit notes generated "SCN000001" instead of "EINV00001", breaking numbering consistency
  - **Root Cause**: Separate prefix generation logic for adjustment documents instead of using universal EINV numbering
  - **Solution**: Replaced custom prefix logic with `GenerateNextInvoiceNumber()` for all document types (`CreateInvoice.cshtml.cs:524-527`)
    - **Before**: SCN000001, SDN000001, SRN000001, CN000001, DN000001, RN000001
    - **After**: EINV00001, EINV00002, EINV00003 (universal sequential numbering)
  - **Impact**: All document types now use consistent EINV prefix numbering regardless of type
  - **System Consistency**: Maintains single sequential numbering across invoices, credit notes, debit notes, and self-billed variants

- **Removed Negative Symbol from Credit Note Unit Prices**: Improved UX by showing positive values in UI
  - **Problem**: Credit notes showed confusing negative unit prices (-1.00) which caused user confusion
  - **Manager Requirement**: Remove negative symbol from UI while maintaining correct credit note logic internally
  - **Solutions Applied**:
    - **Backend Display Logic**: Modified unit price display to use `Math.Abs()` - shows positive values in UI (`CreateInvoice.cshtml.cs:508`)
    - **JavaScript Validation**: Simplified validation to require positive prices for all document types (`CreateInvoice.cshtml:1920`)
    - **Input Validation**: Updated `updatePriceInputValidation()` to enforce positive validation universally (`CreateInvoice.cshtml:3265`)
    - **Credit Logic**: Document type (CN/DN/RN) handles credit nature internally, not through negative values
  - **Impact**: Better user experience - no confusing negative symbols, credit logic handled transparently
  - **Testing**: All document types now show positive unit prices, credit calculations handled by document type

- **Self-billed Credit Note Data Loading**: Fixed issue where original invoice data wasn't loading when creating Self-billed CN
  - **Problem**: Clicking "Create Self-billed CN" from invoice list didn't load original invoice data into the form
  - **Root Cause**: `SELF-CN` type wasn't included in adjustment document detection logic
  - **Solutions Applied**:
    - **Backend Fix**: Added `"SELF-CN", "SELF-DN", "SELF-RN"` to `adjustmentTypes` array in `CreateInvoice.cshtml.cs:146`
    - **Frontend Fix**: Added self-billed types to `urlRefUUIDTypes` array in `CreateInvoice.cshtml:3286`
  - **Impact**: Self-billed adjustment documents now properly load original invoice data (supplier, customer, lines, etc.)
  - **Testing**: Create Self-billed CN should now populate form with original invoice data

- **Invoice Lines Display in Self-billed CN**: Fixed invoice items not displaying in adjustment documents
  - **Problem**: Invoice lines weren't showing in the form when creating Self-billed CN from existing invoices
  - **Root Cause**: Missing `LineNumber` property and improper null handling in invoice line mapping
  - **Solution**: Enhanced invoice line mapping in `PopulateAdjustmentDocumentFromOriginalInvoice` method:
    - **Critical Fix**: Added `LineNumber = index + 1` for proper display (`CreateInvoice.cshtml.cs:503`)
    - **Null Safety**: Added null coalescing operators for all properties (ItemCode, UnitOfMeasure, etc.)
    - **Default Values**: Ensured default tax category and fallback values for missing data
  - **Impact**: All original invoice line items now display correctly in self-billed adjustment documents
  - **Testing**: Invoice lines with quantities, prices, and tax details should now appear in Self-billed CN form

- **Required Fields Validation in Self-billed CN**: Fixed "missing required fields" error for adjustment documents
  - **Problem**: Validation error "Items 1 have missing required fields (Quantity, Unit Price, Description, Classification, or Unit of Measure)" when creating Self-billed CN
  - **Root Cause**: Invalid UnitOfMeasure values ("Unit" instead of valid codes) causing validation failures
  - **Solutions Applied**:
    - **ItemDescription**: Enhanced to ensure non-empty content with fallback (`CreateInvoice.cshtml.cs:505`)
    - **Quantity**: Added null coalescing to default to 1 if original quantity is null (`CreateInvoice.cshtml.cs:506`)
    - **UnitOfMeasure**: Critical fix to replace invalid "Unit" values with "XUN" code (`CreateInvoice.cshtml.cs:507`)
  - **Technical**: Enhanced validation to check for `string.IsNullOrWhiteSpace(line.UnitOfMeasure) || line.UnitOfMeasure == "Unit"`
  - **Impact**: Self-billed CN creation now passes validation without requiring manual field editing
  - **Testing**: Create Self-billed CN should no longer show missing required fields validation error

- **LHDN Import Reliability Enhancement**: Applied open-source resilience patterns for robust API integration
  - **Problem**: API failures and database issues causing unreliable LHDN imports
  - **Solutions Applied**:
    - **Retry Pattern** (inspired by Polly): Added `GetAccessTokenWithRetry` and `SearchDocumentsWithRetry` methods with exponential backoff
    - **Unit of Work Pattern**: Wrapped database operations in atomic transactions with proper rollback handling
    - **Fault Isolation**: Per-TIN error handling to prevent single TIN failures from breaking entire import
    - **Enhanced Logging**: Comprehensive logging with emoji indicators for better debugging
  - **Files Modified**: `Pages/Invoices/InvoiceLists.cshtml.cs:604-815`
  - **Impact**: LHDN import now handles transient failures gracefully and ensures data consistency

- **LHDN Import Simplification**: Reverted to old working CreateInvoiceFromApi method for reliable sync
  - **Problem**: Complex invoice line and tax sync logic was causing import failures and database issues
  - **Solution**: Restored simple approach that focuses on invoice header sync only in `Pages/Invoices/InvoiceLists.cshtml.cs:704-756`
  - **Removed**: Complex `SyncInvoiceLinesAndTaxes` method and related database persistence logic
  - **Impact**: LHDN import should now work reliably for invoice headers as it did before

- **CRITICAL: LHDN API Endpoint Switch**: Fixed missing invoice lines and tax details by switching from `/details` to `/raw` endpoint
  - **Problem**: GET `/documents/{uuid}/details` endpoint only returns summary data without `InvoiceLine` arrays
  - **Solution**: Switched to GET `/documents/{uuid}/raw` endpoint in `Services/LHDNApiService.cs:412`
  - **Impact**: Now retrieves complete UBL document structure with all line items and tax details for proper database sync
  - **Technical**: The `/raw` endpoint returns full JSON document with `InvoiceLine` and `TaxTotal` arrays required for sync

- **Background Service Error Handling**: Fixed multiple error scenarios in `InvoiceStatusUpdater` background service
  - **NULL UUID Protection**: Added checks to skip invoices with empty/null UUIDs in `Services/Background/InvoiceStatusUpdater.cs:180-184`
  - **General TIN Filtering**: Added `GeneralTINHelper.IsGeneralTIN()` filtering to skip TINs that cannot get access tokens (Lines 173-177)
  - **Impact**: Eliminates "UUID cannot be null or empty" and "General TIN is not allowed" exceptions from background sync
  - **Log Improvements**: Added informative logging for skipped invoices with clear emoji indicators

### Added

- **Invoice Lines and Tax Sync from LHDN API**: Complete database synchronization implementation for imported invoices
  - **New Method**: `SyncInvoiceLinesAndTaxes()` in `Pages/Invoices/InvoiceLists.cshtml.cs` (Lines 2086-2190)
  - **Technical Details**:
    - Parses invoice lines from LHDN API document JSON using JArray/JObject parsing
    - Creates `InvoiceLine` entities with proper foreign key relationships (`InvoiceHeaderInvoiceNo`)
    - Sequential saves: Lines first to get InvoiceLineId PKs, then taxes with proper line references
    - Extracts tax information from TaxTotal/TaxSubtotal JSON structure with proper null handling
    - Comprehensive logging with emoji indicators for tracking progress and debugging
  - **Database Integrity**: Uses full namespace references (`eInvWorld.Models.InputModel.InvoiceLine`) to avoid compilation conflicts
  - **Error Handling**: Proper exception handling with transaction rollback support for failed sync operations
  - **Integration**: Modified `RefreshInvoicesFromApiAsync()` method to call sync after header save (Lines 662-668)

## 📅 2025-08-27

### Fixed

- **Import All Invoices from LHDN - Complete Fix**: Fixed General TIN blocking errors and corrected self-billed invoice import logic
  - **Root Cause Analysis**: 
    - InvoiceSync was attempting to get LHDN access tokens for General TINs (EI00000000010, etc.) which are blocked by TokenService design
    - The core issue was misunderstanding self-billed invoice flow in LHDN API - both regular and self-billed invoices are "Sent" by the submitting company
  - **Technical Solution**: Fixed in `Pages/Admin/InvoiceSync.cshtml.cs` and `Helpers/InvoiceSyncHelper.cs`
    - **General TIN Filtering**: Added `GeneralTINHelper.IsGeneralTIN()` filtering in `Pages/Admin/InvoiceSync.cshtml.cs` (Lines 39-67)
      - Prevents token request exceptions by filtering out General TINs before import attempts
      - Enhanced error messaging to distinguish between no companies vs only General TINs available
      - Shows informative summary of which General TINs were skipped during import
    - **Corrected Self-Billed Logic**: Updated `RunFullImportFromLhdnAsync()` method in `Helpers/InvoiceSyncHelper.cs` (Lines 313-361)
      - **Key Understanding**: Self-billed invoices are still "Sent" by the buyer (user company) who creates them
      - **Document Structure**: User TIN appears as receiver/customer, General TIN as issuer/supplier, but user TIN is the submitter
      - **Search Logic**: Uses existing `GetAllUuidsForTinAsync()` to find all documents submitted by user TIN (includes both regular and self-billed)
  - **Self-Billed Invoice Flow Clarification**:
    - **Regular Invoices (01-04)**: User TIN is both submitter and issuer/supplier  
    - **Self-Billed Invoices (11-14)**: User TIN is submitter and receiver/customer, General TIN is issuer/supplier
    - **Both Types**: Searched as documents "Sent" by the user company TIN
  - **User Impact**: 
    - "Import All Invoices from LHDN" now works without General TIN exceptions
    - Imports complete invoice history including both regular invoices (as supplier) and self-billed invoices (as customer)  
    - All invoices properly synced to database with correct TIN relationships maintained
    - Clear feedback about General TINs skipped and documents imported per company
  - **Database Synchronization**: All imported invoices sync via `InvoiceFullSyncHelper.SyncAllFromApiAsync()` with complete headers, lines, and tax details

- **LHDN Date Range Validation Error**: Fixed "Issue Date From should not exceed the maximum search range of last 2 years from today" error
  - **Root Cause**: `GetAllUuidsForTinAsync()` in `Services/LHDNApiService.cs` was hardcoded to search from 2023-08-01, but LHDN only allows searching within last 2 years
  - **Technical Fix**: Updated date range logic in `LHDNApiService.cs` (Line 435)
    - Changed from fixed `new DateTime(2023, 8, 1)` to dynamic `DateTime.Today.AddYears(-2).AddDays(1)`
    - Added 1 day buffer to avoid boundary issues with LHDN API validation
    - Ensures compliance with LHDN's 2-year maximum search range policy
  - **Impact**: "Import All Invoices from LHDN" now works within LHDN's date range restrictions and successfully retrieves invoice data

- **Background Sync to Database Issues**: Fixed critical issues preventing LHDN invoice data from being properly synced to local database
  - **Root Cause Analysis**: Multiple critical issues in `InvoiceFullSyncHelper.SyncAllFromApiAsync()` were preventing data sync:
    - **Model-Database Schema Mismatch**: InvoiceLine model was missing required `InvoiceHeaderInvoiceNo` foreign key property that exists in database
    - **Incorrect Foreign Key Assignment**: Code was trying to use non-existent `InvoiceHeaderId` instead of correct `InvoiceNo` primary key
    - **Missing Data Validation**: Null values were causing database insertion failures
    - **Poor Error Handling**: Sync failures were not properly logged or handled
  - **Technical Solution**: Comprehensive overhaul of `Helpers/InvoiceFullSyncHelper.cs` (Lines 25-212)
    - **Fixed Model Schema**: Added missing `InvoiceHeaderInvoiceNo` property to `Models/InputModel/InvoiceLine.cs` (Line 16) to match database schema
    - **Corrected Foreign Key Relationships**: Updated all references to use `InvoiceHeaderInvoiceNo` instead of non-existent `InvoiceHeaderId`
    - **Enhanced Data Flow**: Fixed supplier/customer parsing to occur before InvoiceHeader creation to ensure proper foreign key assignment
    - **Added Null Safety**: Added null coalescing operators (`??`) for all database fields to prevent insertion failures
    - **Comprehensive Error Handling**: Added try-catch blocks with detailed logging for sync failures
    - **Enhanced Logging**: Added detailed logging for each sync step (header creation, line addition, tax processing)
  - **Database Synchronization Improvements**:
    - InvoiceHeaders now properly sync with all LHDN fields (UUID, SubmissionID, status, dates, amounts)
    - InvoiceLines correctly link to headers using `InvoiceHeaderInvoiceNo` foreign key
    - InvoiceTaxes properly associate with their parent lines
    - Transaction rollback on any failure ensures data consistency
  - **User Impact**: 
    - LHDN import now successfully saves all invoice data to database instead of silently failing
    - Invoice lists and details pages will show imported LHDN data correctly
    - Background sync no longer fails silently - errors are properly logged for debugging
    - Data integrity is maintained with proper transaction handling

- **Namespace Compilation Errors**: Fixed compilation errors preventing project build success
  - **Root Cause**: Namespace conflicts between System.IO and EINVWORLD.Pages.Admin.System causing compilation failures
  - **Files Fixed**: 
    - `Pages/Admin/Resources/Manage.cshtml.cs`: Added System.IO using directive and used global::System.IO.File references to avoid namespace conflicts
    - `Pages/Admin/Resources/Edit.cshtml.cs`: Added System.IO using directive and used global::System.IO.File references for file operations
    - `Pages/Admin/Resources/Create.cshtml.cs`: Added System.Diagnostics using directive for Debug.WriteLine calls
  - **Impact**: Project now builds successfully without compilation errors, enabling "Import All Invoices from LHDN" functionality to work properly
  - **Resolution Method**: Used fully qualified `global::System.IO.File` references to bypass namespace conflicts caused by local System folder
- **PDF Download Functionality in Invoice Lists**: Fixed "download pdf from invoice list not working" issue where PDF button was only reloading the page instead of downloading actual PDF files
  - **Root Cause**: InvoiceLists was using direct link to `asp-page="/Invoices/PdfTemplate_v2"` which only renders HTML instead of generating downloadable PDF
  - **Complete Fix**: Added proper PDF generation handler to InvoiceListsModel (Lines 101-135)
    - `OnGetDownloadPdfAsync(string invoiceNo)` method uses PDFGeneratorService with PdfTemplate_v2 template
    - Generates PDF file using `_pdfGeneratorService.GeneratePdfAsync(invoiceNo)` method
    - Returns actual PDF file download with proper MIME type (`application/pdf`)
    - Includes comprehensive error handling and logging for troubleshooting
    - File naming convention: `Invoice_{invoiceNo}.pdf` for clear identification
  - **Frontend Fix**: Updated PDF download link from `asp-page="/Invoices/PdfTemplate_v2"` to `?handler=DownloadPdf&invoiceNo=@invoice.InvoiceNo` (Line 868)
  - **User Impact**: PDF download button in invoice lists now properly downloads PDF files instead of opening blank pages
  - **Template Consistency**: Uses latest PdfTemplate_v2 template ensuring consistent PDF formatting across all pages
  - **Files Modified**: `Pages/Invoices/InvoiceLists.cshtml.cs` (added OnGetDownloadPdfAsync handler method), `Pages/Invoices/InvoiceLists.cshtml` (updated download link)

- **Table Horizontal Scrolling Empty Space Issue**: Fixed empty space appearing when scrolling to the right side of invoice list table
  - **Root Cause**: Conflicting fixed column widths in `<colgroup>` were interfering with responsive table behavior and minimum width calculations
  - **Complete Fix**: Replaced rigid colgroup structure with flexible CSS-based approach (Lines 526-546)
    - Removed conflicting `<colgroup>` with fixed pixel widths that caused layout issues
    - Added `table-layout: fixed` with `min-width: 1800px` for consistent table sizing
    - Implemented CSS-based column width control using class selectors for better flexibility
    - Enhanced container styling with explicit `overflow-x: auto; width: 100%` for proper scrolling
    - Included missing `enhanced-table-scroll.js` script for proper scroll behavior and shadow effects
  - **User Impact**: Table now scrolls properly without empty space, showing actual content when scrolling horizontally
  - **Files Modified**: `Pages/Invoices/InvoiceLists.cshtml` (Lines 526-546, 1213)

- **Sticky Column Header-Data Misalignment**: Fixed critical issue where Invoice No data column was frozen but header was not, causing visual misalignment during horizontal scrolling
  - **Root Cause**: Existing CSS in `custom.min.css` was being overridden or not applying properly to table headers, causing only data cells to be sticky
  - **Complete Fix**: Added explicit sticky column positioning with enhanced styling (Lines 548-567)
    - **Checkbox Column**: `position: sticky; left: 0; z-index: 10` with proper background and borders
    - **Invoice No Column**: `position: sticky; left: 48px; z-index: 9` with optimal spacing and visual separation
    - **Enhanced Visual Separation**: Added `box-shadow: 2px 0 5px rgba(0,0,0,0.1)` for clear distinction between sticky and scrolling content
    - **Proper Padding**: `padding-left: 12px; padding-right: 8px` ensures text has comfortable spacing and doesn't touch column borders
    - **Z-Index Layering**: Checkbox (z-index: 10) > Invoice No (z-index: 9) for proper stacking order
  - **User Impact**: Both header and data cells now stay frozen together during horizontal scrolling, maintaining perfect alignment
  - **Professional Appearance**: Clean visual separation with shadows and proper padding for enhanced readability
  - **Files Modified**: `Pages/Invoices/InvoiceLists.cshtml` (Lines 548-567)

- **Critical Cancel Button Database Update Issue**: Fixed issue where LHDN API cancellation succeeded but local database status was not updated due to email validation failures
  - **Root Cause**: Email validation and sending errors were preventing `SaveChangesAsync()` from being called, leaving database in outdated state despite successful LHDN API call
  - **Critical Flow Issue**: Database update was positioned after email operations, causing failures in email validation/sending to block database updates entirely
  - **Complete Fix**: Restructured operation flow to prioritize database consistency (Lines 1553-1601)
    - **Database First**: Moved `SaveChangesAsync()` before email operations to ensure status update regardless of email issues
    - **Enhanced Error Handling**: Added separate try-catch blocks for database operations vs email operations
    - **Email Failure Tolerance**: Changed email validation failures from `BadRequest`/`StatusCode(500)` returns to warning logs and graceful skipping
    - **Comprehensive Logging**: Added detailed logging with emojis (💾✅⚠️) to track database vs email operation success/failure
  - **Technical Implementation**:
    - Database update now happens immediately after LHDN API success and invoice status changes
    - Email notifications become optional post-processing that doesn't affect core functionality
    - Missing customer/supplier emails no longer block database updates - just log warnings
    - Email service failures are captured but don't prevent successful completion response
  - **User Impact**: Cancel operations now correctly update invoice status in database even when email notifications fail
  - **LHDN Compliance**: Maintains proper synchronization between LHDN API status and local database status
  - **Files Modified**: `Pages/Invoices/InvoiceLists.cshtml.cs` (CancelDocumentAndSaveAsync method, Lines 1553-1601)

- **Critical Action Button Authorization Issue**: Fixed buyers seeing inappropriate cancel/reject buttons for invoices they received, violating LHDN business rules
  - **Root Cause**: Action buttons were based on generic `invoiceDirection` filter instead of actual user relationship to specific invoice
  - **Business Rule Violation**: Only invoice suppliers should be able to cancel their own invoices; only invoice buyers should be able to request rejection
  - **Previous Logic**: Used `invoiceDirection == "Received"/"Sent"` which showed buttons based on filter view, not actual user authorization
  - **Complete Fix**: Implemented proper authorization based on user's TIN matching invoice supplier/customer TIN (Lines 873-892)
    - **Request Reject Button**: Now only shows when `Model.UserTINs.Contains(invoice.Customer.TIN)` (user is actual buyer of this invoice)
    - **Cancel Button**: Now only shows when `Model.UserTINs.Contains(invoice.Supplier.TIN)` (user is actual supplier of this invoice)
    - **Added UserTINs Property**: Made user's company TINs available to Razor page for proper authorization checks (Line 69)
    - **Enhanced Security**: Prevents unauthorized actions by verifying actual business relationship to each invoice
  - **Technical Implementation**:
    - Added `List<string> UserTINs` property to InvoiceListsModel for authorization checks
    - Populated UserTINs from UserCompanies query in OnGetAsync method (Line 216)
    - Replaced generic direction-based logic with specific TIN-based authorization
    - Maintained all existing functionality while adding proper security controls
  - **LHDN Compliance**: Now properly enforces Malaysian LHDN business rules for invoice actions
  - **User Impact**: Buyers no longer see inappropriate cancel/reject buttons for invoices they receive from suppliers
  - **Files Modified**: `Pages/Invoices/InvoiceLists.cshtml.cs` (Lines 69, 216), `Pages/Invoices/InvoiceLists.cshtml` (Lines 873-892)

## 📅 2025-08-26

### Changed
- **PDF Download Template Update**: Updated InvoiceDetails2 PDF download to use latest template matching InvoiceLists approach
  - **Change**: Updated PDF download link from handler method to direct PdfTemplate_v2 page (Line 432)
  - **Previous**: Used `?handler=DownloadPdf&invoiceNo=` with backend PDF generation service
  - **New**: Uses `asp-page="/Invoices/PdfTemplate_v2" asp-route-InvoiceNo=` with direct page rendering
  - **Benefit**: Consistent PDF template across invoice list and invoice detail pages
  - **Template**: Now uses latest `PdfTemplate_v2.cshtml` template for both pages
  - **User Experience**: Opens PDF template in new tab (`target="_blank"`) for better usability
  - **Backend**: Kept existing PDF generation handler as backup for compatibility

### Fixed
- **CRITICAL: Invoice Detail Page Reject/Cancel API Endpoints**: Fixed critical 404 error where invoice detail page JavaScript was calling non-existent API endpoints
  - **Root Cause**: Invoice detail JavaScript was calling `/InvoiceLists?handler=RejectDocument` but from `/InvoiceDetails2` page, causing 404 errors because InvoiceDetails2 didn't have these handlers
  - **Error**: `Failed to load resource: the server responded with a status of 404 ()` when clicking reject/cancel buttons
  - **Complete Fix**: Added proper API handlers to InvoiceDetails2 page with identical functionality to InvoiceLists
    - **Backend Changes**:
      - Added `UserManager<ApplicationUser>` dependency to InvoiceDetails2 constructor (Line 52)
      - Added `Microsoft.AspNetCore.Identity` using statement (Line 14) 
      - Replaced queue-based methods with direct API methods matching InvoiceLists pattern
      - `OnPutRejectDocumentAsync(documentId, rejectionReason, tin)` - Direct LHDN API integration (Lines 215-271)
      - `OnPutCancelDocumentAsync(documentId, cancellationReason, tin)` - Direct LHDN API integration (Lines 274-318)
    - **Frontend Changes**:
      - Fixed URL routing issue: Changed from absolute `/InvoiceDetails2?handler=` to relative `?handler=` (Lines 180, 241)
      - **URL Issue**: InvoiceDetails2 page has route `@page "{uuid}"` so absolute URLs were being misinterpreted 
      - Added cancellation reasons loading from server data (Lines 91-100)
      - Simplified response handling to match direct API pattern (Lines 199-212, 260-273)
  - **API Response Handling**: Both methods return identical response format as InvoiceLists:
    - **Reject**: `{message: "Document rejection successfully processed."}`
    - **Cancel**: `{message: "Document cancellation successfully processed."}`
  - **Technical Implementation**:
    - Same user TIN resolution logic as InvoiceLists for proper LHDN authentication
    - Direct LHDN API calls without database update complications
    - Comprehensive error handling with proper logging and status codes
    - Built-in CSRF token validation and user authentication checks
  - **User Impact**: 
    - Reject and cancel buttons now work correctly from invoice detail page
    - No more 404 errors - endpoints exist and function properly
    - Same success/error messaging as invoice list for consistency
    - Proper LHDN API integration with correct authentication flow
  - **LHDN Compliance**: Ensures proper document rejection and cancellation workflow through proven API integration pattern

### Security
- **SECURITY FIX: RefUUID Dropdown Data Exposure**: Fixed security vulnerability in RefUUID dropdown selection while maintaining legitimate business functionality
  - **Vulnerability**: `GetInvoicesForReference` API endpoint in Pages/Invoices/CreateInvoice.cshtml.cs:1692-1791 was only filtering by supplier ID without verifying user access rights
  - **Risk**: Users could potentially access dropdown data showing invoices from other companies
  - **Business Logic Preserved**: Maintains ability for companies to reference external invoices they received (legitimate business practice for creating Credit Notes/Debit Notes)
  - **Technical Fix**: Added targeted security filtering:
    - **User Company Validation**: Lines 1701-1705 verify user has associated companies before proceeding
    - **Supplier Access Control**: Lines 1713-1717 verify requested supplier belongs to current user's companies 
    - **Audit Logging**: Lines 1758-1760 added security context logging for access tracking
  - **User Impact**: RefUUID dropdown now correctly shows only invoices from user's own suppliers, while still allowing manual RefUUID entry for external invoices
  - **Balance**: Security protection for dropdown data while preserving legitimate business workflows

### Enhanced
- **Enhanced RefUUID Selection UX**: Significantly improved RefUUID dropdown user experience with comprehensive invoice information and professional styling
  - **Rich Data Display**: Enhanced dropdown format shows: `INV001 | Customer Company Name | RM1,500.00 | ✅Valid | 2025-01-15`
  - **Status Visual Indicators**: Added emoji badges for invoice status (✅ Valid, 📤 Submitted, ❌ Cancelled, 📄 Other)
  - **Advanced Select2 Integration**: Implemented rich dropdown templates with:
    - **Card-like Layout**: Invoice number prominently displayed in eInvWorld brand green (`#3AA564`)
    - **Contextual Icons**: Building icon for customer, money icon for amount, calendar icon for date
    - **Enhanced Search**: Placeholder text guides users to "Type to search by invoice number, customer name, or amount..."
    - **Professional Styling**: Hover effects, consistent spacing, and brand-consistent colors
  - **Compact Selection Display**: Selected items show clean format: `INV001 - Customer Name (RM1,500.00)`
  - **Performance Optimized**: Limited to 50 recent invoices with proper ordering and efficient queries
  - **Files Modified**: 
    - `Pages/Invoices/CreateInvoice.cshtml.cs:1726-1762`: Enhanced server-side data with supplier/customer names and status
    - `Pages/Invoices/CreateInvoice.cshtml:4745-4827`: Rich frontend display with advanced Select2 templates
    - `Pages/Invoices/CreateInvoice.cshtml:4879-4913`: Custom CSS styling for professional appearance

### Fixed  
- **RefUUID External Entry Support**: Fixed Select2 limitation that prevented users from entering external RefUUIDs from invoices received from other companies
  - **Issue**: Select2 dropdown only allowed selection from predefined options, blocking users from entering RefUUIDs from invoices they received from external systems
  - **Business Impact**: Users couldn't create Credit Notes/Debit Notes referencing invoices from suppliers using hardcopy RefUUIDs
  - **Technical Solution**: Enhanced Select2 configuration to support custom entries:
    - **Tags Mode**: Enabled `tags: true` with custom `insertTag` function to allow RefUUID entry (minimum 10 characters)
    - **Smart Templates**: Different visual treatment for external vs internal RefUUIDs with contextual icons and colors
    - **User Guidance**: Updated placeholder to "Type to search or enter external RefUUID..." 
    - **Visual Distinction**: External RefUUIDs display with blue accent color (`#299cdb`) and external link icon
  - **User Experience**: Users can now both select from their own invoices AND manually enter RefUUIDs from external invoices
  - **UUID-Only Display**: ALL RefUUID entries (both internal and external) now display only raw UUID values for consistency and clarity
  - **Rich Dropdown Context**: While selection shows UUID only, dropdown still provides rich invoice information for better selection context
  - **Files Modified**: 
    - `Pages/Invoices/CreateInvoice.cshtml:4750-4755` (UUID-only option text with rich data in attributes)
    - `Pages/Invoices/CreateInvoice.cshtml:4823-4846` (Enhanced dropdown templates with UUID display)
    - `Pages/Invoices/CreateInvoice.cshtml:4853-4854` (UUID-only selection template)
  - **Styling**: `Pages/Invoices/CreateInvoice.cshtml:4939-4947` (Custom CSS for external RefUUID visual distinction)
  
- **RefUUID Recognition Bug Fix**: Fixed critical bug where manually entered existing UUIDs were incorrectly labeled as "External RefUUID from another system"
  - **Issue**: When users manually typed UUIDs that existed in their dropdown options, Select2 was creating new external tags instead of recognizing existing entries
  - **Root Cause**: Select2's matching algorithm and insertTag function were not properly handling case-insensitive UUID recognition
  - **User Impact**: Existing internal invoices were incorrectly marked as external references, causing confusion
  - **Technical Fix**: Enhanced Select2 configuration with:
    - **Smart Matcher**: Case-insensitive UUID matching that recognizes existing dropdown options (`CreateInvoice.cshtml:4799-4820`)
    - **Improved InsertTag**: Prevents creation of external tags for UUIDs that already exist in dropdown (`CreateInvoice.cshtml:4821-4833`)
    - **Accurate Recognition**: System now correctly identifies internal vs external UUIDs regardless of manual entry or dropdown selection
  - **Result**: Manually entered UUIDs that exist in user's dropdown now correctly display internal invoice information instead of external labels

- **RefUUID Logic Correction**: Reverted overly restrictive logic that was blocking legitimate external UUID entries
  - **Issue**: Previous fix was preventing creation of external RefUUID tags for UUIDs that exist in system but not in user's dropdown
  - **Problem**: Users couldn't manually enter external UUIDs that exist in the system (legitimate business use case)
  - **Solution**: Simplified approach allowing all manual UUID entries while maintaining enhanced matching for better user experience
  - **Current Behavior**: All manual RefUUID entries are now accepted, with improved case-insensitive search and matching
  - **Files Modified**: `CreateInvoice.cshtml:4822-4826` (simplified insertTag logic), `4799-4820` (enhanced matcher)

- **RefUUID Template Logic Fix**: Fixed final issue where manually entered existing UUIDs were still incorrectly labeled as "External"
  - **Issue**: Users typing existing UUIDs (like `6F65JWKH5WY53HSS5SAJGG3K10` for EINV00698) were seeing "External RefUUID from another system" instead of internal invoice details
  - **Root Cause**: Template logic was checking for `select2-tag` without verifying if the UUID actually exists in the dropdown options
  - **Solution**: Enhanced template logic to cross-check manually entered UUIDs against existing dropdown options before labeling as external
  - **Technical Fix**: Added smart detection in templateResult function (`CreateInvoice.cshtml:4836-4857`)
  - **Result**: Manually entered UUIDs that exist in dropdown now correctly show rich internal invoice information with company details and status

- **RefUUID Simplification**: Removed confusing external labels entirely - all RefUUID entries now display consistently
  - **Issue**: Complex logic for distinguishing "external" vs "internal" RefUUIDs was causing persistent labeling errors
  - **User Feedback**: "still same, maybe just remove the label" - indicating the external labeling was more confusing than helpful
  - **Solution**: Simplified approach removing all "External RefUUID from another system" labels and special styling
  - **Current Behavior**: 
    - **Dropdown Options**: Show rich internal invoice information (invoice number, customer, amount, status, date)
    - **Manual Entries**: Show just the UUID without any special labeling or formatting
    - **All Selections**: Display UUID only for consistency
  - **Files Modified**: 
    - `CreateInvoice.cshtml:4836-4837` (simplified template logic to just return UUID for manual entries)
    - `CreateInvoice.cshtml:4959` (removed external-specific CSS styling)
  - **Result**: Clean, consistent RefUUID interface without confusing labels

### Enhanced
- **RefUUID Highlighting System**: Added visual highlighting to distinguish existing vs new RefUUID entries in dropdown
  - **User Request**: "if uuid exist, highlight in select, now when i open the select, i not know which existing"
  - **Problem**: Users couldn't easily identify which manually entered UUIDs already exist in their system vs completely new entries
  - **Solution**: Implemented dual highlighting system with distinct visual indicators:
    - **Existing UUIDs**: Green highlight with checkmark icon and "✓ Exists in your invoices" label
      - Background: Light green (`#f0f8f4`) with green left border (`#3AA564`)
      - Icon: `ri-checkbox-circle-line` for recognition
    - **New UUIDs**: Orange highlight with add icon and "New RefUUID entry" label
      - Background: Light orange (`#fff8f0`) with orange left border (`#f59e0b`) 
      - Icon: `ri-add-circle-line` for new entries
  - **Enhanced Hover Effects**: Darker backgrounds on hover for better interactivity
  - **Files Modified**: 
    - `CreateInvoice.cshtml:4837-4860` (enhanced template logic with existence checking)
    - `CreateInvoice.cshtml:4983-4995` (CSS hover effects and highlighting styles)
  - **User Benefit**: Users can now instantly see which UUIDs they've used before vs completely new references when browsing the dropdown

## 📅 2025-08-25

### Fixed
- **Critical Draft Saving Routing Issue**: Fixed "Failed to save draft. Please try again." error caused by incorrect routing to template update logic instead of regular draft save functionality
  - **Root Cause**: Template detection logic in Pages/Invoices/InvoiceEdit.cshtml.cs:642-644 was incorrectly triggering on empty TemplateName form field, causing `saveDraft` actions to be routed to template update code path
  - **User Impact**: Users could not save invoice drafts because the system was trying to update templates instead of saving drafts
  - **Technical Fix**: Updated template detection condition from `Request.Form.ContainsKey("TemplateName") || !string.IsNullOrEmpty(Request.Form["TemplateName"])` to `(!string.IsNullOrEmpty(Request.Form["TemplateName"]) && Request.Form["TemplateName"] != "")` to only trigger on actual template operations
  - **Verification**: Console logs now show correct routing to draft save logic instead of "TEMPLATE UPDATE DETECTED" messages

- **Critical Issue Date Update Issue**: Fixed problem where draft saves were not respecting user's updated issue date, preventing submission of old drafts after date updates
  - **Root Cause**: SaveDraft method was ignoring user input for IssueDate in both new draft creation (line 969) and existing draft updates (missing from lines 929-953)
  - **User Impact**: When users updated old drafts with new issue dates, the system kept using old dates, causing 3-day validation failures ("invoice issue date is more than 3 days old")
  - **Technical Fixes**: 
    - **New Drafts**: Changed `IssueDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ...)` to `IssueDate = Invoice.IssueDate` at line 969
    - **Existing Drafts**: Added missing `draftInvoice.IssueDate = Invoice.IssueDate;` at line 938 in update logic
  - **User Workflow**: Users can now update old drafts by changing the issue date to a current date (within 3-day LHDN window) and successfully submit to LHDN

- **Standardized Success Dialog Styling**: Fixed inconsistent success dialog styling between CreateInvoice and InvoiceEdit forms by implementing consistent eInvWorld brand colors
  - **Issue**: Success dialogs in both forms had different appearances and used generic Bootstrap colors (`#28a745`) instead of eInvWorld brand colors
  - **User Impact**: Professional, consistent brand experience across all success dialogs and notifications
  - **Brand Consistency**: All success dialogs now use eInvWorld primary green (`#3AA564`) instead of generic Bootstrap green
  - **Files Modified**: 
    - `Pages/Invoices/CreateInvoice.cshtml`: Updated all SweetAlert confirmButtonColor, success notification backgrounds, and progress borders
    - `Pages/Invoices/InvoiceEdit.cshtml`: Updated all SweetAlert confirmButtonColor, success notification backgrounds, and progress borders
  - **Color Standardization**: All success elements now follow eInvWorld Brand Guidelines using `#3AA564` (Main Green) for primary actions and success states
  
- **Enhanced UUID/Submission UID Display Styling**: Added consistent visual styling for UUID and Submission UID display boxes in both CreateInvoice and InvoiceEdit success dialogs
  - **Issue**: UUID and Submission UID values in success dialogs had inconsistent visual presentation - some appeared as plain text while others had highlighted background boxes
  - **Solution**: Added comprehensive CSS rules to ensure both `code.text-primary` (Submission UID) and `code.text-info` (UUID) elements have consistent background colors, borders, padding, and typography
  - **Visual Enhancement**: 
    - **Submission UID**: Light green background `rgba(58, 165, 100, 0.1)` with eInvWorld brand green text `#3AA564`
    - **UUID**: Light blue background `rgba(41, 156, 219, 0.1)` with info blue text `#299cdb`
    - **Consistent styling**: Rounded corners, subtle borders, proper padding, and readable font weight
  - **Files Enhanced**: Both `Pages/Invoices/CreateInvoice.cshtml` and `Pages/Invoices/InvoiceEdit.cshtml` now have identical styling for success dialog code elements

- **Complete Success Dialog Template Standardization**: Final fix to ensure both CreateInvoice and InvoiceEdit forms have absolutely identical success dialog appearance matching the reference template
  - **Missing Template Elements**: InvoiceEdit was missing the complete detail-item card styling with left border accents that CreateInvoice had
  - **Card-like Appearance**: Added `.swal-submission-success .detail-item` styling with white background, rounded corners, padding, and left border accent
  - **Contextual Left Border Colors**: 
    - **Submission UID cards**: Green left border (`#3AA564`) to match eInvWorld brand
    - **UUID cards**: Blue left border (`#299cdb`) for visual distinction
  - **Professional Layout**: Added proper spacing, last-child margin removal, and card-like elevation for a polished appearance
  - **Template Reference Match**: Both forms now exactly match the CreateInvoice reference template shown in user's screenshots
  - **CSS Implementation**: Comprehensive styling rules ensure visual consistency across all success dialog elements including both the container cards and the individual code value displays
  - **Text Alignment Standardization**: Fixed text alignment to ensure **Submission UID** and **UUID** labels are consistently left-aligned within their card containers, matching the reference CreateInvoice template exactly
    - Added `text-align: left !important` to `.swal-submission-success .detail-item` elements
    - Ensured `<strong>` labels (`Submission UID:` and `UUID:`) are properly left-aligned with `display: block` and consistent bottom margin
    - Both CreateInvoice and InvoiceEdit now have identical left-aligned text layout for professional consistency

- **Copy Button Styling Standardization**: Added missing copy button styling to InvoiceEdit success dialogs to match CreateInvoice template exactly
  - **Missing Styling Issue**: InvoiceEdit had copy button functionality but was missing the visual styling that CreateInvoice had
  - **Complete Button Styling**: Added comprehensive `.copy-btn` CSS rules including default state, hover effects, and copied success state
  - **Visual Features**: 
    - **Default State**: Light gray background (`#f8f9fa`) with subtle borders for professional appearance
    - **Hover Effects**: Darker background (`#e9ecef`) with smooth transitions for better user experience
    - **Success Feedback**: Blue highlight (`#d1edff`) with check icon when copy succeeds for clear visual confirmation
  - **Functionality Match**: Both forms now have identical copy button appearance and interaction feedback
  - **Template Compliance**: Copy buttons now match the reference CreateInvoice template styling exactly with proper positioning and visual hierarchy

- **Invoice List Navigation Standardization**: Fixed self-billed invoice navigation to use the new CreateInvoice form with consistent EINV prefix numbering
  - **Issue**: Self-billed invoices (DocTypeCode "11") in invoice lists were linking to old CreateSBCN form instead of the new unified CreateInvoice form
  - **User Impact**: Clicking "Create Self-billed CN" from invoice lists now uses the standardized CreateInvoice form with proper EINV prefix continuation
  - **Technical Fix**: Updated InvoiceLists.cshtml line 829 to route self-billed invoice creation through `/Invoices/CreateInvoice?type=SELF-CN` instead of old `/Invoices/CreateSBCN`
  - **Enhanced Options**: Added complete set of self-billed document type options (SELF-CN, SELF-DN, SELF-RN) from invoice list dropdown actions
  - **Prefix Consistency**: All document types created from invoice lists now use the same EINV numbering sequence regardless of document type (02=CN, 03=DN, 04=RN, 12=SELF-CN, 13=SELF-DN, 14=SELF-RN)

### Enhanced
- **Enhanced Error Diagnostics for Draft Saving**: Added comprehensive logging to SaveDraft method in Pages/Invoices/InvoiceEdit.cshtml.cs:1033-1109 to identify exact failure points (database vs file operations) with detailed error messages and inner exceptions for better debugging of draft save issues
  - **Database Operation Logging**: Separate try-catch block around `_context.SaveChanges()` with detailed exception logging
  - **File Operation Logging**: Separate try-catch block around file system operations including directory creation and JSON file writing
  - **Enhanced Exception Details**: Captures both main exception messages and inner exception details for comprehensive error diagnosis
  - **Debugging Support**: Console and logger output for tracking exact failure points during draft save operations
  - **File Location**: Enhanced SaveDraft method at lines 1033-1109 in InvoiceEdit.cshtml.cs

## 📅 2025-08-21

### Added
- **RefUUID Field for Credit Notes (CN Document Type)**: Implemented comprehensive RefUUID functionality for Credit Note creation with proper LHDN compliance
  - **Frontend Implementation**: Added dynamic RefUUID field that shows only when document type "02" (Credit Note) is selected
  - **Key Features**:
    - Smart dropdown field visibility based on document type selection (Lines 848-860 in CreateInvoice.cshtml)
    - Dynamic loading of available invoices from same supplier for reference selection
    - Select2 integration with search and filtering capabilities
    - Automatic field clearing when supplier changes or document type changes to non-CN
  - **Backend API**: New GetInvoicesForReference handler method (Lines 1499-1535 in CreateInvoice.cshtml.cs)
    - Filters only valid, non-draft invoices from selected supplier
    - Excludes existing Credit Notes to prevent circular references
    - Returns formatted invoice data with invoice number, date, and amount for easy selection
    - Security validation: Only returns invoices from user's accessible suppliers
  - **JavaScript Integration**: 
    - handleDocTypeChange function enhanced to show/hide RefUUID field (Lines 2156-2175)
    - loadAvailableInvoicesForReference function for dynamic invoice loading (Lines 4525-4584)
    - Supplier change handler updated to reload RefUUID options when CN is selected (Lines 2446-2452)
  - **LHDN Compliance**: Ensures proper reference linking required for Credit Note submissions to Malaysian tax authority
  - **Business Impact**: Enables proper Credit Note creation with mandatory original invoice references per LHDN regulations

- **Credit Note Pre-Population from Existing Invoice**: Implemented complete "Create CN" workflow for automatic invoice detail copying
  - **Create CN URL Parameters**: Enhanced OnGetAsync method to accept invoiceNo, uuid, and type parameters for CN creation (Line 107)
  - **Pre-Population Logic**: New PopulateCreditNoteFromOriginalInvoice method (Lines 390-493 in CreateInvoice.cshtml.cs)
    - Automatically sets RefUUID to reference original invoice UUID for LHDN compliance
    - Copies all invoice details: supplier, customer, currency, payment terms, line items
    - Generates negative amounts for credit note (negative unit prices, negative tax amounts)
    - Auto-generates sequential CN numbers (CN000001, CN000002, etc.)
    - Preserves original line item descriptions with "Credit for:" prefix
    - Maintains proper tax category and percentage mapping between InvoiceTax and InvoiceTaxView models
  - **Integration with Invoice Lists**: Existing "Create CN" buttons in InvoiceLists.cshtml now properly trigger pre-population
    - URL pattern: `/Invoices/CreateInvoice?invoiceNo={number}&uuid={uuid}&type=CN`
    - Seamless workflow from invoice list → create CN → pre-populated form
  - **Model Compatibility**: Fixed property mapping issues between database models and view models
    - Corrected InvoiceLine.ItemDescription vs Description property usage
    - Fixed nullable int conversion for SupplierId and CustomerId (Lines 420-421)
    - Proper TaxCategory mapping between InvoiceTax and InvoiceTaxView
  - **User Experience**: Complete automation of CN creation reduces manual data entry and eliminates errors
  - **LHDN Compliance**: Ensures Credit Notes maintain proper reference relationships with original invoices per Malaysian tax regulations

### Fixed
- **CRITICAL: Wrong Document Type Code for Credit Notes**: Fixed incorrect document type assignment where Credit Notes were using "03" (Debit Note) instead of "02" (Credit Note)
  - **Root Cause**: Credit Note creation was setting document type to "03" which corresponds to "Debit Note" per LHDN official document types
  - **Proper LHDN Document Types**: "01"=Invoice, "02"=Credit Note, "03"=Debit Note, "04"=Refund Note
  - **Key Fixes**:
    - Changed PopulateCreditNoteFromOriginalInvoice to set DocTypeCode = "02" (Line 413 in CreateInvoice.cshtml.cs)
    - Updated JavaScript Credit Note detection from '03' to '02' (Lines 2158, 2448, 3170 in CreateInvoice.cshtml)
    - Updated CHANGELOG.md documentation to reflect correct document type
  - **User Impact**: "Create CN" workflow now correctly shows "Credit Note" instead of "Debit Note" in the document type dropdown
  - **LHDN Compliance**: Ensures proper document type submission to Malaysian tax authority API

- **RefUUID Dropdown Loading Issue**: Fixed "No results found" in RefUUID dropdown for Credit Note creation
  - **Root Cause**: GetInvoicesForReference API was using overly restrictive filters (LHDNStatusId == "Valid") and incorrect draft status check
  - **Backend Fixes**:
    - Simplified invoice filtering to show non-draft regular invoices (DocTypeCode == "01") for reference
    - Fixed draft status check from `i.IsDraft` to `i.InternalStatusId != "Draft"` (Line 1621)
    - Removed LHDN status requirement to allow referencing submitted but not yet validated invoices
  - **Frontend Improvements**: 
    - Increased JavaScript timeout from 500ms to 1500ms for supplier dropdown loading (Line 3178)
    - Added supplier ID logging for better debugging of API calls
  - **User Impact**: RefUUID dropdown now properly loads available invoices for Credit Note reference selection
  - **Business Value**: Enables proper Credit Note creation workflow with reference invoice selection

- **Auto-Selection of RefUUID for Credit Notes**: Implemented intelligent auto-selection of original invoice when creating CN from existing invoice
  - **Create CN from Invoice Lists**: When user clicks "Create CN" from an existing invoice, the RefUUID dropdown automatically selects the original invoice
  - **Manual Selection Support**: When creating CN from new form, users can manually select any available invoice from the RefUUID dropdown
  - **Smart Detection Logic**: 
    - JavaScript detects pre-set RefUUID value from backend Credit Note pre-population
    - Automatically selects matching option in dropdown after invoices are loaded
    - Integrates seamlessly with Select2 dropdown functionality
  - **Enhanced User Experience**: 
    - Eliminates manual RefUUID selection when creating CN from specific invoice
    - Maintains flexibility for manual selection in new CN forms
    - Provides visual confirmation of auto-selected invoice
  - **Console Debugging**: Added detailed logging for RefUUID detection and auto-selection process
  - **Enhanced User Experience Features**:
    - Form validation automatically updated when RefUUID is auto-selected
    - Visual "Auto-selected" badge appears when RefUUID is pre-filled from original invoice
    - Select2 dropdown properly initialized with pre-selected value
    - No manual interaction required when creating CN from existing invoice
  - **User Impact**: Streamlined Credit Note creation workflow with automatic reference setup and clear visual feedback

- **Final RefUUID Auto-Selection Debug Fix**: Fixed remaining Credit Note generation query that was still using old document type "03"
  - **Root Cause**: The `PopulateCreditNoteFromOriginalInvoice` method was still querying for existing Credit Notes using document type "03" instead of "02"
  - **Critical Fix**: Updated CN number generation query from `DocTypeCode == "03"` to `DocTypeCode == "02"` (Line 462 in CreateInvoice.cshtml.cs)
  - **Enhanced Logging**: Added comprehensive debug logging to track RefUUID value setting and Credit Note population process
    - Backend logs RefUUID value before and after setting to verify correct assignment
    - OnGetAsync method logs Credit Note creation process with UUID and invoice number parameters
    - Complete audit trail for debugging RefUUID auto-selection workflow
  - **User Impact**: RefUUID auto-selection now works correctly when creating Credit Notes from existing invoices
  - **LHDN Compliance**: Ensures proper Credit Note numbering sequence and correct document type classification
  - **Verification Confirmed**: Successfully tested with screenshot evidence showing RefUUID properly set to original invoice UUID
  - **Clean-up**: Removed temporary debug panel after successful testing and verification

- **CRITICAL: RefUUID Field Not Displaying in Form**: Fixed issue where RefUUID field was not visible on Credit Note forms despite backend values being set correctly
  - **Root Cause**: JavaScript Credit Note detection was only checking dropdown value, but ASP.NET model binding and Select2 initialization caused timing issues
  - **Comprehensive Fix**: Enhanced DOMContentLoaded handler with triple detection method:
    - **Method 1**: Check document type dropdown value for "02"
    - **Method 2**: Check backend model DocTypeCode property directly 
    - **Method 3**: Check URL type parameter for "CN"
  - **Timing Enhancement**: Added 500ms delay to handle Select2 initialization timing issues
  - **Debug Logging**: Added comprehensive console logging to track Credit Note detection process
  - **Key Changes**: 
    - Lines 3183-3196: Multi-method Credit Note detection logic in CreateInvoice.cshtml
    - Lines 3201-3211: Enhanced timing handling with setTimeout for Select2 compatibility
  - **User Impact**: RefUUID field now properly displays and functions when creating Credit Notes from existing invoices
  - **Testing**: Verified field visibility works with all three detection methods for maximum reliability

- **RefUUID Display Mode Enhancement**: Implemented intelligent dual-mode RefUUID field display for optimal user experience
  - **Root Cause**: Users were confused seeing dropdown selection when RefUUID should show actual UUID value from selected invoice
  - **Smart Display Logic**: RefUUID now shows different modes based on context:
    - **Auto-Display Mode**: When creating CN from existing invoice → Shows UUID as read-only text field with "Auto-selected from original invoice" badge
    - **Manual Selection Mode**: When creating CN from scratch → Shows dropdown to select from available invoices
  - **Technical Implementation**:
    - **Dual HTML Structure**: Two separate form elements (Lines 855-864: display mode, Lines 867-878: select mode)
    - **Intelligent Mode Detection**: JavaScript checks if RefUUID is pre-set to determine which mode to show (Lines 3211-3232)
    - **Form Submission Handling**: Hidden input field ensures proper form submission for both modes
    - **Event Handling**: Added dropdown change handler to sync selected UUID with hidden field (Lines 4737-4764)
  - **UX Improvements**:
    - **Clear Visual Distinction**: Auto-selected UUIDs show as locked text with success badge
    - **Manual Selection**: Dropdown with Select2 integration for searching available invoices
    - **Consistent Validation**: Both modes integrate with form validation system
  - **User Impact**: 
    - **Clarity**: Users immediately see the actual UUID when auto-selected from original invoice
    - **Flexibility**: Manual selection still available when creating CN from scratch
    - **Reduced Confusion**: No more dropdown showing when value is already determined
  - **LHDN Compliance**: Maintains proper RefUUID submission for both automated and manual Credit Note creation workflows

- **RefUUID Display Flicker Fix**: Eliminated page load flicker where text field briefly shows before switching to dropdown
  - **Root Cause**: Client-side JavaScript was determining display mode after page load, causing visual flicker between modes
  - **Server-Side Solution**: Moved display mode logic to server-side Razor rendering for immediate correct display
  - **Technical Implementation**:
    - **Server-Side Logic**: Added C# code block (Lines 853-861) to determine correct mode during HTML generation
    - **Conditional Rendering**: Both RefUUID section visibility and mode selection determined at render time
    - **State Variables**: `showDisplayMode` and `showSelectMode` calculated based on RefUUID presence and document type
    - **JavaScript Optimization**: Updated client-side code to respect server-rendered state instead of overriding it
  - **Performance Improvement**: Eliminates JavaScript delay and display changes, providing instant correct UI
  - **Key Logic**: 
    - **Display Mode**: `hasPresetRefUUID && (isCreditNote || isUrlCN)` - Shows read-only UUID field
    - **Select Mode**: `!hasPresetRefUUID && (isCreditNote || isUrlCN)` - Shows dropdown for manual selection
  - **User Impact**: 
    - **Instant Correct Display**: No more flicker or mode switching during page load
    - **Smoother Experience**: UI appears correctly from first render
    - **Reduced Confusion**: Users immediately see the appropriate interface for their context
  - **Backward Compatibility**: JavaScript still handles dynamic mode changes when users manually change document type

- **CORRECTED: RefUUID Support for All Adjustment Document Types**: Corrected RefUUID functionality with proper understanding of LHDN self-billed document hierarchy
  - **Complete Coverage**: RefUUID field now shows for ALL adjustment document types: CN, DN, RN, Self-CN, Self-DN, Self-RN
  - **Proper LHDN Document Hierarchy**: 
    - **Original Documents (No RefUUID)**: Invoice=01, Self-billed Invoice=11
    - **Adjustment Documents (Need RefUUID)**: CN=02, DN=03, RN=04, Self-CN=12, Self-DN=13, Self-RN=14
    - **Reference Logic**: Types 12,13,14 reference type 11 UUID; Types 02,03,04 reference type 01 UUID
  - **Enhanced Backend Logic**:
    - **Universal Method**: Renamed `PopulateCreditNoteFromOriginalInvoice` to `PopulateAdjustmentDocumentFromOriginalInvoice` (Lines 431-540)
    - **Complete Document Type Mapping**: All adjustment types supported (CN=02, DN=03, RN=04, Self-CN=12, Self-DN=13, Self-RN=14)
    - **Smart Amount Logic**: CN/RN/Self-CN/Self-RN use negative amounts, DN/Self-DN use positive amounts
    - **Prefix Generation**: Auto-generates appropriate prefixes (CN, DN, RN, SCN, SDN, SRN) with sequential numbering
    - **Self-Billed Support**: Added complete support for self-billed adjustment documents with proper prefixes and descriptions
  - **Frontend Enhancements**:
    - **Complete Field Detection**: RefUUID field shows for all adjustment document types including self-billed (Lines 857-864)
    - **Updated JavaScript**: `handleDocTypeChange` correctly identifies all adjustment types (02,03,04,12,13,14) as RefUUID-required (Lines 2194-2201)
    - **Server-Side Logic**: Proper visibility logic includes self-billed adjustment documents in RefUUID requirement (Lines 853-861)
  - **Invoice Lists Integration**: 
    - **Extended Action Buttons**: Added "Create DN" and "Create RN" buttons alongside existing "Create CN" (InvoiceLists.cshtml)
    - **Consistent URL Pattern**: All adjustment types use same parameter structure: `?invoiceNo={number}&uuid={uuid}&type={type}`
    - **Future Enhancement**: Foundation laid for self-billed adjustment document creation from invoice lists
  - **User Experience**: 
    - **Complete Workflow**: RefUUID field appears for all adjustment documents (02,03,04,12,13,14)
    - **Self-Billed Clarity**: Only self-billed invoice (11) treated as original document without RefUUID
    - **Automatic Pre-Population**: All adjustment types auto-populate from original invoice with proper RefUUID assignment
    - **Manual Selection**: Users can manually select RefUUID when creating adjustment documents from scratch
  - **LHDN Compliance**: Complete support for all LHDN adjustment document types with proper reference linking
  - **Business Logic Correction**: Proper understanding that 12,13,14 are self-billed adjustments referencing type 11, not original documents

- **CRITICAL: Self-Billed Invoice LHDN API Submission TIN Error**: Fixed critical issue where self-billed invoice submissions were using incorrect TIN for LHDN API authentication
  - **Root Cause**: System was using logged-in user's TIN (`_tokenService.GetUserAssignedTINAsync()`) for all document submissions, causing "authenticated TIN and documents TIN is not matching" error for self-billed invoices
  - **LHDN API Requirement**: For self-billed invoices (document types 11,12,13,14), LHDN API requires authentication using the buyer's TIN (customer), not supplier's TIN
  - **Complete Fix**: Enhanced OnPostSubmitDocumentsAsync method in CreateInvoice.cshtml.cs (Lines 1126-1156):
    - **Smart TIN Selection**: Automatically determines correct TIN based on document type
    - **Self-Billed Logic**: Document types 11,12,13,14 use `fullInvoice.Customer?.TIN` for API authentication
    - **Regular Invoice Logic**: All other document types use `fullInvoice.Supplier?.TIN` for API authentication
    - **Enhanced Validation**: Added comprehensive TIN validation with specific error messages for missing customer/supplier TINs
    - **Detailed Logging**: Added debug logging to track TIN selection process for better troubleshooting
  - **Technical Implementation**:
    - Replaced hardcoded user TIN lookup with dynamic document-type-based TIN selection
    - Added database query with proper includes for Supplier and Customer data
    - Implemented fail-safe validation to prevent submissions without required TINs
  - **Error Resolution**: Eliminates "BadRequest - ValidationError: The authenticated TIN and documents TIN is not matching" for self-billed invoice submissions
  - **User Impact**: Self-billed invoices (types 11,12,13,14) now submit successfully to LHDN API using correct buyer TIN
  - **LHDN Compliance**: Ensures proper TIN authentication for all document types per Malaysian tax authority requirements

- **CRITICAL: Self-Billed Invoice JSON Document TIN Mismatch**: Fixed critical issue where JSON documents for self-billed invoices contained incorrect customer TIN values
  - **Root Cause**: Customer dropdown was incorrectly allowing selection of General TINs for self-billed invoices, causing both supplier and customer sections in JSON to have the same General TIN (`EI00000000010`)
  - **JSON Document Structure Issue**: 
    - **Expected**: Supplier=General TIN (`EI00000000010`), Customer=User's company TIN
    - **Actual Problem**: Supplier=General TIN (`EI00000000010`), Customer=General TIN (`EI00000000010`) ❌
  - **Complete Fix**: Enhanced customer dropdown logic in OnGetAsync method (Lines 366-387):
    - **Self-Billed Logic**: For document types 11,12,13,14, exclude General TINs from customer dropdown
    - **Regular Logic**: For other document types, include General TINs in customer dropdown (maintains existing behavior)
    - **Proper Filtering**: Uses same logic as LoadCustomers API handler for consistency
  - **Technical Implementation**:
    - Added document type detection to determine customer dropdown contents
    - Implemented General TIN exclusion list (`EI00000000010`, `EI00000000020`, `EI00000000030`, `EI00000000040`)
    - Ensures customers can only select user companies for self-billed invoices
    - Prevents accidental selection of General TINs as customers in self-billed documents
  - **User Impact**: 
    - Self-billed invoice forms now only show valid customer options (user companies)
    - Eliminates user error that caused LHDN API "authenticated TIN and documents TIN is not matching" errors
    - Maintains proper JSON document structure with correct TIN values
  - **LHDN Compliance**: Ensures JSON documents contain proper party information structure required by Malaysian tax authority
  - **Error Resolution**: Eliminates the core cause of TIN validation errors in self-billed invoice submissions

- **CRITICAL: Restored Correct Self-Billed Invoice Processing Logic**: Fixed self-billed invoice TIN mapping by restoring original InvoiceMapper switching logic and removing conflicting logic from CreateInvoiceHeader
  - **Root Cause**: New CreateInvoice form was duplicating supplier/customer switching that was already handled by InvoiceMapper, causing double-switching and incorrect TIN assignments
  - **User Context**: User confirmed "originally, i switch at invoice mapper, and it work before with old invoice form. but when we do the new create invoice form, it not work now"
  - **Complete Fix**: Simplified logic by removing unnecessary switching and ensuring direct mapping:
    - **CreateInvoiceHeader (Lines 1376-1382)**: Assigns supplier/customer directly from form selections without switching logic
    - **InvoiceMapper (Lines 32-36)**: No switching needed - uses header values directly for JSON generation
    - **Logic Flow**: Form selections → CreateInvoiceHeader stores directly → InvoiceMapper maps directly to JSON
  - **Technical Implementation**:
    - **CreateInvoiceHeader**: Direct assignment: `invoiceHeader.Supplier = supplier; invoiceHeader.Customer = customer;`
    - **InvoiceMapper**: Direct mapping: `var supplier = header.Supplier; var customer = header.Customer;`
    - **Form Configuration**: Self-billed forms already configured to select General TIN as supplier, user company as customer
    - **Enhanced Debugging**: Added logging to track form selections through to JSON generation
  - **Self-Billed Invoice Flow**:
    - **Form Selection**: User selects General TIN as supplier, user company as customer
    - **CreateInvoiceHeader**: Stores General TIN as supplier, user company as customer (direct storage)
    - **InvoiceMapper**: Maps directly - General TIN appears in supplier section, user company in customer section
    - **Result**: JSON shows General TIN in AccountingSupplierParty, user company in AccountingCustomerParty ✅
  - **User Impact**: 
    - Self-billed invoices now process correctly with proper TIN mapping for LHDN API authentication
    - Maintains compatibility with original InvoiceMapper logic that worked with old invoice form
    - Eliminates "authenticated TIN and documents TIN is not matching" errors
  - **LHDN Compliance**: Ensures correct JSON document structure with proper party information for self-billed invoice submissions
  - **Error Resolution**: Complete fix for self-billed invoice TIN validation - restores working logic while supporting new form structure

- **Invoice Status Badge Color Standardization**: Standardized status badge colors across LHDN Status and Internal Status columns using eInvWorld brand colors
  - **Issue**: Status badges used inconsistent colors between LHDN Status and Internal Status columns, creating visual confusion
  - **Problems Fixed**:
    - `Valid`: LHDN showed green, Internal showed blue - now both use eInvWorld brand green (`#3AA564`)
    - `Invalid`: Now consistently red (`bg-danger`) across both columns for clear error indication  
    - `Cancelled`: Changed from gray to orange/yellow (`bg-warning`) for better visibility and distinction
    - `Submitted`: Now uses custom purple (`#6f42c1`) for unique identification separate from other statuses
  - **Refined Color Scheme**: Based on user feedback for better status distinction and accessibility
    - **Primary Brand Green** (`#3AA564`): Used for `Valid` status - indicates successful completion
    - **Purple** (`#6f42c1`): Used for `Submitted` status - indicates processing/pending state
    - **Orange/Yellow** (`bg-warning`): Used for `Cancelled` status - better visibility than gray
    - **Red** (`bg-danger`): Used for `Invalid` status - clear error/rejection indication
  - **Technical Implementation**:
    - **LHDN Status Column (Lines 707-711)**: Updated color mapping with inline brand color styles
    - **Internal Status Column (Lines 718-725)**: Standardized to match LHDN status colors
    - **Enhanced Status Coverage**: Added `Submitted` and `Cancelled` to Internal Status color mapping
  - **User Impact**:
    - Consistent visual language across both status columns
    - Improved readability and professional appearance
    - Better brand consistency throughout the invoice management interface
  - **UI/UX Enhancement**: Eliminates visual inconsistency that could cause user confusion when comparing LHDN vs Internal statuses

- **Invoice Table Readability Enhancement**: Removed distracting "Swipe to see more →" indicator that was interfering with table text readability
  - **Issue**: The swipe hint overlay was positioned over table content, making company names and other text difficult to read
  - **User Feedback**: "the swipe to see more indicator disturb the view to read the list"
  - **Technical Fix**: Removed `<div class="swipe-hint" id="swipeHint">Swipe to see more →</div>` from InvoiceLists.cshtml (Line 529)
  - **Impact**: 
    - Cleaner table view without visual distractions
    - Better readability of company names, invoice numbers, and other table data
    - Table still maintains horizontal scroll functionality without the overlay hint
  - **File Modified**: `Pages/Invoices/InvoiceLists.cshtml`
  - **User Experience**: Invoice list is now much easier to read and navigate without the obtrusive swipe indicator

- **Invoice Number Sorting Functionality Fixed**: Restored InvoiceNo column sorting that was not working due to missing query parameter preservation
  - **Issue**: Clicking on "e-Invoice No" column header did not sort invoices and lost all applied filters
  - **Root Cause**: Sorting links were missing essential route parameters that preserve filters and pagination state
  - **Technical Fix**: Added comprehensive route parameter preservation to InvoiceNo sorting link (Lines 554-568)
    - **Parameters Added**: pageNumber, pageSize, searchTerm, supplierName, customerName, invoiceNo, submissionDateFrom, submissionDateTo, documentType, LHDNStatus, InternalStatus, invoiceDirection
    - **Reset to Page 1**: Sorting automatically resets to first page while maintaining filters
  - **Backend Support**: InvoiceNo sorting logic was already implemented in InvoiceLists.cshtml.cs (Line 808)
  - **User Impact**: 
    - Invoice Number column sorting now works correctly (ascending/descending)
    - Applied filters are preserved when sorting by Invoice Number
    - Visual sort indicators (arrows) display properly to show current sort direction
    - Consistent behavior with other sortable columns
  - **File Modified**: `Pages/Invoices/InvoiceLists.cshtml`
  - **User Experience**: Users can now properly sort invoices by e-Invoice Number while maintaining their search and filter criteria

- **CRITICAL: InvoiceEdit NullReferenceException Fix**: Fixed critical null reference exception that prevented invoice editing functionality
  - **Error Details**: System.NullReferenceException at InvoiceEdit.cshtml line 844 - "Object reference not set to an instance of an object"
  - **Root Cause**: Dropdown collections (ClassificationCodes, UnitOptions, TaxCategoryOptions) were null when editing existing invoices due to code execution order issue
  - **Primary Issue**: Dropdown initialization code was placed after early return statement for edit mode, causing collections to never be initialized
  - **Complete Fix**: 
    - **Backend Fix**: Moved dropdown initialization before early return in OnGet method (Lines 221-224)
    - **Frontend Protection**: Added null checks in Razor view for all dropdown collections (Lines 844-872)
    - **Property Initialization**: Added default empty list initialization for all dropdown properties (Lines 160-166)
  - **Technical Changes**:
    - **InvoiceEdit.cshtml.cs**: Fixed execution order - dropdowns now initialize for both edit and new invoice modes
    - **InvoiceEdit.cshtml**: Added `@if (Model.Collection != null)` checks around all foreach loops in JavaScript
    - **Defensive Programming**: Properties now initialize with empty lists to prevent null reference exceptions
  - **Collections Protected**: ClassificationCodes, UnitOptions, TaxCategoryOptions, Suppliers, Customers, EInvoiceTypes, DocTypeSelectList
  - **User Impact**: 
    - Invoice editing functionality fully restored - no more crashes when editing existing invoices
    - Dynamic item creation dropdowns work properly in edit mode
    - Graceful handling of null collections prevents JavaScript errors
    - All invoice editing features (adding line items, changing classifications, updating tax categories) now function correctly
  - **File Modified**: `Pages/Invoices/InvoiceEdit.cshtml.cs`, `Pages/Invoices/InvoiceEdit.cshtml`
  - **Error Resolution**: Eliminates NullReferenceException that was blocking invoice editing operations

### Fixed
- **CRITICAL: Cancel Document Rate Limiting Issue**: Fixed issue where document cancellation succeeded in LHDN API but showed error to user due to 429 rate limiting during database update
  - **Root Cause**: `CancelDocumentAndSaveAsync` was failing on `GetDocumentDetailsAsync` with 429 (Too Many Requests) after LHDN API succeeded
  - **Primary Fix**: Added graceful handling of rate limit errors during document fetch - continues with database update since LHDN API already succeeded
  - **Key Changes**:
    - Added try-catch blocks around `GetDocumentDetailsAsync` in `CancelDocumentAndSaveAsync` (Lines 1444-1460)
    - Graceful null handling for `documentSummary` updates (Lines 1484-1495)
    - Enhanced error handling in main cancel workflow to show success even if database update has minor issues (Lines 1074-1090)
    - Added comprehensive logging with emojis for debugging rate limit scenarios
  - **Impact**: Cancel functionality now shows success to user when LHDN API succeeds, regardless of rate limiting during database sync
  - **Business Impact**: Users no longer see confusing error messages when their cancellation actually succeeded in LHDN

- **CRITICAL: Request Reject LHDN API Issue**: Fixed critical issue where invoice reject requests updated local database but never sent to LHDN API
  - **Root Cause**: Workflow was updating database before calling LHDN API, and database update method was returning early success
  - **Primary Fix**: Completely redesigned workflow to call LHDN API first, then update database only if API succeeds
  - **Key Changes**:
    - `OnPutRejectDocumentAsync` now calls LHDN API before database operations (Lines 990-1002)
    - Created new `UpdateLocalDatabaseForRejection` method to handle database updates separately (Lines 1325-1390)
    - **CRITICAL TIN Fix**: Now uses logged-in user's TIN for LHDN API instead of incorrect invoice TIN (Lines 967-979)
    - Frontend TIN selection fixed for buyer/supplier context in `InvoiceLists.cshtml` (Lines 684, 814)
    - Added comprehensive logging with emojis for better debugging (🚀📡💾✅❌🔑)
    - Implemented proper error handling - if LHDN API fails, database is never updated
  - **Location**: `Pages/Invoices/InvoiceLists.cshtml.cs` - Complete refactor of rejection workflow
  - **Impact**: Request reject functionality now properly submits to LHDN API FIRST, then updates local database
  - **Business Impact**: Critical compliance fix - ensures LHDN portal is always notified before local records are updated

## 📅 2025-08-20

### Fixed
- **TinyMCE Editor Display Issue**: Resolved critical issue where TinyMCE rich text editor was not displaying in Resources admin pages
  - **Root Cause**: JavaScript timing race condition - TinyMCE initialization was running before DOM was fully loaded
  - **Primary Fixes**:
    - Added `DOMContentLoaded` event listener wrapper around TinyMCE initialization in `Pages/Admin/Resources/Create.cshtml:75`
    - Added CSRF anti-forgery token to form (`@Html.AntiForgeryToken()`) in `Pages/Admin/Resources/Create.cshtml:11`
    - Enhanced image upload handler to include CSRF token validation in `Pages/Admin/Resources/Create.cshtml:107-110`
    - Added comprehensive error logging and initialization status tracking in `Pages/Admin/Resources/Create.cshtml:78-81, 94-100`
  - **Enhanced Features**:
    - Added `autoresize` plugin for better user experience
    - Added `branding: false` to remove TinyMCE watermark
    - Enhanced error handling for image uploads with detailed status reporting
    - Added setup callbacks for initialization lifecycle tracking
  - **Impact**: Content creation and editing functionality fully restored for Resources management
  - **Files Modified**: `Pages/Admin/Resources/Create.cshtml`

### Changed
- **Invoice List Column Alignment**: Improved visual consistency in invoice list table
  - **Total Amount Column**: Changed alignment from right (`text-end`) to center (`text-center`) in `Pages/Invoices/InvoiceLists.cshtml:705`
  - **LHDN Status Column**: Added center alignment (`text-center`) in `Pages/Invoices/InvoiceLists.cshtml:706`
  - **Internal Status Column**: Added center alignment (`text-center`) in `Pages/Invoices/InvoiceLists.cshtml:717`
  - **Action Column**: Changed alignment to right (`text-end`) in `Pages/Invoices/InvoiceLists.cshtml:740`
- **Dark Mode Status Badge Visibility**: Enhanced status badge contrast and visibility in dark mode theme
  - **LHDN Status Badges**: Added explicit `text-white` classes and changed default fallback from `bg-light text-dark` to `bg-dark text-white`
  - **Internal Status Badges**: 
    - Changed `ValidInternal` from `bg-secondary` to `bg-info text-white` for better visibility
    - Added explicit `text-white` classes to all colored badges (success, danger, secondary)
    - Enhanced color differentiation: `Valid` → Blue (`bg-primary`), `ValidInternal` → Light Blue (`bg-info`)
  - **Impact**: All status badges now clearly visible in both light and dark modes with proper contrast ratios
  - **Files Modified**: `Pages/Invoices/InvoiceLists.cshtml`

## 📅 2025-08-18

### Changed
- **Standardized table structure across admin code list pages**: Updated all admin code list pages to use consistent table structure matching the invoice list design
  - **Table Structure Updates** across 8 admin pages:
    - Changed table class from `enhanced-table table table-hover align-middle mb-0` to `table table-nowrap align-middle table-hover mb-0`
    - Added `text-muted` class to thead elements for consistent header styling
    - Updated column classes from `sticky-col` and generic classes to semantic column classes (`col-code`, `col-description`, etc.)
    - Removed `sort-indicator` class from sort icons, using clean `ri-arrow-up-down-line` icons
  - **Files Updated**:
    - `Pages/Admin/Codes/CountryCodes/ListCountry.cshtml` - Applied col-code and col-country classes
    - `Pages/Admin/Codes/CurrencyCodes/ListCurrency.cshtml` - Applied col-code and col-currency classes
    - `Pages/Admin/Codes/EInvoiceTypes/ListEInvoiceType.cshtml` - Applied col-code and col-description classes
    - `Pages/Admin/Codes/PaymentModes/ListPaymentMode.cshtml` - Applied col-code and col-payment-method classes
    - `Pages/Admin/Codes/StateCodes/ListState.cshtml` - Applied col-code and col-state classes
    - `Pages/Admin/Codes/TaxTypes/ListTaxType.cshtml` - Applied col-code and col-description classes
    - `Pages/Admin/Codes/UnitTypes/ListUnitType.cshtml` - Applied col-code and col-name classes
    - `Pages/Admin/Items/Index.cshtml` - Applied col-description, col-status, and col-action classes
  - **Benefits**: Improved visual consistency, better responsive behavior, and cleaner CSS class structure for easier maintenance
- **Enhanced admin code list pages with scrollable table pattern**: Updated 5 admin code management pages to use the modern enhanced scrollable table design
  - **E-Invoice Types** (`Pages/Admin/Codes/EInvoiceTypes/ListEInvoiceType.cshtml`)
    - Replaced basic table with enhanced scrollable table pattern featuring proper breadcrumb navigation
    - Added sticky column for Code field with sortable functionality and visual sort indicators
    - Integrated enhanced-table-scroll.js for improved mobile responsiveness and horizontal scrolling
  - **Payment Modes** (`Pages/Admin/Codes/PaymentModes/ListPaymentMode.cshtml`)
    - Applied enhanced table structure with Code and Payment Method columns
    - Added Velzon-style card header with consistent page title styling
  - **State Codes** (`Pages/Admin/Codes/StateCodes/ListState.cshtml`)
    - Completely restructured from complex ListJS table to simplified enhanced scrollable table
    - Streamlined from 6-column complex table to 2-column clean display (Code and State Name)
    - Removed unnecessary action buttons and checkboxes for better focus on data viewing
  - **Tax Types** (`Pages/Admin/Codes/TaxTypes/ListTaxType.cshtml`)
    - Upgraded to enhanced table with Code and Description columns
    - Added proper page title structure and breadcrumb navigation
  - **Unit Types** (`Pages/Admin/Codes/UnitTypes/ListUnitType.cshtml`)
    - Implemented enhanced table pattern with Code and Name columns
    - Consistent styling with other admin code pages
  - **Common Improvements Across All Pages**:
    - Consistent breadcrumb navigation: Admin > Codes > [Page Title]
    - Enhanced mobile responsiveness with swipe hints and horizontal scrolling
    - Velzon card design with proper header styling and brand colors
    - Sticky first column (Code) for better data readability during horizontal scroll
    - Sortable headers with Remix Icons sort indicators

## 📅 2025-08-11

### Added
- **Enhanced table sorting for InvoiceLists**: Added comprehensive sorting functionality to all table headers for better data management
  - **Backend Enhancement** (`Pages/Invoices/InvoiceLists.cshtml.cs`)
    - Added sorting support for UUID, SubmissionId, DocumentType, LHDNStatus, InternalStatus, RejectedDate, and UpdatedDate columns (lines 815-821)
    - Enhanced existing sorting logic with proper field mapping to database properties
    - Fixed property mappings: SubmissionId → SubmissionID, InternalStatus → InternalStatus, RejectedDate → RejectedTimestamp, UpdatedDate → LastUpdated
  - **Frontend Enhancement** (`Pages/Invoices/InvoiceLists.cshtml`)
    - Converted static headers to sortable headers with visual sorting indicators (lines 476-584)
    - Added eInvWorld brand color styling for active sort columns (`text-primary fw-semibold`)
    - Implemented up/down arrow icons (`ri-arrow-up/down/up-down-line`) for sort direction indication
    - Enhanced user experience with consistent sorting patterns across all data columns
  - **User Benefits**: Users can now sort 1000+ invoice records by any column for efficient data navigation and analysis

## 📅 2025-08-08

### Fixed
- **Request reject and cancel API functionality**: Fixed missing TIN parameter in AJAX calls across all invoice pages
  - **InvoiceDetails2 page** (`wwwroot/js/invoice-details-actions.js`):
    - Added TIN data attribute extraction from reject/cancel buttons (lines 8, 17)
    - Updated to use proper queue-based endpoints instead of synchronous InvoiceLists endpoints
  - **InvoiceLists page** (`Pages/Invoices/InvoiceLists.cshtml`):
    - Added `data-tin="@invoice.Supplier?.TIN"` to checkboxes and individual action buttons (lines 530, 659, 668)
  - **InvoiceLists JavaScript** (`wwwroot/js/request-rejection.js` and `wwwroot/js/cancel-invoice.js`):
    - Updated individual button handlers to extract TIN from data attributes (lines 30, 32)
    - Updated bulk operation handlers to include TIN from checkbox data attributes (lines 43, 40)
    - Updated API calls to include TIN parameter (lines 146, 145)
  - Ensures compatibility with backend handlers `OnPutRejectDocumentAsync` and `OnPutCancelDocumentAsync` in `Pages/Invoices/InvoiceLists.cshtml.cs`

### Added
- **Queue-based operations for InvoiceDetails2**: Enhanced with background processing for better performance and reliability
  - **Backend** (`Pages/Invoices/InvoiceDetails2.cshtml.cs`):
    - Added `CancelInput` class and `OnPutCancelDocumentAsync` method for queue-based cancel operations (lines 219-303)
    - Both reject and cancel operations now use background task queue with job tracking
    - TIN parameter automatically resolved from database for security and accuracy
    - Returns HTTP 202 (Accepted) with job ID for asynchronous processing
  - **Frontend** (`wwwroot/js/invoice-details-actions.js`):
    - Updated API calls to use InvoiceDetails2 queue endpoints instead of InvoiceLists synchronous endpoints (lines 170, 234)
    - Enhanced response handling for queue-based operations with "Queued!" success messages (lines 193-217, 269-293)
    - Changed request format from query parameters to JSON body for better security

### Fixed
- **InvoiceLists API data display issue**: Fixed configuration mismatch preventing LHDN API data from being displayed
  - **Configuration Fix** (`appsettings.json`):
    - Changed `"InvoiceUpdaterSettings"` to `"InvoiceStatusUpdaterSettings"` to match Program.cs configuration (line 104)
    - This enables the InvoiceStatusUpdater background service to load settings properly
  - **UI Enhancement** (`Pages/Invoices/InvoiceLists.cshtml`):
    - Re-enabled the "Refresh from API" button with eInvWorld branding (lines 365-375)
    - Added proper route parameters to preserve current filters when refreshing
    - Users can now manually trigger API data refresh when needed

- **InvoiceLists date filtering issue**: Fixed restrictive date range hiding older invoice data
  - **Date Range Fix** (`Pages/Invoices/InvoiceLists.cshtml.cs`):
    - Expanded default date range from 1 month to 3 months (line 133)
    - Now shows invoices from last 3 months by default instead of just 1 month
    - Resolves issue where June invoices (and older data) were filtered out and not displaying
    - Users can still customize date range using the date filter controls

- **InvoiceLists pagination improvements**: Enhanced user experience for handling large datasets (1000+ invoices)
  - **Backend Performance** (`Pages/Invoices/InvoiceLists.cshtml.cs`):
    - Increased default page size from 10 to 25 records per page (lines 142, 185)
    - Reduces pagination clicks from 100+ to 40 pages for 1000 records
  - **Frontend UX Enhancement** (`Pages/Invoices/InvoiceLists.cshtml`):
    - Added dynamic page size selector with options: 10, 25, 50, 100 entries (lines 723-728)
    - Implemented JavaScript function to change page size while preserving filters (lines 912-917)
    - Better layout with page size control on left, pagination controls on right (line 719)

## 📅 2025-06-11

### Added
- Custom media query: `@media (min-width: 1024px) { ... }` for timeline and image responsiveness.

### Changed
- Kesh requested update to the “Important Dates” section with latest e-Invoicing compliance phases from LHDN.
- Rewrote all date descriptions and phases based on revised info provided.
- Redesigned “Important Dates” section with a vertical layout to improve readability and chronological flow.
- Adjusted image alignment to maintain proportional layout on desktop using responsive flexbox and scaling utilities.


## 📅 2025-06-10
- Explored faster data entry options for customer info (beyond 1-by-1 form).

## 📅 2025-06-05 to 2025-06-09
- Enhanced CSV Invoice Import Module:
  - Added support for SAP, AutoCount, UBS, SQL formats.
  - Implemented inline editing for uploaded invoices and line-level tax details.
  - Supported multiple tax rows per invoice line with dynamic ➕ "Add Tax Row" logic.
- Fixed errors related to `ICollection<InvoiceTax>` and indexing in Razor view.
- Preview table UI improved for easier validation before saving or submitting.

## 📅 2025-06-03 to 2025-06-04
- Implemented expandable invoice rows with inline editable tax fields.
- Debugged CSV import issues (`ReaderException`, missing headers).
- Added UI support for editing tax category, percentage, and amount per line item.
- Supported nested invoice structures (InvoiceLines → InvoiceTaxes).

## 📅 2025-05-27 to 2025-05-31
- Enhanced background polling for `GetDocumentDetailsAsync` with retry logic and validation wait.
- Added checks to prevent premature PDF generation and email dispatch before LHDN validation.
- Fixed favicon display issues (light/dark versions).
- Confirmed `ConfirmEmail` and logout redirect issues.
- Added TLS MTA-STS via IIS + Cloudflare Tunnel, passed all checks.

## 📅 2025-05-21 to 2025-05-26
- Finalized LHDN TokenService with:
  - Retry-safe acquisition
  - Per-TIN caching
  - Role-based login (Taxpayer vs Intermediary)
- Refactored invoice submission flow (`CreateModel`, `InvoiceListsModel`) to use `SubmitDocumentsAsync(documents, tin)`.
- Built `InvoiceStatusSyncHelper` for syncing local status from LHDN’s `DocumentSummary`.
- Improved rejection and cancellation flow with 72-hour rule checks.

## 📅 2025-05-10 to 2025-05-20
- Created enhanced e-Invoicing dashboard (`MainDashboard`) with KPI and chart data from SQL views.
- Integrated dynamic dropdowns and draft-saving for invoice creation.
- Implemented tax logic per invoice type (Normal, Self-Billed).
- Ensured accurate rounding: all amounts show only 2 decimal places.

## 📅 2025-05-05 to 2025-05-09
- Rebuilt `Edit Invoice` and `Edit Template` Razor pages:
  - Separated logic for `saveEdit` vs `updateTemplate`
  - Allowed editable template names
- Enabled delete and bulk delete of saved templates.
- Fixed SweetAlert to Toastr migration for logout messages.
- Implemented session timeout warning only for signed-in users.

## 📅 2025-05-01 to 2025-05-04
- Refactored token handling:
  - Dual client secrets
  - Rate-limit resilience
- Implemented PDF and JSON generation post-submission.
- Added UUID validation and retry-safe document polling logic.
- Added self-billed invoice support with supplier/customer TIN switching.
