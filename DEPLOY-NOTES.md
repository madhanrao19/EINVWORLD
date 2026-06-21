# EINVWORLD — On-Prem Deployment Notes (IIS / Windows Server / SQL Server)

Practical checklist for deploying to a self-hosted Windows + IIS + SQL Server box.
Production runs with `DatabaseSettings:AutoMigrateOnStartup = false`, so **schema changes are a
manual, controlled step** using the idempotent `Apply_*.sql` scripts below.

## 1. Database migrations (run in this order)

Each script is idempotent (guards on `__EFMigrationsHistory` / `COL_LENGTH` / `OBJECT_ID`) and safe to
re-run. Run against **staging first**, verify, then production. Use a migration-only SQL login
(`db_ddladmin`), not the app's runtime login.

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
- `DataProtection__KeyRingPath` — **must point OUTSIDE the App folder** (e.g. `D:\EINVWORLD\Keys`) so a
  redeploy that clears `App\` doesn't wipe the keys (which would log everyone out and break 2FA/antiforgery).

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

## 6. PDF engine

`PDFGenerationSettings:Engine` is `DinkToPdf` (default; loads `wkhtmltox\libwkhtmltox.dll` natively) or
`Puppeteer` (headless Chromium — set `ChromiumExecutablePath` on an offline server). The native DLL is
only loaded for the `DinkToPdf` engine.

## 7. SQL Server backups (operational, outside the app)

- Full daily + log backups every 15–30 min (FULL recovery model) + monthly **restore test**.
- Use **encrypted backups** — and back up the encryption certificate/key separately (without it the
  backup can't be restored).
- Runtime login: least privilege (`db_datareader` + `db_datawriter` + execute), **not** `sa`/`db_owner`.

## 8. Rollback

Deploy to `App_New`, smoke-test, then swap to `App` (keep `App_Old`). Never delete `Documents\`,
`Logs\`, `Cert\`, or `Keys\` during a deploy. DB changes are additive/idempotent; keep a pre-deploy
full backup so you can restore if needed.
