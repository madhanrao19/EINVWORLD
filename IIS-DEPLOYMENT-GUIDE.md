# EINVWORLD — IIS Deployment Guide

**Beginner / Intern Friendly Guide**

This guide assumes:

- Windows Server with IIS installed
- SQL Server already installed
- You have received:
  - `EINVWORLD_release_v1.1.zip`
  - SQL database backup (`.bak`)
  - SSL Certificate
  - LHDN credentials
  - Domain name already points to the server

---

## PART A — Preparation

### Step A1 — Create Folders

Open **File Explorer** and create:

```
E:\EINVWORLD
 ├── App
 ├── Documents
 ├── Logs
 ├── Keys      ← DataProtection encryption keys (MUST be outside App)
 └── Cert
```

> **Why `Keys` is separate:** ASP.NET Core stores the keys that protect login cookies, 2FA, and
> antiforgery tokens here. If they lived under `App\`, every redeploy would wipe them and log everyone
> out. Keeping them in `E:\EINVWORLD\Keys` makes them survive redeploys.

---

## PART B — Install .NET 10

### Step B1

Open a browser and download the **ASP.NET Core Runtime Hosting Bundle (.NET 10)** from Microsoft.

### Step B2

Run the installer **as Administrator** → **Next** → **Install** → **Finish**.

### Step B3 — Verify Installation

Open **Command Prompt** and run:

```
dotnet --list-runtimes
```

You should see:

```
Microsoft.AspNetCore.App 10.x.x
Microsoft.NETCore.App 10.x.x
```

---

## PART C — Deploy Application

### Step C1

Extract `EINVWORLD_release_v1.1.zip`.

### Step C2

Copy **all** extracted files into:

```
E:\EINVWORLD\App
```

You should see:

```
EINVWORLD.exe
EINVWORLD.dll
web.config
appsettings.json
appsettings.Production.json
```

…and many DLL files.

---

## PART D — Create IIS Application Pool

Open **Start → IIS Manager** → **Application Pools** → right-click → **Add Application Pool**.

| Field | Value |
|---|---|
| Name | `EINVWORLD` |
| .NET CLR Version | **No Managed Code** |
| Managed Pipeline | **Integrated** |

Click **OK**.

### Step D2 — Configure Pool

Select **EINVWORLD** → **Advanced Settings**:

| Setting | Value |
|---|---|
| Start Mode | **AlwaysRunning** |
| Idle Time-out | **0** |
| Load User Profile | **True** |

Click **OK**.

> These settings keep the background workers (token renewal, status sync, recurring invoices) alive — without them, IIS recycles the app when idle and the workers stop.

---

## PART E — Create Website

Click **Sites** → right-click → **Add Website**.

| Field | Value |
|---|---|
| Site Name | `EINVWORLD` |
| Physical Path | `E:\EINVWORLD\App` |
| Application Pool | `EINVWORLD` |
| Binding Type | `https` |
| Port | `443` |
| Host Name | `einvworld.com` |
| SSL Certificate | *(choose your certificate)* |

Click **OK**.

---

## PART F — Folder Permissions

Open `E:\EINVWORLD` → right-click → **Properties → Security → Edit → Add**.

Enter:

```
IIS AppPool\EINVWORLD
```

Click **Check Names → OK**, then tick **Modify**, **Read**, **Write** and **Apply**.

---

## PART G — Configure Environment Variables

> **IMPORTANT:** Do **NOT** put passwords inside `appsettings.json`. Store them in IIS environment variables.

### Step G1

Open **IIS Manager** → select the **EINVWORLD** website → **Configuration Editor**.

Top dropdown → `system.webServer/aspNetCore`.

Locate **environmentVariables** → click the **…** button.

---

## PART H — Add Variables

Click **Add** for each row below.

**1. Production Mode**

```
Name:  ASPNETCORE_ENVIRONMENT
Value: Production
```

**2. Main Database**

```
Name:  ConnectionStrings__DefaultConnection
Value: Server=localhost,1433;Database=EINVWORLD;User Id=einvworldusr;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False
```

**3. Website Database**

```
Name:  ConnectionStrings__WebsiteDb
Value: Server=localhost,1433;Database=EINVWORLDWEBSITE;User Id=einvworldusr;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False
```

**4. LHDN Secret**

```
Name:  LHDNApiConfig__ClientSecret
Value: YOUR_LHDN_CLIENT_SECRET
```

**5. LHDN Secret 2**

```
Name:  LHDNApiConfig__ClientSecret2
Value: YOUR_LHDN_CLIENT_SECRET2
```

**6. SMTP Password**

```
Name:  EmailConfiguration__Default__SmtpPassword
Value: YOUR_SMTP_PASSWORD
```

**7. Turnstile Secret**

```
Name:  Turnstile__SecretKey
Value: YOUR_TURNSTILE_SECRET
```

**8. Certificate Password**

```
Name:  LHDNApiConfig__CertPass
Value: YOUR_CERT_PASSWORD
```

When completed you should have:

```
ASPNETCORE_ENVIRONMENT
ConnectionStrings__DefaultConnection
ConnectionStrings__WebsiteDb
LHDNApiConfig__ClientSecret
LHDNApiConfig__ClientSecret2
EmailConfiguration__Default__SmtpPassword
Turnstile__SecretKey
LHDNApiConfig__CertPass
```

Click **OK → Apply**.

---

## PART I — Install Certificate File

Copy:

```
DATAMATION_TECHNOLOGY_(M)_SDN._BHD..p12
```

into:

```
E:\EINVWORLD\App\Cert
```

Final location:

```
E:\EINVWORLD\App\Cert\DATAMATION_TECHNOLOGY_(M)_SDN._BHD..p12
```

> **Note:** Enable v1.1 digital signing only after the signing certificate is in place — set `LHDNApiConfig__SigningEnabled = true` and `LHDNApiConfig__DocVersion = 1.1` as environment variables, and validate against the LHDN PREPROD sandbox first.

---

## PART I2 — Database Backup & Auto-Migration

> ⚠️ **Do this BEFORE the first start of a new version.** On startup the app applies any pending
> database schema changes automatically (`DatabaseSettings:AutoMigrateOnStartup` is `true` in
> `appsettings.Production.json`).

### Step I2.1 — Take a full backup (mandatory)

In **SQL Server Management Studio**: right-click the `EINVWORLD` database → **Tasks → Back Up…** →
**Full** → choose a destination → **OK**. This is your rollback if anything goes wrong.

### Step I2.2 — Confirm the app's SQL login can change schema

Auto-migration must `CREATE`/`ALTER` tables, so `einvworldusr` needs DDL rights. In SSMS:
**Security → Logins → einvworldusr → User Mapping →** tick `db_ddladmin` (or `db_owner`) on `EINVWORLD`.

### Step I2.3 — What auto-migration does

- It applies **only the migrations not yet recorded** in the `__EFMigrationsHistory` table.
- The new-version migrations are **additive** — they add tables (`SyncJobs`, `SubmissionRecords`,
  `AuditLogs`), columns, and indexes. **No existing data is dropped or rewritten.**
- The **first** startup after upgrading is **slower** (schema changes run then) and briefly locks the
  affected tables — deploy in a **low-traffic window**.
- The app pool must run a **single worker process** (the default — do not enable a Web Garden), so two
  worker processes don't run migrations at the same time.

> If you ever prefer to apply schema changes manually instead, set `AutoMigrateOnStartup` to `false`
> and run the `Migrations\Apply_*.sql` scripts yourself (see `DEPLOY-NOTES.md` for the order).

### Step I2.4 — Create the DataProtection keys folder

Confirm `E:\EINVWORLD\Keys` exists (from PART A) and the app-pool identity has **Modify** on it
(covered by PART F). The path is already set in `appsettings.Production.json`
(`DataProtection:KeyRingPath`). **The app will not start in Production if this is missing.**

---

## PART J — Restart IIS

Open **Command Prompt as Administrator** and run:

```
iisreset
```

Wait until **"Internet services successfully restarted"** appears.

---

## PART K — Test Website

Open a browser:

```
https://einvworld.com
```

**Expected:** Login Page Appears.

---

## PART L — Test Login

Login with the admin account.

**Expected (first login on the new version):** Administrator accounts must use two-factor
authentication, so you are redirected to a **"Set up authenticator app"** page. Scan the QR code with
Google Authenticator (or similar), enter the 6-digit code, and **save your recovery codes**. After
that, the Dashboard opens and subsequent logins ask for the 6-digit code.

> This is controlled by `Security:EnforceAdminMfa` (default `true`). To disable it, add
> `"Security": { "EnforceAdminMfa": false }` to `appsettings.Production.json` and restart IIS. There is
> no lockout — you always reach the enrolment page, and recovery codes are your backup.

### Quick health check

Open **Admin → System Health** to confirm the database, background jobs, DataProtection keys, disk, and
(if enabled) signing-cert expiry are all OK. `/health/ready` returns 200 when the app can serve traffic.

---

## PART M — Test LHDN

Create one test invoice and submit to **LHDN PREPROD**.

**Expected:** Submitted Successfully.

---

## PART O — (Optional) AI E-Invoice Assistant

The system includes an optional **AI E-Invoice Assistant** (menu: *E-Invoice Assistant*). It answers
e-invoicing questions and turns a plain-English description into a suggested invoice that pre-fills the
Create Invoice form. It is **OFF by default** and the site runs perfectly fine without it.

It runs on a **local, free, open-source** model engine called **Ollama**, installed on the same server.
**No invoice data ever leaves your server** — nothing is sent to any external/cloud AI service.

> ⚠️ Skip this part unless you actually want the assistant. A reasonably modern CPU works; a GPU is faster.
> Allow ~5–10 GB disk for the model.

### Step 1 — Install Ollama

1. Download the Windows installer from **https://ollama.com/download** and run it.
2. After install, Ollama runs as a local service listening on **http://localhost:11434**.

### Step 2 — Download a model

Open a Command Prompt / PowerShell on the server and run **one** of:

```
ollama pull llama3.1
```

(or a smaller/faster option such as `ollama pull qwen2.5` or `ollama pull mistral`).

Test it works:

```
ollama run llama3.1 "Say hello"
```

### Step 3 — Turn the assistant on

In the IIS **environment variables** (same place as PART G/H), add:

| Name | Value |
|------|-------|
| `AIAssistant__Enabled` | `true` |
| `AIAssistant__Model` | `llama3.1` *(must match the model you pulled)* |
| `AIAssistant__BaseUrl` | `http://localhost:11434` *(only if Ollama is not on the default port)* |

