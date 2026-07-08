# EINVWORLD — On-Prem Deployment Notes (IIS / Windows Server / SQL Server)

Practical checklist for deploying to a self-hosted Windows + IIS + SQL Server box.

## 0. Upgrading an existing installation

Follow this order when moving an already-running server to a newer build. It is safe because schema
changes are additive and AI/features stay off unless already enabled.

1. **Back up first (non-negotiable).**
   - Full **database** backup (your rollback point).
   - Copy the current **`App\`** folder (fast binary rollback) and note the running version
     (`AppInfo:Version`, shown in the footer / `appsettings.json`).
   - Confirm the **DataProtection key ring** lives OUTSIDE `App\` (`DataProtection__KeyRingPath`,
     e.g. `D:\EINVWORLD\Keys`). If it doesn't, set it **before** upgrading — otherwise this deploy
     will rotate the keys and log everyone out / break 2FA. Startup now fails fast if it's blank in
     Production, so verify it is set.
2. **Stop the site** (or the app pool) so no requests hit a half-swapped folder.
3. **Deploy the new build** into `App\` (keep `appsettings.Production.json`, `web.config` env, and the
   key-ring folder intact — never overwrite server secrets). Get the build from the green **CI run on
   `main`** → **Artifacts → `einvworld-app`** (a ready `dotnet publish` output; carries no secrets).
4. **Config/env changes for this version:**
   - **AI (if you use it):** the `AIAssistant__*` environment variables are **retired** — rename them to
     `AI__*` (`AIAssistant__Enabled` → `AI__Enabled`, `AIAssistant__Model` → `AI__Model`, etc.).
     If you skip this, AI simply stays **off** after the upgrade — invoicing is unaffected. Default
     model is now `gemma3:12b`; pull it with `ollama pull gemma3:12b` if you switch models.
     For machine/user-scope env vars you can automate the rename with the helper script (run elevated):
     `powershell -ExecutionPolicy Bypass -File scripts\Rename-AiEnvVars.ps1 -AppPool '<YourAppPool>'`
     (add `-WhatIf` first to preview). If you set the variables in the IIS app-pool dialog or a
     server-side `web.config` instead, rename them there by hand and recycle the pool.
   - Re-check any other env vars against **SECRETS-SETUP.md** (no new required secrets in this release).
5. **Database migrations** run automatically on first boot (see §1) — additive only. Ensure the SQL login
   has DDL rights and start in a **low-traffic window** with a **single** worker process.
6. **Start the site**, then **verify**:
   - `/health` returns Healthy; sign-in works; open an existing invoice; create + submit one to LHDN.
   - If AI is enabled: **Admin → AI Settings → Test connection** reports reachable + model pulled.
   - Check the startup logs for no configuration-validation errors (the app fail-fasts on bad prod config).
7. **Rollback if needed:** stop the site, restore the previous `App\` folder, and (only if a migration
   caused the problem) restore the database backup. Additive migrations rarely need a DB restore.

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
sqlcmd %DB% -i "Migrations\Apply_EncryptPiiFields.sql"
sqlcmd %DB% -i "Migrations\Apply_AddWebhookSubscriptions.sql"
sqlcmd %DB% -i "Migrations\Apply_AddInvoiceHeaderRowVersion.sql"
```

> v1.8.2 note: `Apply_AddInvoiceHeaderRowVersion.sql` adds a `rowversion` column to `InvoiceHeaders`
> (optimistic concurrency). It takes a brief schema lock on that table — run it (or first-boot
> auto-migrate) in a quiet window on large databases.

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
  `AI:Enabled=true` (needs Ollama; see IIS guide PART O). Verify with Admin → AI Settings → Test
  connection. Digital (text-layer) PDFs only; scanned images report "needs OCR".
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

## 8. Log retention (`SystemLogs` table)

`LogCleanupService` prunes `SystemLogs` rows older than `LogCleanupSettings:RetentionDays` (default 30)
every 4 hours. It deletes in **batches** of `LogCleanupSettings:BatchSize` (default 5000) so it never
holds a table lock or hits the command timeout on a large table.

- If you upgrade onto a server with a **large pre-existing `SystemLogs` backlog**, the first few cleanup
  cycles will drain it gradually (5000 rows per batch, looping until caught up) — this is expected; the
  table shrinks over the following runs, not instantly.
- To prune faster on a one-off basis, raise `BatchSize` (e.g. 50000); to keep more history, raise
  `RetentionDays`. Both live under `LogCleanupSettings` in `appsettings.Production.json`.

## 9. Rollback

Deploy to `App_New`, smoke-test, then swap to `App` (keep `App_Old`). Never delete `Documents\`,
`Logs\`, `Cert\`, or `Keys\` during a deploy. DB changes are additive/idempotent; keep a pre-deploy
full backup so you can restore if needed.
