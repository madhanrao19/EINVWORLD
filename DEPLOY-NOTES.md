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
   - **v1.9.7 data fix (one-time):** builds between v1.8.2 and v1.9.7 accepted documents at LHDN
     but failed to persist the UUID/status locally — those invoices still look like Drafts and could
     be resubmitted as duplicates. After the new build is running, open
     `scripts/Reconcile-OrphanedSubmissions.sql` and follow its steps **per environment**: run
     SECTION 1 to list candidates, verify each at LHDN (MyInvois portal or server logs), fill the
     verified UUID/SubmissionUid rows into the script, then run SECTION 2. Do **not** run it
     verbatim — the fill-in rows are environment-specific (it refuses to run with none filled in).
     It is idempotent and never overwrites an already-recorded submission.
   - **v1.11.0 (this release):** Buyer Management, Company Management (new tabbed workspace — Users,
     Roles & Permissions, Invoice Branding, Security, Audit), AI Assistant, Items & Services, and the
     Admin sidebar were all restyled to Tabler and the Company workspace gained real backing features
     (company-scoped roles, token-based user invitations, invoice branding settings). **No new required
     secrets** — user invitations reuse the existing `EmailConfiguration` SMTP settings (same sender as
     other app emails). See §1 for the 4 new migrations and the two-step PII encryption note.
   - **Tabler UI migration (v1.12.0, markup complete):** the authenticated UI has been migrated from the
     Velzon theme to the self-hosted MIT **Tabler** theme (assets under `wwwroot/tabler/`, no CDN). It
     ships as a normal build — no extra deploy step. As of 2026-07-27 **all authenticated pages render
     Tabler with no known remaining raw Velzon markup**, Playwright-verified across all three roles
     (`tests/playwright/05-responsive.spec.js`, `10-tabler-modules.spec.js`) with real viewport sizing.
     To re-run the check after a deploy: set Cloudflare **test** Turnstile keys and disable admin MFA
     *temporarily* for QA (exact env vars in `docs/TABLER-MIGRATION-AUDIT.md`), run the two specs above,
     then **revert** those env vars. Velzon `_Layout`/`_LoginLayout` remain the fallback until Phase 8
     (retiring the theme entirely); to roll a folder back to Velzon, delete its
     `Pages/<area>/_ViewStart.cshtml` (or restore the one line in `Areas/Identity/Pages/_ViewStart.cshtml`
     for the auth pages). **Not Tabler, still open — fix separately:** company logos emitted as
     `file:///E:/…png` paths (Suppliers/Index → browser-blocked) and some resource images 404 on
     `/Admin/Resources/Manage`.
   - **v1.13.0 (this release):** new Admin → Role Management (global role catalog + per-role module
     access grid) and company-scoped custom roles (Supplier Owners/Admins can define a role limited to
     just their own company). Also a bug-fix pass (supplier invitation join, LHDN reject/cancel emails,
     several access-denied fixes) and MyInvois SDK 1.0 compliance work (unit-code validation, signed
     SVDP 1.3, configurable LHDN rate limits — see `LHDNApiConfig:RateLimits:*`). **No new required
     secrets.** See §1 for the 2 new migrations — both additive, no data loss, safe to auto-migrate.
5. **Database migrations** run automatically on first boot (see §1) — additive only. Ensure the SQL login
   has DDL rights and start in a **low-traffic window** with a **single** worker process.
6. **Start the site**, then **verify**:
   - `/health` returns Healthy; sign-in works; open an existing invoice; create + submit one to LHDN.
   - If AI is enabled: **Admin → AI Settings → Test connection** reports reachable + model pulled.
   - Check the startup logs for no configuration-validation errors (the app fail-fasts on bad prod config).
