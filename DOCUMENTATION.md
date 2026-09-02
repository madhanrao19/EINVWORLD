# EINVWORLD — System Documentation

Complete technical documentation for **EINVWORLD (eInvWorld)** — an e-invoicing middleware for
**Malaysia's LHDN MyInvois** system, self-hosted on a single in-house **Windows / IIS + SQL Server**
server.

> This document describes the system as of **v1.25.0**. For release history see [`CHANGELOG.md`](CHANGELOG.md);
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
│   └── Shared/                Layouts, _Sidebar, partials (Velzon fallback + Tabler set — see §4.1)
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

### 4.1 Front-end theme (Velzon → Tabler migration, markup complete)

The UI is **server-rendered Razor Pages** (no SPA framework). The authenticated UI has been migrated from
the commercial-look **Velzon** theme to the free MIT **Tabler** (Bootstrap 5) theme; both are self-hosted
(no CDN). Migration state and plan: `docs/TABLER-MIGRATION-AUDIT.md`; user-visible history: `CHANGELOG.md`.

- **Velzon (legacy, still the fallback):** `_Layout` + `_Sidebar` + `_LoginLayout`, assets under
  `wwwroot/assets/`.
- **Tabler (new):** `_LayoutTabler` composed of `_TablerSidebar` (+ `_AdminNavigation`/
  `_SupplierNavigation`/`_BuyerNavigation`), `_TablerTopbar`, `_UserMenu`, `_Footer`, `_PageHeader`; and
  `_LoginLayoutTabler` for the Identity area. Assets under `wwwroot/tabler/` (Tabler v1.4.0 +
  `einvworld-tokens.css` brand tokens & Velzon-class compat shims + `einvworld-ui.js` route-highlighting
  and the desktop sidebar collapse-to-icons toggle, persisted via `localStorage`; mobile keeps Bootstrap's
  own auto-collapse below `lg`). `einvworld-ui.js` is cache-busted with `asp-append-version`.
- **Switch mechanism:** a per-folder `Pages/<area>/_ViewStart.cshtml` sets `Layout = "_LayoutTabler"` for
  **authenticated** users only, so anonymous/public pages keep the marketing layout. Delete that file to
  revert an area to Velzon. Functional plugins (jQuery, Bootstrap bundle, Select2, Flatpickr, SweetAlert2,
  Toastr, Chart.js, TinyMCE, lord-icon) and behaviour (idle-timeout, app-search, Turnstile on auth) are
  identical across both layouts. **PDF/print templates (`Layout = null`) are theme-independent.**
- **Coverage:** as of 2026-07-27 **all authenticated pages render Tabler with no known remaining raw
  Velzon markup** (deployed + Playwright-verified across Supplier/Buyer/Admin). Only public
  marketing/Home/Resources (`_HomeLayout`) and Error pages (`Layout = null`) are intentionally non-Tabler.
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
- `CompanyRole` — a company-scoped permission set (ManageUsers/EditProfile/ManageBranding/ViewAudit)
  assignable to a `UserCompany` membership. 4 system rows (Owner/Admin/Editor/Viewer, `PartyInfoId =
  null`) are shared by every company; a Supplier Owner/Admin can additionally create custom roles
  scoped to just their own company (`PartyInfoId` set) via Roles & Permissions.
- `RoleModulePermission` — whether a global Identity role (Supplier/Buyer; Admin always has full access)
  may access a given app module (Invoices, Recurring Invoices, Company Management, ...). Managed on
  **Admin → Role Management**; a missing row defaults to allowed.

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

**Field validation** — `InvoiceMapper` enforces LHDN SDK constraints before building the UBL JSON:
currency exchange rate required when the currency isn't MYR, and every invoice line's unit-of-measure
code validated against the active `UnitType` table (the official LHDN unit-code list) — not just the
Create Invoice form's dropdown, so CSV import, templates, and recurring invoices are covered too.

**Endpoints used** (`api/v1.0/…`): `taxpayer/validate`, `documentsubmissions` (submit + poll by
`submissionUid`), `documents/search`, `documents/state/{id}/state` (cancel/reject), document details.

