# EINVWORLD — IIS Deployment Guide

**A complete, click-by-click guide. Written so a junior IT / intern can deploy EINVWORLD to a
Windows Server (IIS + SQL Server) with no prior knowledge of the app.**

Follow the parts **in order**. Each step says exactly what to click and **what you should see** when it
works. If a step's result is different, stop and check the Troubleshooting section (Part 16) before
continuing.

> ⏱️ **Time needed:** ~1–2 hours the first time.
> 🧰 **You will deploy to one server** — the steps are identical for **Production** and **Staging**; the
> only differences (database name, LHDN URL, domain) are called out in **Part 2**.

---

## Contents

1. [What you need before you start](#part-1--what-you-need-before-you-start)
2. [Production vs Staging — what changes](#part-2--production-vs-staging)
3. [Create the folders](#part-3--create-the-folders)
4. [Install .NET 10 Hosting Bundle](#part-4--install-net-10-hosting-bundle)
5. [Prepare the SQL Server databases](#part-5--prepare-the-sql-server-databases)
6. [Copy the application files](#part-6--copy-the-application-files)
7. [Create the IIS Application Pool](#part-7--create-the-iis-application-pool)
8. [Create the IIS Website](#part-8--create-the-iis-website)
9. [Set folder permissions](#part-9--set-folder-permissions)
10. [Set environment variables (secrets)](#part-10--set-environment-variables-secrets)
11. [Install the signing certificate (optional)](#part-11--install-the-signing-certificate-optional)
12. [Back up the database (before first run)](#part-12--back-up-the-database-before-first-run)
13. [Start the site & first run](#part-13--start-the-site--first-run)
14. [First login + enrol Admin 2FA](#part-14--first-login--enrol-admin-2fa)
15. [Smoke test (verify everything works)](#part-15--smoke-test)
16. [Troubleshooting](#part-16--troubleshooting)
17. [Optional features](#part-17--optional-features)
18. [Updating to a new version later](#part-18--updating-to-a-new-version)
19. [Final checklist](#part-19--final-checklist)

---

## Part 1 — What you need before you start

Collect all of these **before** you begin. Ask the project lead if anything is missing.

| Item | Example / where it comes from |
|---|---|
| ☐ Windows Server with **Administrator** access | Remote Desktop login |
| ☐ **IIS** installed (Web Server role) | Server Manager → Add Roles |
| ☐ **SQL Server** installed + **SSMS** (SQL Server Management Studio) | already on the DB server |
| ☐ The application package (zip) | e.g. `EINVWORLD_release_v1.3.zip` |
| ☐ **SQL database backup** (`.bak`) if migrating an existing DB | from the previous server |
| ☐ **SSL certificate** for the domain | `.pfx` installed in Windows, or CA cert |
| ☐ **Domain name** pointing to this server | e.g. `einvworld.com` (prod) / `staging.einvworld.com` |
| ☐ **SQL login** username + password | e.g. `einvworldusr` / a strong password |
| ☐ **LHDN MyInvois** credentials | `ClientId`, `ClientSecret`, `ClientSecret2` |
| ☐ **SMTP** password (for outgoing email) | from the mail admin |
| ☐ **Cloudflare Turnstile** secret key | from the project lead |
| ☐ (optional) **Signing certificate** `.p12` + password | only if you will enable LHDN digital signing |

> 🔐 **Golden rule:** **never type passwords into `appsettings.json`.** All secrets go into **IIS
> environment variables** (Part 10). This guide assumes that.

---

## Part 2 — Production vs Staging

Do the **same steps** for both. Only these values differ — write down which set you are using:

| Setting | Production | Staging |
|---|---|---|
| Domain | `einvworld.com` | `staging.einvworld.com` (or a port like `:8443`) |
| Database name | `EINVWORLD` | `EINVWORLD_STAGING` (a separate DB!) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Production` (still Production — just different DB/URL) |
| LHDN `BaseUrl` | `https://api.myinvois.hasil.gov.my/` | `https://preprod-api.myinvois.hasil.gov.my/` (sandbox) |
| LHDN `ValidationBaseUrl` | `https://myinvois.hasil.gov.my/` | `https://preprod.myinvois.hasil.gov.my/` |

> Staging points at the **LHDN PREPROD sandbox** so test invoices don't go to the real tax authority.
> If you set the preprod URL while `ASPNETCORE_ENVIRONMENT=Production`, the app logs a harmless
> **warning** at startup (reminding you it's the sandbox) — that's expected on staging.
>
> The LHDN `BaseUrl`/`ValidationBaseUrl` live in `appsettings.json` (not a secret). For staging, edit
> those two values in the staging server's `appsettings.json` to the preprod URLs.

---

## Part 3 — Create the folders

Open **File Explorer** and create this exact structure (here on the `E:` drive — use another drive if
`E:` doesn't exist, but keep the same sub-folders):

```
E:\EINVWORLD
 ├── App         ← the application files go here
 ├── Documents   ← generated invoices, PDFs, drafts (the app writes here)
 ├── Logs        ← log files
 ├── Keys        ← encryption keys (MUST be separate from App)
 └── Cert        ← the LHDN signing certificate (only if you use signing)
```

**Why `Keys` is separate:** Windows stores the keys that protect login cookies, 2-factor, and form
security here. If they lived inside `App\`, every time you re-deploy a new version they'd be wiped and
**everyone would be logged out**. Keeping them in `E:\EINVWORLD\Keys` makes them survive upgrades.

> ✅ **You should see:** five folders under `E:\EINVWORLD`.

---

## Part 4 — Install .NET 10 Hosting Bundle

The app runs on .NET 10. IIS needs the **Hosting Bundle** (runtime + the IIS module).

1. On the server, open a browser → search **".NET 10 Hosting Bundle download"** → go to the official
   **dotnet.microsoft.com** page.
2. Download **ASP.NET Core Runtime → Hosting Bundle** (Windows).
3. Right-click the installer → **Run as administrator** → **Install** → **Finish**.
4. **Restart IIS** so it picks up the new module: open **Command Prompt as Administrator** and run:
   ```
   iisreset
   ```

**Verify it installed:** in the same Command Prompt run:
```
dotnet --list-runtimes
```
✅ **You should see** lines containing:
```
Microsoft.AspNetCore.App 10.x.x ...
Microsoft.NETCore.App 10.x.x ...
```
If you don't see `10.x`, the Hosting Bundle didn't install — re-run the installer.

---

## Part 5 — Prepare the SQL Server databases

The app uses **two** databases on the same SQL Server:
- **`EINVWORLD`** — the main application database.
- **`EINVWORLDWEBSITE`** — the public website/marketing data.

### Step 5.1 — Create (or restore) the databases

Open **SSMS** and connect to the SQL Server.

- **Brand-new install:** right-click **Databases → New Database…** → create `EINVWORLD`, then again for
  `EINVWORLDWEBSITE`. (The app creates the tables itself on first run — Part 13.)
- **Migrating an existing DB:** right-click **Databases → Restore Database…** → **Device** → pick the
  `.bak` → restore it as `EINVWORLD`.

### Step 5.2 — Create the SQL login the app will use

1. In SSMS: **Security → Logins → right-click → New Login…**
2. **Login name:** `einvworldusr`
3. Select **SQL Server authentication**, set a **strong password**, untick "Enforce password policy" if
   it blocks you, click the **User Mapping** page.
4. Tick **both** `EINVWORLD` and `EINVWORLDWEBSITE`. For each, in the **role membership** list below tick:
   - **`db_datareader`**, **`db_datawriter`**, and **`db_ddladmin`**.
5. Click **OK**.

> 🔎 **Why `db_ddladmin`?** On first run (and on each upgrade) the app **creates/updates its own
> tables automatically**. That needs schema permission. If your security policy forbids this, see the
> "manual migrations" note in **Part 13**.

✅ **You should see:** `einvworldusr` under **Security → Logins**, mapped to both databases.

---

## Part 6 — Copy the application files

1. Copy the release zip (e.g. `EINVWORLD_release_v1.3.zip`) onto the server.
2. Right-click → **Extract All…**
3. Copy **everything** from inside the extracted folder into:
   ```
   E:\EINVWORLD\App
   ```

✅ **You should see**, directly inside `E:\EINVWORLD\App`:
```
EINVWORLD.exe
EINVWORLD.dll
web.config
appsettings.json
appsettings.Production.json
wwwroot\           (folder)
…and many .dll files
```

> ⚠️ **`web.config` must be present** at the root of `App\`. Without it IIS can accidentally expose
> internal files. If it's missing, the package is incomplete — get a correct build.

---

## Part 7 — Create the IIS Application Pool

The "Application Pool" is the Windows process that runs the app.

1. Open **Start → IIS Manager** (Internet Information Services Manager).
2. In the left tree, click **Application Pools**.
3. Right-click → **Add Application Pool…**
   - **Name:** `EINVWORLD`
   - **.NET CLR version:** **No Managed Code**  *(important — the app brings its own .NET)*
   - **Managed pipeline mode:** **Integrated**
   - Click **OK**.
4. Select the new **EINVWORLD** pool → on the right click **Advanced Settings…** and set:

   | Setting | Value | Why |
   |---|---|---|
   | **Start Mode** | `AlwaysRunning` | keeps background workers (sync, tokens, recurring) alive |
   | **Idle Time-out (minutes)** | `0` | don't shut down when idle |
   | **Maximum Worker Processes** | `1` | a single process (don't use a "web garden") |
   | **Load User Profile** | `True` | needed for some libraries / temp files |
   | **Regular Time Interval (recycle, minutes)** | `0` | or schedule a recycle at night, not during work hours |

   Click **OK**.

✅ **You should see:** an Application Pool named **EINVWORLD**, Started, "No Managed Code".

---

## Part 8 — Create the IIS Website

1. In **IIS Manager**, right-click **Sites → Add Website…**
2. Fill in:
   - **Site name:** `EINVWORLD`
   - **Application pool:** click **Select…** → choose **EINVWORLD**
   - **Physical path:** `E:\EINVWORLD\App`
   - **Binding → Type:** `https`
   - **Port:** `443` (Production) — for staging you may use `443` on the staging host name, or a port
     like `8443`
   - **Host name:** your domain, e.g. `einvworld.com` (or `staging.einvworld.com`)
   - **SSL certificate:** pick your installed certificate from the dropdown
3. Click **OK**.

> If your certificate isn't in the dropdown, install it first: **IIS Manager → Server name → Server
> Certificates → Import…** (`.pfx`), then come back.

✅ **You should see:** a Site named **EINVWORLD**, Started, bound to `https` on your domain.

> **Using a Cloudflare Tunnel instead of a public IP + SSL certificate?** Skip the `https`/443 binding
> above — bind the site to **`http`** on **port 80** (host name your domain or blank) and let Cloudflare
> terminate TLS. See **Part 8b**.

---

## Part 8b — Alternative: expose the site with a Cloudflare Tunnel (no public IP, no cert on IIS)

Use this **instead of** a public HTTPS binding when the server has **no fixed public IP** and you want
**Cloudflare to provide the SSL certificate**. Cloudflare terminates HTTPS at its edge and `cloudflared`
forwards plain **HTTP to your server on localhost**, so the IIS site itself only needs an HTTP binding.

1. **Bind the IIS site to HTTP**, not HTTPS:
   - In Part 8, set **Binding → Type:** `http`, **Port:** `80`, **Host name:** your domain (or leave blank).
   - No SSL certificate is needed on IIS — Cloudflare supplies it.
2. **Install cloudflared** on the server (from `https://github.com/cloudflare/cloudflared/releases`, or
   `winget install --id Cloudflare.cloudflared`).
3. **Create the tunnel** (one-time), logging in to your Cloudflare account:
   ```
   cloudflared tunnel login
   cloudflared tunnel create einvworld
   ```
4. **Point the tunnel at the local site.** In the tunnel config (`%USERPROFILE%\.cloudflared\config.yml`):
   ```yaml
   tunnel: einvworld
   credentials-file: C:\Users\<you>\.cloudflared\<tunnel-id>.json
   ingress:
     - hostname: einvworld.com
       service: http://localhost:80
     - service: http_status:404
   ```
5. **Route DNS and run it as a service:**
   ```
   cloudflared tunnel route dns einvworld einvworld.com
   cloudflared service install
   ```
6. In the **Cloudflare dashboard** turn on **SSL/TLS → Edge Certificates → Always Use HTTPS** so visitors
   are forced to HTTPS at the edge.
7. **Tell the app it's behind a tunnel** (Part 10 environment variables, then `iisreset`):
   | Name | Value | Why |
   |---|---|---|
   | `ForwardedHeaders__Enabled` | `true` (this is the default) | App trusts `X-Forwarded-Proto`/`X-Forwarded-For` from cloudflared so it sees the **real client IP** (correct rate limiting + audit logs) instead of `127.0.0.1`. **Also flips the HTTPS-redirect default OFF** (next row), preventing a redirect loop. |
   | `Security__HttpsRedirectPort` | `0` (already the default when `ForwardedHeaders__Enabled=true`) | Disables the app's own HTTP→HTTPS redirect; Cloudflare's **Always Use HTTPS** does it at the edge. You normally don't need to set this — it's only here to force-off if someone set a port. |

✅ **You should see:** browsing `https://einvworld.com` loads the site over Cloudflare's certificate; the
app's **Admin → System Logs** show real visitor IPs (not `127.0.0.1`), and there is no
*"Failed to determine the https port"* warning.

---

## Part 9 — Set folder permissions

The app pool must be allowed to write to its working folders.

1. In **File Explorer**, right-click **`E:\EINVWORLD`** → **Properties → Security → Edit… → Add…**
2. In the box type exactly:
   ```
   IIS AppPool\EINVWORLD
   ```
   (this is the pool's identity — `EINVWORLD` must match the pool name from Part 7)
3. Click **Check Names** → it should resolve/underline → **OK**.
4. With that identity selected, tick **Allow** for **Modify**, **Read & execute**, **List**, **Read**,
   **Write**.
5. Click **Apply → OK**. (This applies to all sub-folders including `Documents`, `Logs`, `Keys`, `Cert`.)

✅ **You should see:** `IIS AppPool\EINVWORLD` listed with **Modify** permission on `E:\EINVWORLD`.

---

## Part 10 — Set environment variables (secrets)

This is where the passwords go — **not** in `appsettings.json`.

1. In **IIS Manager**, click the **EINVWORLD** *site* (left tree).
2. Double-click **Configuration Editor** (under "Management").
3. In the **Section** dropdown at the top, choose:
   ```
   system.webServer/aspNetCore
   ```
4. Find the **`environmentVariables`** row → click it → click the **`…`** button on the right.
5. A grid opens. Click **Add** for each row below and fill **Name** and **Value**:

   | Name | Value (example) |
   |---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__DefaultConnection` | `Server=localhost,1433;Database=EINVWORLD;User Id=einvworldusr;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true` |
   | `ConnectionStrings__WebsiteDb` | `Server=localhost,1433;Database=EINVWORLDWEBSITE;User Id=einvworldusr;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true` |
   | `LHDNApiConfig__ClientSecret` | `YOUR_LHDN_CLIENT_SECRET` |
   | `LHDNApiConfig__ClientSecret2` | `YOUR_LHDN_CLIENT_SECRET2` |
   | `EmailConfiguration__Default__SmtpPassword` | `YOUR_SMTP_PASSWORD` |
   | `Turnstile__SecretKey` | `YOUR_TURNSTILE_SECRET` |

   **Add these only if you use the feature:**

   | Name | When |
   |---|---|
   | `LHDNApiConfig__CertPass` | only if you enable LHDN **digital signing** (Part 11) |
   | `Api__Key` | only if an external ERP will call the validation API (`POST /api/import/validate`) |

   > ℹ️ The double underscore `__` replaces the `:` in a config key. So `ConnectionStrings:DefaultConnection`
   > becomes `ConnectionStrings__DefaultConnection`.

6. Click **OK** (close the grid) → on the right click **Apply**.

> 📝 **`DataProtection:KeyRingPath` is already set** to `E:\EINVWORLD\Keys` inside
> `appsettings.Production.json`. You just made that folder in Part 3 and gave it Modify rights in Part 9,
> so there's nothing more to do here. **(The app will refuse to start in Production if this folder/path
> is missing — that's a safety feature.)**

✅ **You should see:** the environment variables listed (with `ASPNETCORE_ENVIRONMENT = Production`).

---

## Part 11 — Install the signing certificate (optional)

**Skip this** unless the project lead told you to enable **LHDN v1.1 digital signing**.

1. Copy the certificate file (e.g. `DATAMATION_TECHNOLOGY_(M)_SDN._BHD..p12`) into:
   ```
   E:\EINVWORLD\Cert
   ```
2. In the app's environment variables (Part 10) also add:
   | Name | Value |
   |---|---|
   | `LHDNApiConfig__SigningEnabled` | `true` |
   | `LHDNApiConfig__DocVersion` | `1.1` |
   | `LHDNApiConfig__CertPath` | `Cert\DATAMATION_TECHNOLOGY_(M)_SDN._BHD..p12` |
   | `LHDNApiConfig__CertPass` | the certificate password |
3. **Validate against the LHDN PREPROD sandbox first** before doing this in Production.

> If signing is **on** but the cert path or password is wrong, the app **won't start** (a safety check)
> and the log will tell you exactly what's missing.

---

## Part 12 — Back up the database (before first run)

⚠️ **Do this before you start the site the first time.** On first run the app **creates/updates its
tables automatically**. A backup is your safety net.

1. In **SSMS**: right-click the **`EINVWORLD`** database → **Tasks → Back Up…**
2. **Backup type:** `Full` → choose a destination folder → **OK**.

✅ **You should see:** "The backup of database 'EINVWORLD' completed successfully."

> The app's schema changes are **additive** (it adds tables/columns/indexes — it never deletes your
> data), but always keep this backup until you've confirmed the new version works.

---

## Part 13 — Start the site & first run

1. In **IIS Manager**, click the **EINVWORLD** site → on the right, under **Manage Website**, click
   **Restart** (or run `iisreset` in an Admin Command Prompt).
2. Wait ~30–60 seconds — on the **first** start the app creates/updates the database tables (this makes
   the first start slower than usual). That's normal.
3. Open a browser and go to your domain:
   ```
   https://einvworld.com
   ```
   (or your staging URL)

✅ **You should see:** the **EINVWORLD login page**.

**Quick health check** — open these URLs:
- `https://einvworld.com/health/live` → should say **Healthy** (the app is running).
- `https://einvworld.com/health/ready` → should say **Healthy** (database + folders are OK).

❌ If you get **HTTP 500** or a blank page, go to **Part 16 — Troubleshooting** (check the
`E:\EINVWORLD\App\logs\stdout` files — the error usually says exactly what's wrong, e.g. a missing
environment variable).

> **Prefer to apply database changes by hand?** (strict DBs that don't allow `db_ddladmin`.) Set the env
> var `DatabaseSettings__AutoMigrateOnStartup` to `false`, and ask the developer for the
> `Migrations\Apply_*.sql` scripts; run them in SSMS in the documented order **before** starting the
> site. See `DEPLOY-NOTES.md`.

---

## Part 14 — First login + enrol Admin 2FA

The app **requires two-factor authentication for administrator accounts**.

1. On the login page, sign in with the **admin** account given to you.
2. **First time:** you'll be redirected to a **"Configure authenticator app"** page (this is expected,
   not an error).
3. On your phone install **Google Authenticator** (or Microsoft Authenticator).
4. **Scan the QR code** shown on screen, then type the **6-digit code** from the app and submit.
5. **Save the recovery codes** it shows you somewhere safe — they're your backup if you lose the phone.
6. You'll land on the **Dashboard**. From now on, admin logins ask for the 6-digit code.

✅ **You should see:** the Dashboard after entering the code.

> Want 2FA off? (not recommended) Add the env var `Security__EnforceAdminMfa` = `false` and restart IIS.
> There is **no lockout** either way — you always reach the enrolment page and have recovery codes.

---

## Part 15 — Smoke test

Confirm the core things work:

1. **Health:** open **Admin → System Health** (left menu). Everything should look OK — database,
   background jobs, DataProtection keys, disk space, signing cert (if enabled).
2. **Login/roles:** the menu shows the admin options.
3. **Create a test invoice** and **submit it to LHDN**.
   - Production: it goes to the real MyInvois — use a genuine test invoice your team agreed on.
   - Staging: it goes to the **PREPROD sandbox**.
   ✅ Expected: status moves to **Submitted → Valid** and a **QR code** appears.
4. **Email:** trigger an action that sends mail (e.g. validated-invoice notification) and confirm it
   arrives. (If not, check the SMTP password env var.)
5. **PDF:** open an invoice and download its PDF.

✅ If all five pass, the deployment is good.

---

## Part 16 — Troubleshooting

**First place to look:** `E:\EINVWORLD\App\logs\` — open the newest `stdout_*.log`. The error message
there almost always names the exact problem. (Also **Admin → System Logs** once the app is up.)

| Symptom | Likely cause → fix |
|---|---|
| **Website won't open** | Is the IIS **site started**? Is the **app pool started**? Is the **binding/port** correct? |
| **HTTP 500.30 / app won't start** | A required **environment variable** is missing/wrong (most often a connection string), **or** `E:\EINVWORLD\Keys` is missing/not writable, **or** signing is on with a bad cert. The `stdout` log says which. |
| **Database / login errors** | Check `ConnectionStrings__DefaultConnection`. Confirm `einvworldusr` password and that it's mapped to the DB with `db_datareader/writer/ddladmin`. |
| **First start is very slow then OK** | Normal — it's creating/updating tables. Only the first start. |
| **Logged out after every deploy** | `E:\EINVWORLD\Keys` wasn't used/persisted — confirm Part 3 + Part 9 (the folder exists and the app pool has Modify). |
| **LHDN submit fails** | Check `LHDNApiConfig__ClientSecret` / `ClientSecret2`. On staging confirm the **preprod** URLs. |
| **Email not sending** | Check `EmailConfiguration__Default__SmtpPassword`. |
| **Too many "429 Too Many Requests"** | If many users share one office IP, raise `RateLimiting__PermitsPerMinute` (e.g. `3000`) or set `RateLimiting__Enabled` = `false`. |
| **Page styles/images missing** | Confirm `wwwroot\` was copied into `App\`. |

After changing any environment variable, **always run `iisreset`** so it takes effect.

---

## Part 17 — Optional features

All are **OFF by default**. Enable only if asked.

### 17a — AI E-Invoice Assistant & AI Document Capture (local, private)

The AI layer is **provider-agnostic** but ships with a **local, free** provider (Ollama) that runs on the
server — **no invoice data leaves the server.** AI is **optional**: if it is off or unreachable, invoicing
still works normally. Models are **not bundled** with the app — you pull them separately with Ollama.

1. Download & install **Ollama for Windows** from `https://ollama.com/download`. It runs as a service on
   `http://localhost:11434`.
2. Open Command Prompt and pull a model:
   ```
   ollama pull gemma3:12b
   ```
   Test it: `ollama run gemma3:12b "Say hello"`.

   > **Pick a model your server's RAM can hold.** Recommended models are `gemma3:12b` (~8 GB, the default),
   > `gemma3:27b` and `qwen3:32b` — larger models need proportionally more RAM/VRAM. On a modest server a
   > smaller model (e.g. `llama3.2:3b` ~2 GB) still works. Oversized models can fail with
   > *"failed to allocate buffer …"* in the Ollama log, which makes the request time out
   > (`TaskCanceledException` in the app log). Only move up if you have the RAM/VRAM to spare.
3. Add these environment variables (Part 10) and `iisreset`:
   | Name | Value |
   |---|---|
   | `AI__Enabled` | `true` |
   | `AI__Model` | `gemma3:12b` (must match what you pulled) |
   | `DocumentCapture__Enabled` | `true` (enables PDF → suggestion) |

   > The config section is **`AI`** (env prefix `AI__`). The old **`AIAssistant__…`** variables are
   > **retired** — if you set them on a previous version, rename them to `AI__…` (e.g. `AIAssistant__Enabled`
   > → `AI__Enabled`, `AIAssistant__Model` → `AI__Model`), otherwise AI will stay off after upgrading.
   > For machine/user-scope env vars, the helper script does this for you (run elevated):
   > `powershell -ExecutionPolicy Bypass -File scripts\Rename-AiEnvVars.ps1 -AppPool '<YourAppPool>'`
   > (add `-WhatIf` first to preview). If the variables were set in the IIS app-pool dialog or a
   > server-side `web.config` instead, rename them there by hand.
   > A cloud provider's key (future OpenAI/Azure/Claude/Gemini) goes in `AI__ApiKey` as an env var —
   > never in a settings file.
4. Verify from the app: sign in as Admin → **AI Settings** → **Test connection**. It reports whether the
   provider is reachable and the model is pulled, plus round-trip latency — without exposing any API key.

✅ The **E-Invoice Assistant** and **AI Document Capture** menus now work. They only *suggest* drafts —
they never submit. Always review every field before saving. (AI Document Capture replaces the old
"Extract Invoice (Beta)" page, which has been removed.)

#### 17a-OCR — Scanned-PDF OCR for AI Document Capture (optional)

By default AI Document Capture reads **digital (text-based) PDFs** only. To also read **scanned/image**
PDFs, enable the built-in OCR (Tesseract). The native libraries (Tesseract + PDFium) ship with the app
and are only loaded when OCR is on, so leaving it off costs nothing.

1. **Stage the language data.** Create a `tessdata` folder, e.g. `D:\EINVWORLD\tessdata`, and copy the
   Tesseract trained-data files into it: `eng.traineddata` (and `msa.traineddata` for Malay). Get them
   from `https://github.com/tesseract-ocr/tessdata_fast` (FOSS, Apache-2.0). Grant the app-pool **Read**.
2. **Visual C++ runtime.** Ensure the **Microsoft Visual C++ 2015–2022 Redistributable (x64)** is
   installed on the server (the native OCR/PDF libraries need it). Most servers already have it.
3. Add env vars (Part 10) and `iisreset`:
   | Name | Value |
   |---|---|
   | `DocumentCapture__OcrEnabled` | `true` |
   | `DocumentCapture__TessdataPath` | `D:\EINVWORLD\tessdata` |
   | `DocumentCapture__OcrLanguage` | `eng` (or `eng+msa`) |

✅ **Verify:** upload a scanned invoice PDF to **AI Document Capture** — it should extract text and produce
a suggestion. If you see "couldn't read this document", check the `tessdata` path/permissions and the VC++
runtime; the app log records the OCR error. (OCR can't be exercised by CI — it must be verified here.)

### 17b — Watched-folder import (drop files to validate)

1. Create a folder, e.g. `E:\EINVWORLD\Inbox`, and give the app pool **Modify** rights on it (Part 9).
2. Add env vars and `iisreset`:
   | Name | Value |
   |---|---|
   | `WatchedFolderImport__Enabled` | `true` |
   | `WatchedFolderImport__InboxPath` | `E:\EINVWORLD\Inbox` |

   > **Set `InboxPath` whenever you set `Enabled=true`.** If the feature is enabled but the path is blank,
   > the worker stays idle and logs *"WatchedFolderImport enabled but InboxPath is empty — worker idle."*
   > once at startup. If you don't want the feature, leave `WatchedFolderImport__Enabled=false` instead.
3. Drop a `.csv`/`.xlsx` invoice file into the Inbox. The app validates it and moves it to
   `Processed\` or `Rejected\` with a `.report.json` result. (It validates only — it doesn't create invoices.)

### 17c — Import REST API (for an external ERP)

1. Add env var `Api__Key` = a long random string, then `iisreset`.
2. The ERP calls `POST https://einvworld.com/api/import/validate` with header `X-Api-Key: <that key>` and
   a JSON array of invoice rows; it returns a per-row validation report.

---

## Part 18 — Updating to a new version

1. **Back up the database** (Part 12).
2. In **IIS Manager**, **Stop** the EINVWORLD site (so files aren't locked).
3. Keep `Documents\`, `Logs\`, `Keys\`, `Cert\` — **never delete these.** Replace only the contents of
   `App\` with the new build (tip: deploy to `App_New`, then swap, keeping the old folder as `App_Old`
   for quick rollback).
4. Re-check `web.config` and `appsettings.Production.json` are present in the new `App\`.
5. **Start** the site. The first start applies any new DB changes automatically (additive — your data is
   safe).
6. Check `/health/ready` and **Admin → System Health**, then do a quick smoke test (Part 15).

**Rollback:** if something's wrong, Stop the site, swap `App_Old` back to `App`, Start. If the DB was
changed, restore the backup from step 1.

---

## Part 19 — Final checklist

Tick each before declaring "done":

- [ ] .NET 10 **Hosting Bundle** installed (`dotnet --list-runtimes` shows 10.x)
- [ ] Folders created: `App`, `Documents`, `Logs`, **`Keys`**, `Cert`
- [ ] Databases `EINVWORLD` (+ `EINVWORLDWEBSITE`) exist; login `einvworldusr` mapped with
      `db_datareader/writer/ddladmin`
- [ ] App files copied to `App\` (incl. **`web.config`**, `wwwroot\`)
- [ ] App Pool `EINVWORLD`: **No Managed Code**, **AlwaysRunning**, Idle **0**, **1** worker process
- [ ] Website bound to **https** + correct host + SSL certificate
- [ ] `IIS AppPool\EINVWORLD` has **Modify** on `E:\EINVWORLD`
- [ ] Environment variables set (**`ASPNETCORE_ENVIRONMENT`**, connection strings, LHDN secrets, SMTP,
      Turnstile); **secrets are NOT in appsettings.json**
- [ ] (if signing) `.p12` in `Cert\` + signing env vars set
- [ ] **Database backed up** before first start
- [ ] Site starts; `/health/ready` = **Healthy**
- [ ] Admin login works; **2FA enrolled**; recovery codes saved
- [ ] **Admin → System Health** all OK
- [ ] Test invoice **submitted to LHDN** (PREPROD on staging) → Valid + QR
- [ ] Test email received; test PDF downloads
- [ ] Go-live approved

---

### Related documents
- **`README.md`** — what the system is and does.
- **`DOCUMENTATION.md`** — full architecture/technical reference.
- **`DEPLOY-NOTES.md`** — concise operator checklist + manual migration order.
- **`SECRETS-SETUP.md`** — every secret and its environment-variable name.
