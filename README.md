# EINVWORLD (eInvWorld)

E-invoicing middleware for **Malaysia's LHDN MyInvois** system. EINVWORLD lets companies create,
validate, submit, track and manage e-invoices against MyInvois, with a web UI for suppliers, buyers and
admins. It is designed to run **self-hosted on a single in-house Windows / IIS server**.

> Built with a **FOSS-only** dependency policy — all libraries are free/open-source.

---

## Tech stack

- **.NET 10 (LTS)** — ASP.NET Core, Razor Pages + MVC API controllers
- **EF Core 10** on **SQL Server**
- **ASP.NET Core Identity** (roles: Admin, Supplier, Buyer)
- **Serilog** (file + SQL `SystemLogs`)
- PDF: **DinkToPdf** (default) or **Puppeteer** (switchable)
- Excel export: **ClosedXML**; images: **Magick.NET**

## Key features

- Full support for the **8 LHDN document types**: `01` Invoice, `02` Credit Note, `03` Debit Note,
  `04` Refund Note, `11` Self-billed Invoice, `12` Self-billed Credit Note, `13` Self-billed Debit Note,
  `14` Self-billed Refund Note.
- Submit to MyInvois (UBL 2.1 JSON), poll validation status, capture **LongId** (QR code), cancel/reject.
- **Centralised LHDN rate limiting** (`LhdnRateLimitHandler`) — evenly paced per endpoint so the system
  stays under MyInvois limits and avoids `429` storms.
- **Durable background jobs** — manual sync/import/refresh run as **SQL-backed** jobs that survive an
  IIS app-pool recycle / reboot: a worker claims each job, retries with backoff, and recovers orphaned
  jobs on startup. Progress + **Retry/Cancel** on the **Sync Jobs** admin page (`/Admin/SyncJobs`).
- **Tamper-evident audit trail** — append-only, hash-chained `AuditLogs` (LHDN submit/cancel/reject),
  with one-click chain verification on **Admin → Audit Trail**.
- **Admin 2FA enforced** (block-until-enrolled; `Security:EnforceAdminMfa`, default on) and
  **duplicate-submission idempotency** (replays an identical submission within 10 min instead of
  duplicating at LHDN).
- **Health + ops** — `/health/live` and `/health/ready` probes, an **Admin → System Health** dashboard,
  fail-fast startup config validation, and CSP violation reporting (`/csp-report`).
- **Invoice ingestion** (draft-safe — validate/suggest only, never auto-create or submit): **AI Document
  Capture** (PDF → reviewed suggestion), **Bulk Import** (CSV/XLSX validation + template), a
  **watched-folder** importer, and a **REST validate API** (`POST /api/import/validate`). All OFF by
  default.