7. **Rollback if needed:** stop the site, restore the previous `App\` folder, and (only if a migration
   caused the problem) restore the database backup. Additive migrations rarely need a DB restore.

## 1. Database migrations

### ⚠️ v1.11.0 pre-flight: confirm how far behind each environment is

A production backup taken 2026-07-26 (`__EFMigrationsHistory` check) showed the live database was on
migration `20260415075935_RemovePreFix` — **22 migrations behind head**, i.e. it had not been
schema-migrated since mid-April even though `AutoMigrateOnStartup` policy and code shipped well past
that point. `AutoMigrateOnStartup=false` in Production means migrations only ever apply when someone
runs them as a deploy step — if that step gets skipped on a release, the gap silently grows until the
next person notices (usually because a newer feature 500s on a missing column/table). Those 22
migrations have since been **squashed into one**, `20260726135229_ConsolidatedSchemaCatchup_v1_11_0`
(see below) — but **Staging auto-migrates by default**, so it may have already applied some or all of
the original 22 individually, under their own IDs, before they were squashed. Run this on Staging and
Production before deploying to see exactly where each stands:

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
```
If the last row is `20260415075935_RemovePreFix` or earlier, that environment is fully behind — the
squashed migration/script builds everything from scratch. If it's one of the 22 original IDs (e.g.
`20260726085937_AddUnitAndPriceToItemDescription`), that environment already has the schema — the
squashed script's per-object guards will detect this and only insert its own history row (a no-op on
the schema). **Either way, run `Apply_ConsolidatedSchemaCatchup_v1_11_0.sql` once, manually, on Staging
before its next deploy** — do not let Staging's normal auto-migrate-on-boot be the first thing to try
applying the new squashed migration ID, since the app's own `Migrate()` call runs the raw (non-guarded)
`Up()` and will fail with "object already exists" on anything Staging already has. Running the guarded
SQL script first (safe on any starting state, per above) marks the new migration ID as applied, so the
next boot's `Migrate()` sees it as already done and skips it.

This was rehearsed for this release in all three states — a full restore of the 2026-07-26 production
backup, a copy of the same backup with all 22 original migrations pre-applied under their own IDs
(simulating an already-caught-up Staging), and re-running the script a second time against each — with
`dotnet ef database update` and the `Apply_*.sql` script producing identical, correct end states and
zero errors in every case, including confirming `SystemLogs` (111k+ existing rows) is never dropped.

**Default: automatic.** `appsettings.json` ships with `DatabaseSettings:AutoMigrateOnStartup = true`
(inherited by Staging, which has no override), so on the first start of a new version the app applies
any pending EF migrations itself. `appsettings.Production.json` overrides this to **`false`** — Production
always requires the manual step below as a matter of policy, run in a controlled window. The migrations
in this release are **additive** (new tables/columns/indexes — no `Up()` drops data), so existing data is
preserved regardless of which path you use. Before the first start on a version bump you MUST:

1. **Take a full DB backup** (your rollback).
2. Ensure the app's SQL login (`einvworldusr`) has **DDL rights** (`db_ddladmin`/`db_owner`) — or use a
   separate migration login for the manual path (see §7 exception).
3. Deploy in a **low-traffic window** (the first boot runs the schema changes and briefly locks the
   affected tables) and keep the app pool at a **single worker process**.

### Manual alternative (Production's default — required, not optional)

With `AutoMigrateOnStartup = false`, run the idempotent `Apply_*.sql` scripts below **in order** (staging
first, then production) with a migration login (`db_ddladmin`). Each guards on `__EFMigrationsHistory` /
`COL_LENGTH` / `OBJECT_ID` and is safe to re-run — running the full list against an already-migrated
database is a no-op.

```bat
set DB=-S <sql-host> -d <database> -E -b
sqlcmd %DB% -i "Migrations\Apply_ConsolidatedSchemaCatchup_v1_11_0.sql"
```

> **v1.11.0 note — this one script replaces 22 individually-numbered migrations.** The originals
> (`AddLhdnIntermediaryRejectedFlag` through `AddUnitAndPriceToItemDescription`, spanning 2026-04-23 to
> 2026-07-26) were squashed into `20260726135229_ConsolidatedSchemaCatchup_v1_11_0` because Production had
> never applied any of them and was 3.5 months behind — see the pre-flight note above for why Staging
> needs the script run manually once before its next deploy, even though it normally auto-migrates.

> v1.8.2 note: the squashed migration adds a `rowversion` column to `InvoiceHeaders` (optimistic
> concurrency, originally `AddInvoiceHeaderRowVersion`). It takes a brief schema lock on that table — run
> it (or first-boot auto-migrate) in a quiet window on large databases.

> **v1.11.0 note — PII encryption is two steps, not one.** The column-widening in this migration only
> **widens** the bank-account/address columns (`nvarchar(150)` → `nvarchar(max)`) so they can hold
> ciphertext — it does **not** encrypt any existing rows. After this migration lands (and the app is
> confirmed running with a valid `DataProtection:KeyRingPath`), go to **Admin → System Health → Encrypt
> PII** and run the backfill once per environment. It's idempotent (`PiiEncryptionBackfillService`) — safe
> to click again if unsure whether it already ran. Until it's run, existing bank/address data stays in
> plaintext (still readable — no functional break) but isn't yet protected at rest.

> **v1.11.0 note — new `CompanyRoles` seed data.** The migration seeds 4 fixed rows (`CompanyRoleId` 1–4:
> Owner/Admin/Editor/Viewer) only if the table is empty. This is a brand-new table so there's no collision
> risk, but don't manually insert rows with `CompanyRoleId` 1–4 into that table before running this script.

