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

# SMTP
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
