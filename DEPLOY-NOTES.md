# EINVWORLD — On-Prem Deployment Notes (IIS / Windows Server / SQL Server)

Practical checklist for deploying to a self-hosted Windows + IIS + SQL Server box.

## 1. Database migrations

**Default: automatic.** `appsettings.Production.json` ships with
`DatabaseSettings:AutoMigrateOnStartup = true`, so on the first start of a new version the app applies
any pending EF migrations itself. The migrations are **additive** (new tables/columns/indexes — no
`Up()` drops data), so existing data is preserved. Before that first start you MUST:

1. **Take a full DB backup** (your rollback).
2. Ensure the app's SQL login (`einvworldusr`) has **DDL rights** (`db_ddladmin`/`db_owner`).
3. Deploy in a **low-traffic window** (the first boot runs the schema changes and briefly locks the
   affected tables) and keep the app pool at a **single worker process**.

### Manual alternative (optional)

If you prefer to control schema changes yourself, set `AutoMigrateOnStartup = false` and run the
idempotent `Apply_*.sql` scripts below in order (staging first, then production) with a migration login
(`db_ddladmin`). Each guards on `__EFMigrationsHistory` / `COL_LENGTH` / `OBJECT_ID` and is safe to re-run.

```bat
set DB=-S <sql-host> -d <database> -E -b
sqlcmd %DB% -i "Migrations\Apply_SyncModelAfterNet10Upgrade.sql"
sqlcmd %DB% -i "Migrations\Apply_AddSyncJobTable.sql"
sqlcmd %DB% -i "Migrations\Apply_DecoupleSystemLogsFromEf.sql"
sqlcmd %DB% -i "Migrations\Apply_FixInvoiceDecimalPrecision.sql"
sqlcmd %DB% -i "Migrations\Apply_AddInvoiceHotPathIndexes.sql"
sqlcmd %DB% -i "Migrations\Apply_AddInvoiceSubmissionClaim.sql"
sqlcmd %DB% -i "Migrations\Apply_AddSyncJobDurability.sql"
sqlcmd %DB% -i "Migrations\Apply_AddSubmissionRecords.sql"
sqlcmd %DB% -i "Migrations\Apply_AddAuditLog.sql"
```

Verify the expected migrations are recorded:
```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
```

> The durable job worker, idempotency guard, and audit service all **degrade gracefully** (log a
> warning, pause/skip) if their table is missing — a slightly-late migration won't crash the app, but
> the corresponding feature won't work until applied.

## 2. Secrets & configuration (never commit these)

Set on the server via environment variables or user-secrets — see `SECRETS-SETUP.md`:

- `ConnectionStrings__DefaultConnection`
- `LHDNApiConfig__ClientSecret`, `LHDNApiConfig__ClientSecret2`, `LHDNApiConfig__CertPass` (if signing)
- `EmailConfiguration__Default__SmtpPassword`
- `Turnstile__SecretKey`
- `Api__Key` — **optional**; set only to enable the import REST API (`POST /api/import/validate`).
- `DataProtection__KeyRingPath` — **must point OUTSIDE the App folder** (preset to `E:\EINVWORLD\Keys`
  in `appsettings.Production.json`) so a redeploy that clears `App\` doesn't wipe the keys (which would
  log everyone out and break 2FA/antiforgery). **Required in Production — the app won't start if blank.**

Startup runs `ProductionConfigValidator`, which **fails fast with one clear message** if a critical
setting is blank/wrong (connection string, key ring, signing cert, localhost URLs in Production, etc.).

## 3. IIS / App Pool

- Install the latest **.NET Hosting Bundle** (patch monthly; restart IIS after a runtime update).
- App Pool: **No Managed Code**, **Start Mode = AlwaysRunning**, **Idle Time-out = 0**,
  **Regular Time Interval (recycle) = 0** or scheduled at a low-traffic hour. Enable
  **Preload** on the site. (The durable SQL queue does not *depend* on always-running, but it avoids
  needless cold starts.)
- App Pool identity: a dedicated least-privilege account (or `ApplicationPoolIdentity`) with:
  - `App\` Read/Execute
  - `Documents\`, `Logs\`, `Temp\`, `Keys\` Modify
  - `Cert\` Read
- Confirm `web.config` is present at the app root (without it, IIS can serve `.deps.json` etc.).
- Set `ASPNETCORE_ENVIRONMENT=Production`.

## 4. Health monitoring

- `/health/live` — process is up (use for IIS Application Initialization / load-balancer liveness).
- `/health/ready` — DB reachable **and** Documents/GeneratedPdf/DataProtection folders writable.
- `/health` — all checks (back-compat).
- **Admin → System Health** page shows queue depth, failed/oldest jobs, signing-cert expiry, disk
  space, and DataProtection key-ring status.

Point Uptime Kuma / PRTG / Zabbix at `/health/ready`.

## 5. Security

- **Admin 2FA is enforced** (`Security:EnforceAdminMfa = true`): an admin without 2FA is redirected to
  the authenticator-setup page until they enrol (no hard lockout). Emergency escape hatch: set it
  `false` and recycle.
- **Audit trail** is hash-chained and append-only — never `UPDATE`/`DELETE` `AuditLogs`. Verify
  integrity any time from **Admin → Audit Trail → Verify chain integrity**.

## 5b. Optional ingestion features (all OFF by default)

Draft-safe — they validate/suggest only; none creates or submits invoices automatically.

- **AI Document Capture** (`/Invoices/CreateFromFile`) — set `DocumentCapture:Enabled=true` **and**
  `AIAssistant:Enabled=true` (needs Ollama; see IIS guide PART O). Digital (text-layer) PDFs only;
  scanned images report "needs OCR".
- **Bulk Import** (`/Invoices/BulkImport`) — always available to Admin/Supplier; download the template,
  upload CSV/XLSX, get a per-row validation report. No config needed.
- **Watched-folder importer** — set `WatchedFolderImport:Enabled=true` and `InboxPath`
  (e.g. `E:\EINVWORLD\Inbox`); grant the app-pool **Modify**. Files are validated and moved to
  `Processed/` / `Rejected/` with a `.report.json`.
- **REST validate API** — set `Api:Key`; callers POST to `/api/import/validate` with header `X-Api-Key`.

## 6. PDF engine

`PDFGenerationSettings:Engine` is `DinkToPdf` (default; loads `wkhtmltox\libwkhtmltox.dll` natively) or
`Puppeteer` (headless Chromium — set `ChromiumExecutablePath` on an offline server). The native DLL is
only loaded for the `DinkToPdf` engine.

## 7. SQL Server backups (operational, outside the app)

- Full daily + log backups every 15–30 min (FULL recovery model) + monthly **restore test**.
- Use **encrypted backups** — and back up the encryption certificate/key separately (without it the
  backup can't be restored).
- Runtime login: least privilege (`db_datareader` + `db_datawriter` + execute), **not** `sa`/`db_owner`.
  Exception: with **auto-migrate on**, the runtime login also needs `db_ddladmin` (to create/alter
  schema on boot). For strict least-privilege, set `AutoMigrateOnStartup=false` and run the
  `Apply_*.sql` scripts with a separate DDL login instead.

## 8. Rollback

Deploy to `App_New`, smoke-test, then swap to `App` (keep `App_Old`). Never delete `Documents\`,
`Logs\`, `Cert\`, or `Keys\` during a deploy. DB changes are additive/idempotent; keep a pre-deploy
full backup so you can restore if needed.