Then **restart IIS** (PART J).

**Expected:** Open *E-Invoice Assistant* from the menu, type a question, and get an answer. If it says
the assistant is disabled or unreachable, re-check `AIAssistant__Enabled`, that Ollama is running, and
that the model name matches.

> The assistant only **suggests** — it never submits, cancels, or changes any document. Always review
> every field (especially supplier/customer, tax and classification) before saving a prefilled invoice.

---

## PART P — (Optional) Invoice Ingestion Features

All OFF by default and **draft-safe** — they validate or suggest only; none creates or submits an
invoice automatically. Skip this part unless you want them.

| Feature | How to enable | Where |
|---|---|---|
| **AI Document Capture** (PDF → suggestion) | env vars `DocumentCapture__Enabled=true` **and** the Ollama assistant from PART O | menu: *AI Document Capture* (`/Invoices/CreateFromFile`) |
| **Bulk Import** (CSV/XLSX validation) | nothing — always available to Admin/Supplier | menu: *Bulk Import* (`/Invoices/BulkImport`) |
| **Watched-folder importer** | env vars `WatchedFolderImport__Enabled=true`, `WatchedFolderImport__InboxPath=E:\EINVWORLD\Inbox` (create the folder, grant the app-pool **Modify**) | drop CSV/XLSX into the Inbox; results move to `Processed\` / `Rejected\` with a `.report.json` |
| **REST validate API** | env var `Api__Key=<a-long-random-key>` | `POST https://einvworld.com/api/import/validate` with header `X-Api-Key` |

