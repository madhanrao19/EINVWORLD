# 🧾 EINVWORLD Developer Change Log

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