- **AI E-Invoice Assistant** (optional, OFF by default) — a local, on-prem [Ollama](https://ollama.com)
  LLM that answers e-invoicing questions and turns a plain-English description into a suggested invoice
  that pre-fills the Create Invoice form. **No invoice data leaves the server**; it only suggests, never
  submits. See deployment guide PART O.
- **v1.1 digital signing** (XAdES) is built and config-gated **OFF** until a signing certificate is
  purchased — flip `LHDNApiConfig:SigningEnabled` to enable.
- Security: per-TIN ownership checks (IDOR protection), secrets externalised, security response headers,
  client-side rate limiting, configurable DataProtection key-ring path.

---

## Getting started (development)

**Prerequisites:** .NET 10 SDK, SQL Server (or LocalDB), and (optional) Ollama for the AI assistant.

```bash
# 1) Configure secrets (connection string, LHDN PREPROD creds, etc.) — see SECRETS-SETUP.md
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=eInvWorld;Trusted_Connection=True;TrustServerCertificate=True"

# 2) Apply database migrations (or let it auto-migrate on first run)
dotnet ef database update --context ApplicationDbContext

# 3) Run
dotnet run
```

Then browse to the HTTPS URL shown (default `https://localhost:7073`).

> Migrations auto-apply on startup when `DatabaseSettings:AutoMigrateOnStartup = true` (the default). Set it
> to `false` in production and apply migrations as a controlled deploy step.

### Tests

```bash
dotnet test
```

---

## Configuration

Most behaviour is driven by `appsettings.json`. Highlights:

| Section | Purpose |
|---|---|
| `ConnectionStrings` | Database connections (**secret** — left blank, supplied via user-secrets / env vars). |
| `LHDNApiConfig` | MyInvois endpoints, client id, **secrets**, `SigningEnabled`, `DocVersion`, `SyncRetentionDays`. |
| `DataProtection:KeyRingPath` | Where encryption keys live — point **outside** `App\` on the server. **Required in Production** (startup fails if blank); preset to `E:\EINVWORLD\Keys` in `appsettings.Production.json`. |
| `DatabaseSettings:AutoMigrateOnStartup` | Auto-apply EF migrations on boot. `true` in `appsettings.Production.json` — migrations are additive (data preserved), but **back up first**. Set `false` to apply `Apply_*.sql` manually. |
| `Security:EnforceAdminMfa` | Require Admins to enrol 2FA (default `true`; no lockout — they self-enrol). |
| `Security:HttpsRedirectPort` | Public HTTPS port for redirects (default `443`); set explicitly behind IIS so the port isn't guessed. `0` = disable (use behind a TLS-terminating proxy / Cloudflare Tunnel). |
| `ForwardedHeaders` | Reverse-proxy / Cloudflare Tunnel support (default on). Honours `X-Forwarded-Proto` (scheme) and `X-Forwarded-For` (real client IP) from a trusted proxy — needed so cookies, redirects, rate limiting and audit IPs are correct when TLS terminates upstream. |
| `PDFGenerationSettings:Engine` | `DinkToPdf` (default) or `Puppeteer` — see note below. |
| `AIAssistant` | Optional local-LLM assistant (OFF by default). |
| `DocumentCapture` | Optional AI Document Capture (PDF → suggestion; OFF; needs `AIAssistant:Enabled`). |
| `WatchedFolderImport` | Optional Inbox folder validator (OFF; set `InboxPath`). |
| `Api:Key` | **Secret** — enables `POST /api/import/validate` for an external ERP (header `X-Api-Key`). Blank = disabled. |
| `InvoiceStatusUpdaterSettings` | Background status-sync polling cadence & UI cooldowns. |

> **PDF engine note.** The default `DinkToPdf` (wkhtmltopdf) engine is **unmaintained / end-of-life**
> upstream, but it is kept as the default because it renders fully **offline** — the right choice for an
> on-prem / air-gapped server. The alternative `Puppeteer` engine is actively maintained but downloads a
> Chromium build **at runtime on first use**, which needs outbound network access (or a pre-staged
> Chromium). To switch, set `PDFGenerationSettings:Engine` to `Puppeteer`; on an air-gapped host,
> pre-download Chromium and point PuppeteerSharp at it so there is no runtime fetch.

**Secrets are never committed.** See **[SECRETS-SETUP.md](SECRETS-SETUP.md)** for the full list and how to
set them in dev (user-secrets) and on the server (environment variables).

---

## Documentation

- **[DOCUMENTATION.md](DOCUMENTATION.md)** — complete system & architecture reference.
- **[SECRETS-SETUP.md](SECRETS-SETUP.md)** — all secrets and how to configure them.
- **[IIS-DEPLOYMENT-GUIDE.md](IIS-DEPLOYMENT-GUIDE.md)** — step-by-step production deployment to IIS
  (DataProtection keys, backup + auto-migration, admin 2FA, optional Ollama setup).
- **[DEPLOY-NOTES.md](DEPLOY-NOTES.md)** — concise on-prem operator checklist (migration order,
  app-pool settings, health endpoints, backups, rollback).
- **[CHANGELOG.md](CHANGELOG.md)** — release history and notable fixes.

---

## License / dependencies

All runtime dependencies are free / open-source (FOSS-only policy). Commercial-licensed packages have been
replaced with FOSS equivalents (see CHANGELOG).
