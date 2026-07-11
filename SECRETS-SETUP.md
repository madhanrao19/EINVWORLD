# EINVWORLD — Secrets Setup

This project keeps **secrets out of source control**. `appsettings.json` ships with the secret values
left **blank**; the real values are supplied at runtime from:

- **Development** → [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (stored in your Windows user profile, never in the repo).
- **Server / IIS** → **environment variables** (set per the IIS app — see `IIS-DEPLOYMENT-GUIDE.md`, PART G/H).

ASP.NET Core configuration precedence means these sources **override** the blank placeholders in `appsettings.json`.

> 🔑 **Naming rule:** a config key like `LHDNApiConfig:ClientSecret` becomes the environment variable
> `LHDNApiConfig__ClientSecret` — replace each `:` with a **double underscore** `__`.

---

## The secrets

| Config key | Env-var name (server) | What it is |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | SQL Server connection string for the main app database (also used by Serilog `SystemLogs`). |
| `ConnectionStrings:WebsiteDb` | `ConnectionStrings__WebsiteDb` | Secondary website DB connection string (if used). |
| `LHDNApiConfig:ClientSecret` | `LHDNApiConfig__ClientSecret` | LHDN MyInvois API client secret (primary). |
| `LHDNApiConfig:ClientSecret2` | `LHDNApiConfig__ClientSecret2` | LHDN MyInvois API client secret (secondary/rotation). |
| `LHDNApiConfig:CertPass` | `LHDNApiConfig__CertPass` | Password for the `.p12` signing certificate. **Only needed when v1.1 signing is enabled** (`SigningEnabled=true`). |
| `Turnstile:SecretKey` | `Turnstile__SecretKey` | Cloudflare Turnstile (CAPTCHA) secret key. |
| `EmailConfiguration:Default:SmtpPassword` | `EmailConfiguration__Default__SmtpPassword` | SMTP password for outbound email. |
| `Api:Key` | `Api__Key` | Static API key for the import REST endpoint (`POST /api/import/validate`, header `X-Api-Key`). **Optional** — leave blank to keep the API disabled. |

### Environment-specific (not a secret, but must be set on the server)

| Config key | Env-var name | Notes |
|---|---|---|
| `DataProtection:KeyRingPath` | `DataProtection__KeyRingPath` | Folder for the encryption key ring. **Required in Production — the app will not start if blank.** Point it OUTSIDE `App\` (preset to `E:\EINVWORLD\Keys` in `appsettings.Production.json`) so a redeploy doesn't wipe the keys (which would log everyone out / break 2FA). Create the folder and grant the app-pool **Modify**. |
| `DatabaseSettings:AutoMigrateOnStartup` | `DatabaseSettings__AutoMigrateOnStartup` | `true` in `appsettings.Production.json`: applies additive migrations on boot (data preserved). **Back up the DB first** and ensure the SQL login has DDL rights. Set `false` to apply `Apply_*.sql` manually. |
| `Security:EnforceAdminMfa` | `Security__EnforceAdminMfa` | Require Admins to enrol 2FA (default `true`). Set `false` only as an emergency escape hatch. Not a secret. |
| `AI:Enabled` etc. | `AI__Enabled` | Optional provider-agnostic AI (local Ollama by default). Not a secret. Config section is `AI` (the old `AIAssistant__…` vars are retired — rename them to `AI__…`). See deployment guide PART O. |
| `AI:ApiKey` | `AI__ApiKey` | **Secret** — only for future cloud providers (OpenAI/Azure/Claude/Gemini); unused by the local Ollama provider. Set via env var / user-secrets, never a settings file. |
| `DocumentCapture:Enabled` | `DocumentCapture__Enabled` | Optional AI Document Capture (needs `AI:Enabled`). Not a secret. |
| `WatchedFolderImport:Enabled` / `:InboxPath` | `WatchedFolderImport__Enabled` / `WatchedFolderImport__InboxPath` | Optional folder validator. Not a secret. |

---

## Development setup (User Secrets)

From the project folder (the one with `EINVWORLD.csproj`):

```bash
# One-time (the project already has a UserSecretsId, so this just confirms it)
dotnet user-secrets init

# Database
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=eInvWorld;Trusted_Connection=True;TrustServerCertificate=True"

# LHDN MyInvois (PREPROD credentials for dev)
dotnet user-secrets set "LHDNApiConfig:ClientSecret"  "your-preprod-client-secret"
dotnet user-secrets set "LHDNApiConfig:ClientSecret2" "your-preprod-client-secret-2"

# Cloudflare Turnstile
dotnet user-secrets set "Turnstile:SecretKey" "your-turnstile-secret"
# QA only (Playwright): to bypass the challenge, temporarily use Cloudflare's always-pass TEST keys —
#   Turnstile:SiteKey=1x00000000000000000000AA   Turnstile:SecretKey=1x0000000000000000000000000000000AA
# NEVER use test keys in production (they disable bot protection). See docs/TABLER-MIGRATION-AUDIT.md.

# SMTP — set BOTH username and password. A blank SmtpUsername fails with a clear
#        "SMTP not configured" error (v1.9.9) and no email is sent.
dotnet user-secrets set "EmailConfiguration:Default:SmtpUsername" "your-smtp-username"
dotnet user-secrets set "EmailConfiguration:Default:SmtpPassword" "your-smtp-password"

# Only if you enable v1.1 signing in dev:
dotnet user-secrets set "LHDNApiConfig:CertPass" "your-p12-password"

# Only if you enable the import REST API in dev:
dotnet user-secrets set "Api:Key" "a-long-random-key"
```

List what you've set:

```bash
dotnet user-secrets list
```

> Always state **`Encrypt=True`** explicitly in production connection strings so encryption-in-transit is
> a visible, auditable setting rather than an implicit driver default. `TrustServerCertificate=True` is
> fine for a local/in-house SQL Server with a self-signed cert. Avoid it against a server where you can
> install a proper TLS certificate.

---

## Server setup (IIS environment variables)

Set these as **environment variables on the IIS application** (or the app pool / machine), using the
`__` names from the tables above. Full click-by-click steps are in **`IIS-DEPLOYMENT-GUIDE.md`** (PART G–H).

Example (PowerShell, machine-level — adjust scope to your policy):

```powershell
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=...;Database=eInvWorld;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True", "Machine")
[Environment]::SetEnvironmentVariable("LHDNApiConfig__ClientSecret",  "prod-secret",   "Machine")
[Environment]::SetEnvironmentVariable("LHDNApiConfig__ClientSecret2", "prod-secret-2", "Machine")
[Environment]::SetEnvironmentVariable("Turnstile__SecretKey", "turnstile-secret", "Machine")
# SMTP needs BOTH username and password — a blank username sends no mail (v1.9.9 fails it with a clear message).
[Environment]::SetEnvironmentVariable("EmailConfiguration__Default__SmtpUsername", "smtp-username", "Machine")
[Environment]::SetEnvironmentVariable("EmailConfiguration__Default__SmtpPassword", "smtp-password", "Machine")
# Optional — only if you enable the import REST API:
[Environment]::SetEnvironmentVariable("Api__Key", "a-long-random-key", "Machine")
```

> `DataProtection__KeyRingPath` is preset in `appsettings.Production.json` (`E:\EINVWORLD\Keys`); set the
> env var only if your server uses a different path.

Then **restart IIS** (`iisreset`) so the new values are picked up.

---

## Rotation & hygiene

- **Never commit real secrets.** `appsettings.json` must keep blank placeholders. Do not paste secrets into
  `appsettings.Production.json` either.
- **Rotate** the LHDN client secret, SMTP and Turnstile keys periodically and immediately if they were ever
  exposed (e.g. pasted into a screenshot, email, or chat). LHDN supports two client secrets
  (`ClientSecret` / `ClientSecret2`) precisely to allow zero-downtime rotation.
- After rotating, update the env var (or user-secret) and `iisreset`.
- Keep the `.p12` signing certificate and its password out of the repo; store the cert on the server only
  (see deployment guide PART I) and supply `CertPass` via env var.

## Signing-key custody (`LHDNApiConfig:SigningKeyProvider`)

The signing service gets its certificate from a pluggable **`ICertificateProvider`**, selected by
`LHDNApiConfig:SigningKeyProvider`:

- **`File`** (default) — loads the `.p12` from `CertPath`/`CertPass` as documented above. Fine for a
  single on-prem server, but the private key lives as a file on disk.
- **Vault/HSM upgrade path** (not yet implemented — a clean drop-in when you're ready): implement
  `ICertificateProvider` (e.g. an `AzureKeyVaultCertificateProvider` named `"AzureKeyVault"`) using the
  `Azure.Security.KeyVault.Certificates` NuGet package with `DefaultAzureCredential` (managed identity —
  no secret in config), add config keys such as `LHDNApiConfig:KeyVaultUri` and
  `LHDNApiConfig:KeyVaultCertName`, register it in `Program.cs`, and set
  `SigningKeyProvider = "AzureKeyVault"`. The signing service and its callers need **no changes** —
  selection is by provider name, mirroring the AI provider pattern.

## Field-level PII encryption & the DataProtection key-ring (IMPORTANT)

As of v1.7.2 a small set of sensitive free-text columns — **bank account numbers** (`BankAccountNo`) and
the **secondary/tertiary address lines** (`Addr2`, `Addr3`) — are **encrypted at rest** using the same
ASP.NET Core DataProtection key-ring configured by `DataProtection:KeyRingPath`. Encryption/decryption is
transparent: new and edited records are encrypted automatically.

This raises the stakes on the key-ring:

- **The key-ring is now load-bearing for data, not just sessions.** Previously, losing the key-ring only
  logged users out and broke 2FA/antiforgery. Now it **also makes the encrypted PII columns permanently
  unreadable.** Treat the key-ring folder (`DataProtection:KeyRingPath`, e.g. `E:\EINVWORLD\Keys`) as a
  **critical backup target** — back it up whenever you back up the database, and store the backup with the
  same protection as the DB backup.
- **Never delete or hand-edit** the `*.xml` key files. Key rotation is automatic (DataProtection rolls
  keys on its own schedule); old keys are retained so existing ciphertext keeps decrypting.
- The DataProtection **purpose** for this data is `eInvWorld.Pii.FieldEncryption.v1` (in
  `ApplicationDbContext`). It is versioned and **must not change** without a planned re-encryption, or
  existing ciphertext becomes unreadable.

### One-time backfill of existing rows

Rows created before v1.7.2 hold plaintext until encrypted. To encrypt them in place:

1. **Take a full database backup** and confirm the key-ring folder is backed up.
2. Go to **Admin → System Health → "Encrypt existing PII"** and confirm.
3. The operation is **idempotent** — already-encrypted rows are skipped, so it is safe to re-run (e.g. if
   interrupted). The outcome is recorded in **Admin → Audit Trail** (`PiiEncryptionBackfill`).

TIN is **not** encrypted (it is filtered on throughout the app and is a semi-public tax identifier), and
neither are `Addr1`/city/state/postal (used in reporting and PDFs). See CHANGELOG v1.7.2 for the rationale.

### Webhook signing secrets (v1.8.0)

Outbound webhook subscriptions (Admin → Webhooks) each carry an HMAC signing secret. These are **generated
by the app** (not entered by you), shown exactly once at create/rotate time, and stored **encrypted** with
the DataProtection key-ring under a dedicated purpose (`eInvWorld.Secret.FieldEncryption.v1`). There is
nothing to put in `appsettings`/env vars for them. Two consequences:

- The key-ring backup now also protects these secrets — losing it means re-issuing (rotating) every
  webhook secret and reconfiguring each receiver.
- Rotating a secret (Admin → Webhooks → Rotate) invalidates the old one immediately; update the receiver in
  the same change window.