**Authentication** — OAuth client-credentials **per TIN**; tokens cached in `LHDNToken` and reused
(`TokenService`). Intermediary/on-behalf-of calls add the **`onbehalfof`** header for the target TIN.
`TokenRenewalService` keeps tokens warm.

**Rate limiting** — `LhdnRateLimitHandler` is attached to **every** LHDN client (incl. the token
client) and paces each endpoint below MyInvois' per-minute limits to avoid `429` storms. Per-endpoint
ceilings are read from `LHDNApiConfig:RateLimits:*` (`TokenPerMinute`, `ValidatePerMinute`,
`SubmitPerMinute`, `PollPerMinute`, `SearchPerMinute`, `GetDocPerMinute`, `StatePerMinute`,
`GeneralPerMinute`), falling back to the current SDK's preprod limits if unset — override per
environment if production limits are confirmed to differ. Registered as a DI **singleton** (not
transient) so the token buckets survive `IHttpClientFactory`'s periodic handler-pool rotation.
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
`InvoiceMapper` reads `IDocumentSigningService.Enabled`/`DocVersion` to pick the document version it
declares: 1.0 (unsigned) / 1.1 (signed) normally, 1.2 (unsigned) / 1.3 (signed) for SVDP — so enabling
signing correctly upgrades both regular and SVDP submissions to their signed version.

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

**External-ERP invoices (buyer-side sync).** `InvoiceFullSyncHelper` pulls documents from LHDN's
`documents/search` API, which is TIN-scoped, not submitter-scoped — it returns every document where the
company is a party, including ones submitted directly to LHDN by a different system entirely (an
external ERP), not through EINVWORLD. When such a document is first discovered and the local company is
the buyer, `InvoiceFullSyncHelper` creates the missing `PartyInfo` for the supplier (if EINVWORLD has
never seen them) and the invoice appears in the Buyer's Received tab automatically. See §8 for the
"new e-invoice received" notification this triggers.

---

## 8. Background services

All run as `IHostedService` in the same process (so the IIS app pool should be **AlwaysRunning**):

| Service | Purpose |
|---|---|
| **`DurableSyncJobWorker`** | Durable, SQL-backed job queue. Polls `SyncJobs`, atomically claims a job (`UPDLOCK`/`READPAST`), dispatches by `JobType` to an `ISyncJobHandler`, retries with backoff, and recovers orphaned jobs after a restart. Handles StatusSync / FullImport / SupplierRefresh / **SubmitDocument** (background retry of an interactive LHDN submission that threw, and — since Smart Capture Stage 4 — the same job type an eligible auto-submit schedules with a delay via `NextRunAtUtc`; reuses `InvoiceSubmissionHelper`, no-ops if the invoice is no longer Draft so it can never double-submit; exhausted retries land in the Sync Jobs dead-letter view) / **SmartCaptureExtraction** (OCR/LLM extraction for one uploaded document) / **SmartCaptureRetention** (tiered document/file cleanup, §10) / **WebhookDelivery** (outbound customer-ERP webhook, see §10). |
| **`InvoiceStatusUpdater`** | Periodically polls LHDN for pending invoices' validation status. Also runs the webhook dispatcher (enqueues `WebhookDelivery` jobs for invoices that reached a terminal status; no-op unless `Webhooks:Enabled`), the PDF/validation-email finalizer safety net, the new-e-invoice-received-email safety net (below), and — every 10th cycle — the full LHDN `documents/search` import for every registered company TIN (`InvoiceStatusUpdaterSettings:BackgroundImportLookbackDays`, default 7 days; this is what catches invoices an external ERP submitted directly to LHDN — see §7). |
| **`InvoiceFinalizerService`** | Finalizes invoices once validated (PDF/email/QR follow-ups). |