> Document Capture handles **digital PDFs (with a text layer)**. Scanned images report "needs OCR" — image
> OCR is a later phase.

After adding any of these env vars, **restart IIS** (PART J).

---

## PART N — Troubleshooting

**Website Not Opening** — Check: Is the IIS site started?

**HTTP Error 500** — Check `E:\EINVWORLD\App\logs` for `stdout*.log`; open the latest file. The error usually tells you exactly what is missing.

**Database Error** — Check `ConnectionStrings__DefaultConnection` in the IIS environment variables.

**LHDN Error** — Check `LHDNApiConfig__ClientSecret` and `LHDNApiConfig__ClientSecret2` in the IIS environment variables.

**Email Not Sending** — Check `EmailConfiguration__Default__SmtpPassword` in the IIS environment variables.

**AI Assistant says "disabled" or "could not reach"** — Confirm `AIAssistant__Enabled=true`, that Ollama is installed and running (open `http://localhost:11434` on the server — it should say "Ollama is running"), and that `AIAssistant__Model` exactly matches a model you pulled (`ollama list`).

---

## Deployment Checklist

- [ ] .NET 10 Hosting Bundle Installed
- [ ] Application Files Copied
- [ ] Application Pool Created (single worker process)
- [ ] Website Created
- [ ] SSL Certificate Assigned
- [ ] Folder Permissions Set (incl. `E:\EINVWORLD\Keys` = Modify)
- [ ] **DataProtection `Keys` folder exists** (app will not start without `KeyRingPath`)
- [ ] Environment Variables Added (secrets — NOT in appsettings)
- [ ] P12 Certificate Copied
- [ ] **Database backed up (full)** — before first start
- [ ] **`einvworldusr` has DDL rights** (for auto-migration)
- [ ] IIS Restarted
- [ ] Login Tested (admin **2FA enrolled**)
- [ ] **Admin → System Health checked**
- [ ] LHDN Tested
- [ ] Email Tested
- [ ] Go Live Approved
