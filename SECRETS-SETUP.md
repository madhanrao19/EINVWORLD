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

### Environment-specific (not a secret, but must be set on the server)

| Config key | Env-var name | Notes |
|---|---|---|
| `DataProtection:KeyRingPath` | `DataProtection__KeyRingPath` | Folder for the encryption key ring. **Point it OUTSIDE `App\`** (e.g. `D:\EINVWORLD\Keys`) so a redeploy doesn't wipe the keys (which would log everyone out). See `IIS-DEPLOYMENT-GUIDE.md`. |
| `AIAssistant:Enabled` etc. | `AIAssistant__Enabled` | Optional AI assistant (local Ollama). Not a secret. See deployment guide PART O. |

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
```

List what you've set:

```bash
dotnet user-secrets list
```

> `TrustServerCertificate=True` is fine for a local/in-house SQL Server with a self-signed cert. Avoid it
> against a server where you can install a proper TLS certificate.

---

## Server setup (IIS environment variables)

Set these as **environment variables on the IIS application** (or the app pool / machine), using the
`__` names from the tables above. Full click-by-click steps are in **`IIS-DEPLOYMENT-GUIDE.md`** (PART G–H).

Example (PowerShell, machine-level — adjust scope to your policy):

```powershell
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=...;Database=eInvWorld;User Id=...;Password=...;TrustServerCertificate=True", "Machine")
[Environment]::SetEnvironmentVariable("LHDNApiConfig__ClientSecret",  "prod-secret",   "Machine")
[Environment]::SetEnvironmentVariable("LHDNApiConfig__ClientSecret2", "prod-secret-2", "Machine")
[Environment]::SetEnvironmentVariable("Turnstile__SecretKey", "turnstile-secret", "Machine")
[Environment]::SetEnvironmentVariable("EmailConfiguration__Default__SmtpPassword", "smtp-password", "Machine")
[Environment]::SetEnvironmentVariable("DataProtection__KeyRingPath", "D:\EINVWORLD\Keys", "Machine")
```

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