**Email retry pattern (Valid-status, new-invoice-received, rejection, cancellation).**
`IInvoiceFinalizer.FinalizeInvoiceAsync` (Valid-status PDF + email), `SendNewInvoiceReceivedEmailAsync`
(buyer notification for an externally-submitted invoice, `EmailConfiguration:NewInvoiceReceivedEmailSettings`,
default 7-day recency window, `EmailConfiguration:Notifications:EnableNewInvoiceReceivedEmails` kill
switch), and `SendRejectionEmailAsync`/`SendCancellationEmailAsync` (`EnableRejectionEmails`/
`EnableCancellationEmails`) all use the same atomic-claim-then-send pattern: an
`ExecuteUpdateAsync WHERE <flag> = false` claims the row so concurrent callers can't double-send; a
thrown exception during the actual send rolls the claim back so `InvoiceStatusUpdater`'s background
pass retries it on the next cycle — indefinitely, no age cutoff. `InvoiceHeader.
IsNewInvoiceReceivedEmailSent`/`IsRejectionEmailSent`/`IsCancellationEmailSent` all default to `true`
("not applicable") for every invoice, so normal Sent-invoice submission is unaffected — the reject/
cancel handlers set the relevant flag to `false` only when that invoice is actually rejected/cancelled,
in the same save as the status transition, and attempt the send immediately for a snappy user
experience; the background pass is the safety net for when that immediate attempt fails.
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

**Module access (Role Management)** — a global Razor Pages filter (`ModuleAccessPageFilter`) runs after
each page's own `[Authorize(Roles=...)]` gate and checks the `RoleModulePermission` grid (**Admin →
Role Management**) for the current user's role. Admin always passes; a module with no configured row
defaults to allowed, so this is purely additive on top of existing authorization — it only ever
narrows access an Admin has explicitly restricted, never widens it.

**Company user management** — a Supplier Owner/Admin (`CompanyPermission.ManageUsers`) can invite,
remove, and reassign the role of members within their own company (`Pages/Suppliers/Users.cshtml.cs` /
`RolesPermissions.cshtml.cs`); guarded against self-removal and removing the last Owner.

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
- **Invoice line items** (Create Invoice + Invoice Edit, matching UI): each line's Item/Service section
  is ordered Select Saved Item → Item Code → Description → Classification → Unit, followed by Quantity &
  Pricing, then four optional, collapsed-by-default sections — Discount, Fee/Charge, Taxes (multi-entry,
  unchanged), and line-level Additional Information (Product Tariff Code, Country of Origin — both
  mapped into the submitted UBL payload). An invoice-level **Additional Information** section covers
  Payment & Prepayment/Incoterms, Shipping Recipient, and Customs/Import-Export — all now mapped into
  the submitted LHDN payload as of v1.23.1 (`Delivery.DeliveryParty`, top-level
  `AdditionalDocumentReference` — including `Incoterms`, moved there in v1.23.1 from its previous,
  incorrect `Shipment.ID` placement — `AccountingSupplierParty.AdditionalAccountID`,
  `Shipment.FreightAllowanceCharge`). Line-level discount/fee now correctly nets the LHDN tax base and
  reaches the UBL `AllowanceCharge` (previously silently dropped/miscalculated).
- Submit to MyInvois (UBL 2.1 JSON), poll status, capture LongId/QR, **cancel/reject** within the 72h
  window, view validation errors with a human-readable rejection helper.
- **Manual sync / import / refresh** run as durable background jobs (visible on **Sync Jobs**).
- **Templates** and **recurring invoices**.
- PDF generation (DinkToPdf/Puppeteer) and validated-invoice email notifications.
- **New-e-invoice-received email** — when an external ERP submits an invoice directly to LHDN and the
  local company is the buyer, EINVWORLD's LHDN sync discovers it (§7) and emails the buyer once it's
  synced in, with the same indefinite-retry robustness as the validated-invoice email (§8).

**AI (optional, on-prem, OFF by default)**
- **AI E-Invoice Assistant** (`/Assistant`) — answers MyInvois questions and turns a plain-English
  description into a reviewed invoice **suggestion** (grounded on real LHDN codes + the user's
  customers). Suggest-only; never submits. (`AI` config; Ollama provider by default.)
