# EINVWORLD — System Documentation

Complete technical documentation for **EINVWORLD (eInvWorld)** — an e-invoicing middleware for
**Malaysia's LHDN MyInvois** system, self-hosted on a single in-house **Windows / IIS + SQL Server**
server.

> This document describes the system as of **v1.3.1**. For release history see [`CHANGELOG.md`](CHANGELOG.md);
> for deployment see [`IIS-DEPLOYMENT-GUIDE.md`](IIS-DEPLOYMENT-GUIDE.md) and [`DEPLOY-NOTES.md`](DEPLOY-NOTES.md);
> for secrets see [`SECRETS-SETUP.md`](SECRETS-SETUP.md).

---

## Table of contents

1. [Overview](#1-overview)
2. [Technology stack](#2-technology-stack)
3. [Architecture](#3-architecture)
4. [Solution layout](#4-solution-layout)
5. [Domain model](#5-domain-model)
6. [LHDN MyInvois integration](#6-lhdn-myinvois-integration)
7. [Invoice lifecycle](#7-invoice-lifecycle)
8. [Background services](#8-background-services)
9. [Security](#9-security)
10. [Features](#10-features)
11. [HTTP endpoints](#11-http-endpoints)
12. [Configuration reference](#12-configuration-reference)
13. [Database & migrations](#13-database--migrations)
14. [Operations & monitoring](#14-operations--monitoring)
15. [Build, test & CI](#15-build-test--ci)
16. [Document index](#16-document-index)

---

## 1. Overview

EINVWORLD lets Malaysian companies **create, validate, submit, track, cancel and manage e-invoices**
against the LHDN MyInvois platform, with a web UI for **suppliers, buyers and admins**. It acts as
*middleware*: it owns the MyInvois integration (auth, submission, validation polling, QR/LongId,
cancel/reject, retention sync) so that the customer's people — or their existing accounting/ERP — don't
have to.

**Design principles**
- **Self-hosted, single in-house server** (Windows + IIS + SQL Server). No cloud dependency.
- **FOSS-only** dependency policy — every runtime library is free/open-source.
- **On-prem privacy** — optional AI features run a local LLM (Ollama); no invoice data leaves the server.
- **Safe by default** — new/optional features ship OFF; nothing auto-submits without human review.

---

## 2. Technology stack

| Layer | Technology |
|---|---|
| Runtime | **.NET 10 (LTS)**, ASP.NET Core |
| Web | **Razor Pages** (primary UI) + **MVC API controllers** |
| Data | **EF Core 10** on **SQL Server** |
| Identity | **ASP.NET Core Identity** (roles: Admin, Supplier, Buyer) + TOTP 2FA |
| Logging | **Serilog** → rolling file + SQL `SystemLogs` table (MSSqlServer sink) |
| PDF | **DinkToPdf** (wkhtmltopdf, offline; default) or **Puppeteer** (headless Chromium) |
| Excel / CSV | **ClosedXML**, **CsvHelper** |
| Images | **Magick.NET** |
| PDF text extraction | **PdfPig** (AI Document Capture) |
| Signing | **XAdES** (System.Security.Cryptography.Xml) — built, OFF by default |
| HTTP resilience | **Microsoft.Extensions.Http.Resilience** (token client) + custom rate-limit handler |
| AI (optional) | Provider-agnostic (`IAiProvider`/`IAiService`); ships with **Ollama** local LLM (open-weight models) |
| QR | **QRCoder** |

---

## 3. Architecture

A single ASP.NET Core process hosts everything: the web UI, the API controllers, and a set of
**background hosted services**. It talks to **two SQL Server databases** and to the **LHDN MyInvois**
REST API.

```
                ┌──────────────────────────────────────────────────────────┐
   Browser ───► │  IIS  ──►  ASP.NET Core (EINVWORLD)                        │
   ERP/API ───► │     Razor Pages UI  ·  MVC API controllers                │
                │     Middleware: auth · MFA enforce · rate limiter · CSP    │
                │                                                            │
                │     Services (LHDN, PDF, Email, Assistant, Audit, …)       │
                │     Background workers (sync, status, recurring, …)        │
                └───────┬───────────────────────────────┬───────────────────┘
                        │                                │
                 ┌──────▼──────┐                  ┌──────▼───────────────┐
                 │ SQL Server  │                  │  LHDN MyInvois API   │
                 │  EINVWORLD  │                  │  (per-TIN OAuth,      │
                 │  + WEBSITE  │                  │   submit/poll/cancel) │
                 └─────────────┘                  └──────────────────────┘
```

**Layers**
- **Presentation** — `Pages/` (Razor Pages) and `Controllers/` (MVC, mostly API/utility endpoints).
- **Application/services** — `Services/` (integration, PDF, email, AI, audit) and `Helpers/`
  (cross-cutting logic, sync orchestration, guards, validation).
- **Domain/data** — `Models/` (entities, view models, DTOs) and `Data/ApplicationDbContext`.
- **Background** — `IHostedService` workers (see §8).

---

## 4. Solution layout

```
EINVWORLD/                     ASP.NET Core web project
├── Areas/Identity/            Scaffolded ASP.NET Identity UI (login, 2FA, account management)
├── Controllers/               MVC controllers (API + utility)
│   └── Api/                   InvoiceImportApiController (ERP REST validate)
├── Data/                      ApplicationDbContext (+ WebsiteDbContext)
├── Helpers/                   Cross-cutting helpers, sync orchestration, guards, validation
│   └── HealthChecks/          WritableFoldersHealthCheck
├── Migrations/                EF migrations + idempotent Apply_*.sql scripts
├── Models/                    Entities, input/view models, JSON DTOs (InputModel, JsonModels, Audit, Background, …)
├── Pages/                     Razor Pages (Admin, Invoices, Suppliers, Templates, Assistant, …)
│   └── Shared/                Layouts, _Sidebar, partials (Velzon + parallel Tabler set — see §4.1)
├── Services/
│   ├── AI/                    Provider-agnostic AI core (IAiProvider/IAiService; Ollama provider)
│   ├── Assistant/             AI e-invoice assistant (domain prompts; delegates to IAiService)
│   ├── Audit/                 Tamper-evident hash-chained audit
│   ├── Background/            Hosted workers + durable job queue
│   ├── DocumentCapture/       PDF text extraction (PdfPig)
│   ├── Import/                Bulk import + watched-folder importer
│   ├── Mappers/               Invoice ↔ UBL/PDF/CSV mappers
│   └── Middleware/            MFA enforcement, user context
├── EINVWORLD.Tests/           xUnit test project (built in CI)
├── Program.cs                 Composition root (DI + middleware pipeline)
├── appsettings*.json          Configuration (Production overrides base)
└── *.md                       README, this doc, deployment & secrets guides, changelog
```

### 4.1 Front-end theme (Velzon → Tabler migration, in progress)

The UI is **server-rendered Razor Pages** (no SPA framework). The authenticated UI is being migrated from
the commercial-look **Velzon** theme to the free MIT **Tabler** (Bootstrap 5) theme; both are self-hosted
(no CDN). Migration state and plan: `docs/TABLER-MIGRATION-AUDIT.md`; user-visible history: `CHANGELOG.md`.

- **Velzon (legacy, still the fallback):** `_Layout` + `_Sidebar` + `_LoginLayout`, assets under
  `wwwroot/assets/`.
- **Tabler (new):** `_LayoutTabler` composed of `_TablerSidebar` (+ `_AdminNavigation`/
  `_SupplierNavigation`/`_BuyerNavigation`), `_TablerTopbar`, `_UserMenu`, `_Footer`, `_PageHeader`; and
  `_LoginLayoutTabler` for the Identity area. Assets under `wwwroot/tabler/` (Tabler v1.4.0 +
  `einvworld-tokens.css` brand tokens & Velzon-class compat shims + `einvworld-ui.js` route-highlighting).
- **Switch mechanism:** a per-folder `Pages/<area>/_ViewStart.cshtml` sets `Layout = "_LayoutTabler"` for
  **authenticated** users only, so anonymous/public pages keep the marketing layout. Delete that file to
  revert an area to Velzon. Functional plugins (jQuery, Bootstrap bundle, Select2, Flatpickr, SweetAlert2,
  Toastr, Chart.js, TinyMCE, lord-icon) and behaviour (idle-timeout, app-search, Turnstile on auth) are
  identical across both layouts. **PDF/print templates (`Layout = null`) are theme-independent.**
- **Coverage:** as of 2026-07-11 **all authenticated pages render Tabler** (deployed + Playwright-verified
  across Supplier/Buyer/Admin). Only public marketing/Home/Resources (`_HomeLayout`) and Error pages
  (`Layout = null`) are intentionally non-Tabler.
- **Not migrated / deferred (Phase 8):** removing Velzon and retiring the DB-backed global-theme system
  (`/api/Theme/*`) — held until a fully-green re-verification. Two pre-existing (non-Tabler) app bugs were
  surfaced during QA and remain open: company logos emitted as `file:///` paths and 404 resource images.

---

## 5. Domain model

Persisted via `ApplicationDbContext` (database `EINVWORLD`). Key entities:

**Invoicing core**
- `InvoiceHeader` — the invoice (doc type, dates, currency, totals, supplier/customer, UUID/SubmissionID,
  internal + LHDN status, QR/LongId, the `SubmissionClaimedAtUtc` concurrency claim).
- `InvoiceLine` / `InvoiceTax` — line items and their taxes.
- `InvoiceHistory` — per-invoice audit of state changes.
- `PartyInfo` — a company/taxpayer (TIN, registration, MSIC, SST, address, bank). Suppliers and buyers
  are `PartyInfo`. `Supplier`, `Buyer`, `SupplierBuyer`, `PublicCustomer`, `UserCompany` model the
  relationships (who can invoice whom; which TINs a user owns).

**Templates & recurring**
- `InvoiceTemplate` / `InvoiceTemplateLine` / `InvoiceTemplateTax` — reusable invoice templates.
- `RecurringProfile` / `RecurringRunHistory` — scheduled recurring invoices.

**LHDN & auth**
- `LHDNToken` (per-TIN cached OAuth token, unique on TIN) / `LHDNTokenLog`.
- `SubmissionRecord` — idempotency: hash of a submitted payload + the response (replay-on-duplicate).
- `SyncJob` — durable background job rows (queue + retry + lock fields).

**Reference data** (seeded from `wwwroot/codes/*.json`)
- `ClassificationCode`, `TaxType`, `CurrencyCode`, `CountryCode`, `StateCode`, `UnitType`,
  `EInvoiceType`, `MSICSubCategoryCode`, `PaymentMode`, `RegistrationType`, `Status`.

**Audit / logs / dashboard**
- `AuditLog` — tamper-evident hash chain (see §9). `UserActivityLog` / `ActivityLog` — activity trail.
- `Resources` (`ResourceItem`/`ResourceType`) — CMS-style resource/article content.
- Dashboard read-models: `InvoiceKpiSummary`, `InvoiceMonthlySummary`, `InvoiceByCustomerSummary`,
  `InvoiceTopProduct`, `InvoiceTypeBreakdown`, `InvoiceTaxSummary`, `InvoiceRejectedReason`.

> A second context (`WebsiteDbContext`, database `EINVWORLDWEBSITE`, connection `WebsiteDb`) backs the
> public marketing/website data. `SystemLogs` is **owned by the Serilog sink**, not EF.

---

## 6. LHDN MyInvois integration

All LHDN calls go through **`LHDNApiService`** (typed `HttpClient`), the **single chokepoint** for
submit/cancel/reject/poll/search.

**Document types** (8): `01` Invoice, `02` Credit Note, `03` Debit Note, `04` Refund Note, `11`
Self-billed Invoice, `12` Self-billed CN, `13` Self-billed DN, `14` Self-billed RN.

**Endpoints used** (`api/v1.0/…`): `taxpayer/validate`, `documentsubmissions` (submit + poll by
`submissionUid`), `documents/search`, `documents/state/{id}/state` (cancel/reject), document details.

**Authentication** — OAuth client-credentials **per TIN**; tokens cached in `LHDNToken` and reused
(`TokenService`). Intermediary/on-behalf-of calls add the **`onbehalfof`** header for the target TIN.
`TokenRenewalService` keeps tokens warm.

**Rate limiting** — `LhdnRateLimitHandler` is attached to **every** LHDN client (incl. the token
client) and paces each endpoint below MyInvois' per-minute limits to avoid `429` storms.
> **Single-instance assumption:** these rate-limit buckets are **per process** (in-memory). The app is
> designed to run as a **single instance**. If you ever scale to multiple instances behind a load
> balancer, the per-instance buckets would multiply the effective LHDN call rate — move to a shared
> (e.g. Redis-backed) limiter first.

**Resilience** — the **token client** additionally uses `AddStandardResilienceHandler` (retry +
timeouts) since token acquisition is idempotent. The **submission client does not retry** (a retried
POST could create a duplicate document).

**Digital signing (v1.1)** — XAdES signing (`DocumentSigningService`) is **built but OFF**
(`LHDNApiConfig:SigningEnabled=false`). When enabled, signing happens centrally inside
`SubmitDocumentsAsync` for all submission paths. The certificate comes from a pluggable
**`ICertificateProvider`** (`Services/Signing/`), selected by `LHDNApiConfig:SigningKeyProvider` —
`"File"` (default) loads the `.p12` from `CertPath`; a vault/HSM provider (e.g. Azure Key Vault) is a
drop-in registration with no signing-service change (see SECRETS-SETUP.md "Signing-key custody").

---

## 7. Invoice lifecycle

```
 Draft ──submit──► (claim) ──► sign? ──► POST documentsubmissions ──► accepted
   │                                                   │
   │                                            poll submissionUid
   │                                                   ▼
   └─ edit/delete                         Valid / Invalid  ──► fetch LongId (QR)
                                                   │
                              cancel (72h) / reject ─► documents/state
```

**Concurrency & de-duplication** (defense in depth on the submit path):
1. **UUID/Draft guard** — already-submitted invoices can't be resubmitted.
2. **Atomic claim** — `InvoiceSubmissionGuard.TryClaimAsync` compare-and-sets `SubmissionClaimedAtUtc`
   (5-min stale timeout) so a double-click can't double-submit. A winning claim also reloads any
   `InvoiceHeader` already tracked by the caller's context: the claim's raw UPDATE bumps the row's
   `RowVersion`, so without the reload the caller's post-submission save would always fail with a
   concurrency conflict. Callers must mutate the entity only **after** claiming.
3. **Payload idempotency** — `SubmissionRecord` replays the prior response for an identical payload
   submitted within 10 minutes (mirrors MyInvois' own `422 DuplicateSubmission`).
4. **TIN resolution** — `TinHelper.ResolveSubmitterTin` picks the correct submitter (self-billed →
   customer, else supplier); `OwnsTinAsync` enforces the user owns that TIN.

**Status sync** — `InvoiceStatusUpdater` (background) and the manual sync/import jobs poll LHDN and
update internal/LHDN status, capturing the `LongId`/QR once Valid.

---

## 8. Background services

All run as `IHostedService` in the same process (so the IIS app pool should be **AlwaysRunning**):

| Service | Purpose |
|---|---|
| **`DurableSyncJobWorker`** | Durable, SQL-backed job queue. Polls `SyncJobs`, atomically claims a job (`UPDLOCK`/`READPAST`), dispatches by `JobType` to an `ISyncJobHandler`, retries with backoff, and recovers orphaned jobs after a restart. Handles StatusSync / FullImport / SupplierRefresh / **SubmitDocument** (background retry of an interactive LHDN submission that threw — reuses `InvoiceSubmissionHelper`, no-ops if the invoice is no longer Draft so it can never double-submit; exhausted retries land in the Sync Jobs dead-letter view) / **WebhookDelivery** (outbound customer-ERP webhook, see §10). |
| **`InvoiceStatusUpdater`** | Periodically polls LHDN for pending invoices' validation status. Also runs the webhook dispatcher (enqueues `WebhookDelivery` jobs for invoices that reached a terminal status; no-op unless `Webhooks:Enabled`). |
| **`InvoiceFinalizerService`** | Finalizes invoices once validated (PDF/email/QR follow-ups). |
| **`RecurringInvoiceWorker`** | Generates invoices from `RecurringProfile`s on schedule (roll-forward, no catch-up storms). |
| **`TokenRenewalService`** | Keeps per-TIN LHDN tokens fresh. |
| **`LogCleanupService`** | Prunes old `SystemLogs` rows (older than `LogCleanupSettings:RetentionDays`, default 30) every 4 h. Deletes in batches of `LogCleanupSettings:BatchSize` (default 5000) so a large backlog never holds a table lock or hits the command timeout — a large pre-existing backlog drains over several runs. |
| **`WatchedFolderImportWorker`** | (Optional) validates CSV/XLSX dropped into an Inbox folder. |

The durable queue (`SyncJob` + `ISyncJobTracker` + handlers) replaced an older in-memory queue so jobs
**survive an app-pool recycle / reboot**.

---

## 9. Security

**Authentication & roles** — ASP.NET Core Identity; roles **Admin / Supplier / Buyer**. Login supports
**Taxpayer** and **Intermediary** modes. Identity lockout is on (`IdentityLockout`: 5 attempts → 15 min).

**Two-factor (Admin)** — `AdminMfaEnforcementMiddleware` enforces **block-until-enrolled**: an Admin
without 2FA is redirected to the authenticator-setup page (the `/Identity` area + health + static assets
stay reachable, so there is no lockout). Toggle with `Security:EnforceAdminMfa` (default `true`).

**Tamper-evident audit** — `AuditService` writes an append-only, **hash-chained** `AuditLogs`: each row
stores the previous row's hash plus a SHA-256 of its own contents chained onto it. Recomputing the chain
(**Admin → Audit Trail → Verify**) detects any insert/delete/edit. Wired into LHDN submit/cancel/reject,
bulk/watched/API imports, document capture, admin sync-job actions, and **cross-tenant invoice reads**
(`InvoiceViewedCrossTenant` — written when a viewer's own companies include none of the invoice's
parties, which post-IDOR-guard can only be an Admin; same-tenant views are deliberately not audited).
Appends are serialised and isolated (own DbContext); writing never throws to the caller.

**Authorization / IDOR** — per-TIN ownership checks (`OwnsTinAsync`, `UserCompany`) gate invoice
access; `SafePath.TryResolve` blocks path traversal on all file-serving endpoints; uploads are
extension/size/magic-byte validated and stored outside `wwwroot`.

**Transport / headers** — HTTPS + HSTS; security headers (`X-Content-Type-Options`, `X-Frame-Options`,
`Referrer-Policy`, …); **CSP in Report-Only** mode with a `/csp-report` collector (to be promoted to
enforcing once violations are reviewed); antiforgery on state-changing forms; Cloudflare Turnstile on
public forms.

**Rate limiting** — *inbound*: a generous per-IP sliding-window limiter (`RateLimiting`, health-exempt)
as a DoS backstop. *Outbound*: the LHDN rate-limit handler (§6).

**Data protection** — DataProtection keys persisted to `DataProtection:KeyRingPath` (**required in
Production** — outside the App folder so redeploys don't wipe them). Startup **fails fast**
(`ProductionConfigValidator`) on missing key ring / blank connection string / misconfigured
signing / localhost URLs in Production, etc.

**Field-level PII encryption** (v1.7.2) — bank account numbers (`BankAccountNo`) and secondary/tertiary
address lines (`Addr2`/`Addr3`) are encrypted at rest via an EF Core value converter
(`Services/Security/ProtectedStringConverter`) backed by the DataProtection key-ring (purpose
`eInvWorld.Pii.FieldEncryption.v1`). Encryption is transparent on read/write; reads fall back to plaintext
for not-yet-migrated rows. Existing rows are encrypted by a one-time, idempotent, admin-triggered backfill
(**Admin → System Health → "Encrypt existing PII"**, audited as `PiiEncryptionBackfill`). TIN and
`Addr1`/city/state/postal are deliberately **not** encrypted (TIN is filtered on throughout; the primary
address feeds reporting/PDFs). This makes the key-ring load-bearing for data — **back it up** (see
SECRETS-SETUP.md and RUNBOOKS.md Runbook 4).

**Secrets** — never committed; supplied via user-secrets (dev) or IIS environment variables (server).
See [`SECRETS-SETUP.md`](SECRETS-SETUP.md).

---

## 10. Features

**Invoicing**
- Create/edit/submit all 8 document types; self-billed variants; credit/debit/refund notes referencing
  the original UUID.
- Submit to MyInvois (UBL 2.1 JSON), poll status, capture LongId/QR, **cancel/reject** within the 72h
  window, view validation errors with a human-readable rejection helper.
- **Manual sync / import / refresh** run as durable background jobs (visible on **Sync Jobs**).
- **Templates** and **recurring invoices**.
- PDF generation (DinkToPdf/Puppeteer) and validated-invoice email notifications.

**AI (optional, on-prem, OFF by default)**
- **AI E-Invoice Assistant** (`/Assistant`) — answers MyInvois questions and turns a plain-English
  description into a reviewed invoice **suggestion** (grounded on real LHDN codes + the user's
  customers). Suggest-only; never submits. (`AI` config; Ollama provider by default.)
- **AI Document Capture** (`/Invoices/CreateFromFile`) — upload a digital PDF → extract text (PdfPig) →
  suggestion → review. Draft-safe. (`DocumentCapture` config; needs `AI:Enabled`.)
- **Admin → AI Settings** (`/Admin/AiSettings`) — read-only view of the active AI config + a **Test
  connection** probe (reachable / model pulled / latency). Never shows the API key.

**Ingestion / connectors** (draft-safe — validate/suggest only)
- **Bulk Import** (`/Invoices/BulkImport`) — CSV/XLSX per-row validation against LHDN codes + a
  downloadable template.
- **Watched-folder importer** — validates CSV/XLSX dropped into an Inbox, sorts to `Processed/`/`Rejected/`.
- **REST validate API** — `POST /api/import/validate` (header `X-Api-Key`) for an external ERP.
- **Legacy "Extract Invoice"** — posts a PDF to an external OCR service (`ExtractInvoice:ServiceUrl`).

**Outbound webhooks (optional, OFF by default)**
- When an invoice reaches a terminal LHDN status (Valid / Cancelled / Rejected / Invalid), a signed HTTP
  callback is delivered to each enabled subscription for the invoice's supplier/customer TIN — so a
  customer ERP learns of the change without polling or relying on email.
- **Durable delivery**: each callback is a `WebhookDelivery` job on the durable queue, so a receiver being
  down is retried with backoff and dead-letters into **Sync Jobs** (no bespoke retry code).
- **Signed**: `X-EInvWorld-Signature: sha256=HMAC_SHA256(secret, rawBody)` (plus `X-EInvWorld-Event` and
  `X-EInvWorld-Delivery`). Receivers must verify the signature and treat `invoiceNo`+`status` as an
  idempotency key (delivery is at-least-once).
- **SSRF-guarded**: callback URLs must be absolute http(s), HTTPS by default, and (default) may not resolve
  to loopback/private/link-local addresses (`Webhooks:BlockPrivateNetworks`).
- **Admin → Webhooks** manages subscriptions (register / rotate secret / enable-disable / delete / test).
  Secrets are generated server-side, shown once, and stored encrypted (DataProtection). Config: `Webhooks`.

**Admin & ops** — user/company management, resources (CMS), system logs, **Sync Jobs**, **Audit Trail**,
**System Health**, **Webhooks**, global theme, dashboards/KPIs.

---

## 11. HTTP endpoints

**Health** (anonymous)
- `GET /health/live` — process up (IIS App Initialization / liveness).
- `GET /health/ready` — DB reachable + Documents/GeneratedPdf/DataProtection folders writable.
- `GET /health` — all checks (back-compat).

**API / utility controllers**
- `POST /api/import/validate` — ERP invoice-row validation (`X-Api-Key`). *InvoiceImportApiController*
- `POST /csp-report` — CSP violation sink (anonymous). *CspReportController*
- `GET  /api/Image/logo?fileName=` — company logo (auth, path-guarded). *ImageController*
- `GET  /api/resources/images/{category}/{size}/{fileName}` — resource images. *ResourcesApiController*
- `GET  /api/resources/editor/{fileName}`, `companies/logos/{fileName}` — resource/logo files.
- `POST /api/…/submitDocuments`, `save`, `save-pdf` — invoice operations. *EInvoicing/Invoice controllers*
- `GET  /api/.../validateTaxpayer/{tin}` — LHDN taxpayer validation.
- `POST /api/resources-migration/*`, `cleanup-old-*`, `migrate-existing-*` — admin resource maintenance.
- `GET  /Theme/global`, `POST /Home/LoginAsTaxpayer|LoginAsIntermediary`, `GET /LogExport/download`.

**UI** — Razor Pages under `/`, `/Invoices/*`, `/Admin/*`, `/Suppliers/*`, `/Templates/*`,
`/RecurringInvoices/*`, `/Assistant`, `/Profile`, `/Dashboard`, plus the `/Identity` account area.

---

## 12. Configuration reference

`appsettings.json` is the base; **`appsettings.Production.json` overrides it** in Production. Secrets are
blank in files and supplied via env vars / user-secrets.

| Section | Purpose |
|---|---|
| `AppInfo` | Name / Version / Environment (footer display). |
| `ConnectionStrings` | `DefaultConnection` (EINVWORLD) + `WebsiteDb` (EINVWORLDWEBSITE). **Secret.** |
| `DatabaseSettings:AutoMigrateOnStartup` | Apply EF migrations on boot. `true` in Production (additive — back up first). |
| `DataProtection:KeyRingPath` | Encryption key-ring folder. **Required in Production**; outside `App\`. |
| `Security:EnforceAdminMfa` | Require Admin 2FA enrolment (default `true`). |
| `Security:HttpsRedirectPort` | HTTP→HTTPS redirect port. **Smart default:** when `ForwardedHeaders` is enabled (behind a TLS-terminating proxy / Cloudflare Tunnel) the redirect is **off** — an in-app redirect would loop (`http→https→http`) since the edge already terminates TLS; for a direct IIS HTTPS binding it defaults to `443` (set explicitly because behind IIS the port can't be auto-discovered). Override anytime: a port forces it on, `0` forces it off. |
| `ForwardedHeaders` | Reverse-proxy / Cloudflare Tunnel support. `Enabled` (default `true`) makes the app honour `X-Forwarded-Proto` (original scheme = https → correct Secure cookies, HSTS, no redirect loop) and `X-Forwarded-For` (real client IP → correct per-IP rate limiting + audit/log IPs). `KnownProxies` (extra trusted proxy IPs beyond loopback) and `ForwardLimit` (hops, default 1). Only headers from a known proxy are trusted. |
| `RateLimiting` | Inbound per-IP limiter: `Enabled`, `PermitsPerMinute` (default 1200), `AdminSyncPerMinute` (default 10 — stricter per-user cap on `/Admin/InvoiceSync`). |
| `SyncFailureAlerts` | Optional email when failed sync jobs pile up: `Enabled` (default false), `RecipientEmail`, `Threshold`, `CheckMinutes`, `CooldownHours`. Throttled. |
| `Webhooks` | Outbound customer-ERP webhooks (default OFF): `Enabled`, `DeliveryTimeoutSeconds` (default 15), `BlockPrivateNetworks` (SSRF guard, default true), `RequireHttps` (default true). Subscriptions managed in Admin → Webhooks; signing secrets encrypted at rest. |
| `PDFGenerationSettings:TimeoutSeconds` | Max wait for a DinkToPdf render before abandoning it (default 60) so a hung render can't block the request. |
| `LHDNApiConfig` | MyInvois `BaseUrl`/`ValidationBaseUrl`, `ClientId`, **secrets** (`ClientSecret`/`2`), `OnBehalfOf`, `SigningEnabled`, `DocVersion`, `CertPath`/`CertPass`, `SigningKeyProvider` (certificate custody — `File` default; vault/HSM drop-in), `SyncRetentionDays`. |
| `TaxpayerValidationSettings` | Default TIN/ID used for token caching & system identity. |
| `EmailConfiguration` | SMTP (**`SmtpPassword` secret**), base URLs, per-event subjects, notification toggles. |
| `PDFGenerationSettings` | `Engine` (DinkToPdf/Puppeteer), `BaseUrl`, render delay, `ChromiumExecutablePath`. |
| `FilePathConfig` | Document/draft/submitted/valid/invalid/cancelled/PDF/logo/resource folders. |
| `Serilog` / `Logging` | File + `SystemLogs` sink config. |
| `InvoiceStatusUpdaterSettings` | Status-sync polling cadence & UI cooldowns. |
| `SessionSettings` / `IdentityLockout` | Session timeout/cookie; lockout policy. |
| `LogCleanupSettings` | `RetentionDays` (default 30) and `BatchSize` (default 5000) for the batched `SystemLogs` prune. |
| `InvoiceSettings` | e.g. `BackdateSeconds`. |
| `Turnstile` | Cloudflare CAPTCHA (`SecretKey` **secret**). |
| `AI` | Provider-agnostic AI: `Enabled`, `Provider` (Ollama today), `BaseUrl`, `Model` (default `gemma3:12b`), `TimeoutSeconds`, `Temperature`, `MaxTokens`, `ApiKey` (**secret**, cloud providers only — env var). The old `AIAssistant` section is retired — rename any `AIAssistant__*` env vars to `AI__*`. |
| `DocumentCapture` | AI Document Capture: `Enabled`, `MaxFileSizeMb`, `MaxPages`. |
| `WatchedFolderImport` | `Enabled`, `InboxPath`, `PollSeconds`. |
| `Api:Key` | **Secret** — enables `POST /api/import/validate`. |
| `ExtractInvoice:ServiceUrl` | Legacy OCR service endpoint. |
| `CodeFilePaths` | Locations of the reference-code seed JSON files. |

---

## 13. Database & migrations

- **EF Core 10 / SQL Server**, two databases: `EINVWORLD` (main, `ApplicationDbContext`) and
  `EINVWORLDWEBSITE` (`WebsiteDbContext`).
- **84 migrations** under `Migrations/`. In Production migrations apply **automatically on startup**
  (`AutoMigrateOnStartup=true`) — they are **additive** (new tables/columns/indexes; no `Up()` drops),
  so existing data is preserved. **Always back up first** and ensure the runtime SQL login has DDL rights.
- Every migration has an idempotent **`Apply_*.sql`** for the manual path (`AutoMigrateOnStartup=false`);
  ordered list in [`DEPLOY-NOTES.md`](DEPLOY-NOTES.md).
- `SystemLogs` is created/owned by the **Serilog MSSqlServer sink**, not EF.
- Reference-code tables are seeded at startup from `wwwroot/codes/*.json` (`DataSeeder`).

---

## 14. Operations & monitoring

- **Health probes** — point Uptime Kuma / PRTG / Zabbix at `GET /health/ready`.
- **Admin → System Health** — queue depth / failed / oldest-queued job, audit & submission row counts,
  DataProtection key-ring writability, Documents-drive free space, signing-cert expiry, runtime/version.
- **Admin → Sync Jobs** — background job status with **Retry/Cancel**.
- **Admin → Audit Trail** — list + **Verify chain integrity**.
- **Admin → System Logs** — Serilog `SystemLogs` (IP/user enriched).
- **Logs** — rolling file (`Serilog:WriteTo:File`) + `SystemLogs` table.
- **Backups** — full DB daily + log backups; monthly restore test; back up the DataProtection `Keys`
  folder and the signing cert. (See `DEPLOY-NOTES.md` §7.)

---

## 15. Build, test & CI

- **Build:** `dotnet build EINVWORLD.sln -c Release`. **Run:** `dotnet run` (dev; default
  `https://localhost:7073`).
- **Tests:** `EINVWORLD.Tests/` (xUnit) — `dotnet test`. Covers helpers, validators, the background
  queue, AI (`Services/AI`, provider mapping, config validation), and UBL mapping.
- **Integration tests:** `EINVWORLD.Tests/Integration/` runs against a **real SQL Server** — migrations
  applied via `Migrate()` and raw-SQL paths (e.g. `InvoiceSubmissionGuard`'s atomic claim) exercised for
  real. Gated on the `INTEGRATION_SQLSERVER` env var; no-ops cleanly when it's unset (e.g. local dev
  without a DB), so the suite always passes either way. CI sets it against SQL Server Express LocalDB.
- **CI:** `.github/workflows/ci.yml` runs restore → build → start LocalDB → test on **windows-latest** for
  every push/PR.
- **No local SDK?** Migrations are hand-authored with a generated `Designer` + idempotent `Apply_*.sql`;
  CI is the compiler of record.

---

## 16. Document index

| Document | Contents |
|---|---|
| [`README.md`](README.md) | Overview, stack, features, getting started, configuration. |
| **`DOCUMENTATION.md`** (this) | Full system/architecture reference. |
| [`IIS-DEPLOYMENT-GUIDE.md`](IIS-DEPLOYMENT-GUIDE.md) | Step-by-step IIS deployment (beginner-friendly). |
| [`DEPLOY-NOTES.md`](DEPLOY-NOTES.md) | Concise operator checklist (migrations, app pool, backups, rollback). |
| [`SECRETS-SETUP.md`](SECRETS-SETUP.md) | Every secret and how to configure it. |
| [`CHANGELOG.md`](CHANGELOG.md) | Release history and notable fixes. |