Verify the expected migration is recorded:
```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
-- last row should be 20260726135229_ConsolidatedSchemaCatchup_v1_11_0
```

> The durable job worker, idempotency guard, and audit service all **degrade gracefully** (log a
> warning, pause/skip) if their table is missing — a slightly-late migration won't crash the app, but
> the corresponding feature won't work until applied.

### v1.13.0 migrations — Role Management + company-scoped custom roles

Two new migrations, both purely additive (new table / nullable column — no drops, no data loss):

```bat
set DB=-S <sql-host> -d <database> -E -b
sqlcmd %DB% -i "Migrations\Apply_AddRoleModulePermissions.sql"
sqlcmd %DB% -i "Migrations\Apply_AddCompanyRolePartyInfoScope.sql"
```

> **`AddRoleModulePermissions`** — creates `RoleModulePermissions` (empty on a fresh apply). A missing
> row for a role/module means "allowed" (see `ModuleAccessPageFilter`), so this migration alone changes
> no existing behavior — it only creates the table Admin → Role Management writes to.

> **`AddCompanyRolePartyInfoScope`** — adds nullable `CompanyRole.PartyInfoId` (existing system rows
> stay `NULL` = visible to every company). The FK to `PartyInfos` is `NO ACTION`, not cascade — deleting
> a company that has custom roles requires the app to delete those roles first (already handled in
> `Pages/Suppliers/Index.cshtml.cs`'s delete handler), since SQL Server rejects a second cascade path to
> the same table (`PartyInfo` already cascades to `UserCompany`, which also references `CompanyRole`).

Verify both are recorded:
```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
-- last row should be 20260729092236_AddCompanyRolePartyInfoScope
```

### Post-v1.13.0 migration — new-invoice-received email tracking

One new migration, purely additive (3 new nullable/defaulted columns — no drops, no data loss):

```bat
set DB=-S <sql-host> -d <database> -E -b
sqlcmd %DB% -i "Migrations\Apply_AddNewInvoiceReceivedEmailTrackingToInvoiceHeader.sql"
```

> **`AddNewInvoiceReceivedEmailTrackingToInvoiceHeader`** — adds `InvoiceHeader.IsNewInvoiceReceivedEmailSent`
> (`bit NOT NULL DEFAULT 1`), `NewInvoiceReceivedEmailSentAt`, `NewInvoiceReceivedEmailSentTo`. The
> `DEFAULT 1` backfills every existing row as "not applicable" — this migration cannot retroactively
> email anyone about invoices already in the database; only new buyer-side invoices synced in from LHDN
> after this deploy start out eligible (`false`) and get emailed by the existing background finalizer
> loop.

Verify it's recorded:
```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
-- last row should be 20260802130000_AddNewInvoiceReceivedEmailTrackingToInvoiceHeader
```

### Post-v1.14.0 migration — rejection/cancellation email tracking

One new migration, purely additive (6 new nullable/defaulted columns + 1 nullable text column — no
drops, no data loss). **Run this before deploying the new app code** — the app's EF model expects
these columns to exist, so running the new code against an un-migrated database will fail every query
touching `InvoiceHeaders`:

```bat
set DB=-S <sql-host> -d <database> -E -b
sqlcmd %DB% -i "Migrations\Apply_AddRejectionCancellationEmailTrackingToInvoiceHeader.sql"
```

> **`AddRejectionCancellationEmailTrackingToInvoiceHeader`** — adds `InvoiceHeader.IsRejectionEmailSent`/
> `RejectionEmailSentAt`/`RejectionEmailSentTo` and `IsCancellationEmailSent`/`CancellationEmailSentAt`/
> `CancellationEmailSentTo` (all `DEFAULT 1`/`NULL`, same "not applicable until actually rejected/
> cancelled" pattern as the new-invoice-received columns above), plus `CancellationReason`. Backfills
> every existing row as "not applicable" — cannot retroactively email anyone about invoices already in
> the database; only a document rejected/cancelled after this deploy becomes eligible for the
> background retry pass if its immediate send attempt fails.

Verify it's recorded:
```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
-- last row should be 20260803200000_AddRejectionCancellationEmailTrackingToInvoiceHeader
```

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

- **Admin 2FA is optional by default** (`Security:EnforceAdminMfa = false` in `appsettings.json`) —
  Admins can self-enrol voluntarily from Profile & Settings, but are not forced. **Recommended: set
  `Security__EnforceAdminMfa = true` on Production** so an admin without 2FA is redirected to the
  authenticator-setup page until they enrol (no hard lockout either way — recovery codes always work).
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

`LogCleanupService` prunes `SystemLogs` rows older than `LogCleanupSettings:RetentionDays` (default 365)
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