- **AI Document Capture** (`/Invoices/CreateFromFile`) — upload a digital PDF → extract text (PdfPig) →
  suggestion → review. Draft-safe, synchronous. (`DocumentCapture` config; needs `AI:Enabled`.) Being
  superseded by Smart Capture below; kept unadvertised in nav as a rollback path.
- **Smart Capture** (`/Invoices/SmartCapture`, labelled **"Create from Document"** in nav) — the
  persisted/async successor to AI Document Capture, built in 5 staged, deliberately reduced increments
  (`SmartCapture` config; needs `DocumentCapture` + `AI:Enabled`; OFF by default in Development/Production,
  Staging-only pending real-Ollama sign-off):
  - **Stage 1 — assisted capture.** Upload a PDF/JPG/PNG → durable background job (extract text/OCR → AI
    suggestion → `InvoiceSuggestionValidator` review checklist) → the user always explicitly confirms the
    LHDN document type and buyer (never auto-decided) → draft via the unchanged `InvoiceDraftService`. No
    application-level malware scanning (file-type/signature validation, size/page/quota limits, and
    tenant-scoped storage instead — see `IIS-DEPLOYMENT-GUIDE.md` PART 17d).
  - **Stage 1.5 — duplicate flag + condensed review.** An exact-content re-upload within the same company
    is flagged as a review Warning (never blocked); an extraction with zero errors/warnings gets a
    condensed "all checks passed" view instead of the full checklist expanded by default.
  - **Stage 2 — learned company hints.** A per-company row (`SmartCaptureCompanyHints`) tracks the most
    commonly confirmed doc type/currency/tax via a streaming majority vote, surfaced as advisory-only
    context in the AI prompt once a company has confirmed a few drafts. Never sets a field directly.
  - **Stage 3 — bulk upload.** The upload form accepts multiple files at once
    (`SmartCapture:MaxFilesPerBulkUpload`), each going through the identical per-file pipeline as a single
    upload; one bad file in a batch never blocks the others.
  - **Stage 4 — conditional automatic submission.** A system Admin can opt a company into unattended LHDN
    submission of Smart Capture drafts (`/Admin/SmartCaptureAutoSubmit`, never company self-service),
    gated by a global kill switch (`SmartCapture:AutoSubmitEnabled`, default false), a per-company doc-type
    allowlist + value ceiling, and a deterministic per-document check (zero review issues, matched buyer,
    under ceiling) re-evaluated on every confirmation — never a fuzzy confidence score. Reuses the
    existing `SubmitDocumentJobHandler`/`InvoiceSubmissionHelper` pipeline unchanged (idempotency/signing/
    retry/audit untouched) via a delayed, cancellable job; the Smart Capture list page shows a countdown +
    Cancel button during the delay window.
  - See `CLAUDE.md` § "Invoice-input mechanisms" for the standing architecture rule these all follow
    (capture is invoice-*input* only, never a parallel subsystem) and the Stage 4 exception's reasoning.
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

**Admin & ops** — user/company management, **Role Management** (global role catalog + per-role module
access grid), resources (CMS), system logs, **Sync Jobs**, **Audit Trail**, **System Health**,
**Webhooks**, global theme, dashboards/KPIs.

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
| `LHDNApiConfig` | MyInvois `BaseUrl`/`ValidationBaseUrl`, `ClientId`, **secrets** (`ClientSecret`/`2`), `OnBehalfOf`, `SigningEnabled`, `DocVersion`, `SvdpEnabled` (1.2 unsigned / 1.3 signed), `CertPath`/`CertPass`, `SigningKeyProvider` (certificate custody — `File` default; vault/HSM drop-in), `SyncRetentionDays`, `RateLimits:*` (per-endpoint requests-per-minute; see §6). |
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
| `AI` | Provider-agnostic AI: `Enabled`, `Provider` (Ollama today), `BaseUrl`, `Model` (default `gemma3:12b`), `TimeoutSeconds` (default 120 — may need raising for a large model's first cold load), `KeepAliveMinutes` (default 30; sent as Ollama's `keep_alive` so the model stays resident between requests instead of unloading after Ollama's own 5-minute default, avoiding a repeat cold-load penalty), `Temperature`, `MaxTokens`, `ApiKey` (**secret**, cloud providers only — env var). The old `AIAssistant` section is retired — rename any `AIAssistant__*` env vars to `AI__*`. |
| `DocumentCapture` | AI Document Capture: `Enabled`, `MaxFileSizeMb`, `MaxPages`. |
| `SmartCapture` | Persisted/async Smart Capture (needs `DocumentCapture`+`AI:Enabled`): `Enabled` (OFF in Development/Production, Staging-only), `AllowedExtensions`, `MaxFileSizeMb`, `MaxPages`, retention day tiers, `MonthlyProcessedPageQuota`, `MaxFilesPerBulkUpload` (Stage 3, default 20), `AutoSubmitEnabled` (Stage 4 global kill switch, default **false everywhere** — a company's opt-in via `/Admin/SmartCaptureAutoSubmit` has no effect while this is false). No application-level malware scanning — see `IIS-DEPLOYMENT-GUIDE.md` PART 17d. |
| `WatchedFolderImport` | `Enabled`, `InboxPath`, `PollSeconds`. |
| `Api:Key` | **Secret** — enables `POST /api/import/validate`. |
| `ExtractInvoice:ServiceUrl` | Legacy OCR service endpoint. |
| `CodeFilePaths` | Locations of the reference-code seed JSON files. |

---

## 13. Database & migrations

- **EF Core 10 / SQL Server**, two databases: `EINVWORLD` (main, `ApplicationDbContext`) and
  `EINVWORLDWEBSITE` (`WebsiteDbContext`).
- **87 migrations** under `Migrations/` (across both contexts; 22 pre-v1.11.0 migrations were squashed
  into one, `ConsolidatedSchemaCatchup_v1_11_0`). Two new additive migrations in v1.13.0:
  `AddRoleModulePermissions` (new `RoleModulePermissions` table) and `AddCompanyRolePartyInfoScope`
  (nullable `CompanyRole.PartyInfoId`). One more in v1.14.0: `AddNewInvoiceReceivedEmailTrackingToInvoiceHeader`
  (3 new `InvoiceHeaders` columns backing the new-e-invoice-received notification, §8). One more in
  v1.14.1: `AddRejectionCancellationEmailTrackingToInvoiceHeader` (7 new `InvoiceHeaders` columns
  backing retry-safe rejection/cancellation emails, same section). Three more for Smart Capture (§10):
  `AddSmartCaptureDocument` (v1.17.0, the core `SmartCaptureDocuments` table), `AddSmartCaptureCompanyHint`
  (v1.18.0, Stage 2's learned-hints table), and `AddSmartCaptureAutoSubmit` (v1.20.0, Stage 4's
  `SmartCaptureAutoSubmitSettings` table + `SmartCaptureDocuments.PendingAutoSubmitJobId`). One more in
  v1.22.0: `AddLineTariffOriginAndHeaderShippingCustoms` (`InvoiceLines.ProductTariffCode`/
  `CountryOfOrigin`/`DiscountReason`/`FeeChargeAmount`/`FeeChargeReason`, plus `InvoiceHeaders` Shipping
  Recipient + Customs/Import-Export columns, backing the Invoice Items redesign, §10) — see
  `DEPLOY-NOTES.md` §1 for the apply order. Auto-apply on startup
  (`AutoMigrateOnStartup=true`) is the default in Development/Staging — they are **additive** (new
  tables/columns/indexes; no `Up()` drops), so existing data is preserved. **Production overrides this to
  `AutoMigrateOnStartup=false`**: migrations there always apply manually as a controlled deploy step, not
  automatically on boot. **Always back up first** and ensure the runtime/migration SQL login has DDL
  rights.
- Every migration has an idempotent **`Apply_*.sql`** for the manual path; ordered list and current
  environment-catch-up guidance in [`DEPLOY-NOTES.md`](DEPLOY-NOTES.md) §1.
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
