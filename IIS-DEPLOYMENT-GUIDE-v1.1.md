# EINVWORLD v1.1 — IIS Deployment Guide

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
 └── Cert
```

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

**Expected:** Dashboard Opens.

---

## PART M — Test LHDN

Create one test invoice and submit to **LHDN PREPROD**.

**Expected:** Submitted Successfully.

---

## PART N — Troubleshooting

**Website Not Opening** — Check: Is the IIS site started?

**HTTP Error 500** — Check `E:\EINVWORLD\App\logs` for `stdout*.log`; open the latest file. The error usually tells you exactly what is missing.

**Database Error** — Check `ConnectionStrings__DefaultConnection` in the IIS environment variables.

**LHDN Error** — Check `LHDNApiConfig__ClientSecret` and `LHDNApiConfig__ClientSecret2` in the IIS environment variables.

**Email Not Sending** — Check `EmailConfiguration__Default__SmtpPassword` in the IIS environment variables.

---

## Deployment Checklist

- [ ] .NET 10 Hosting Bundle Installed
- [ ] Application Files Copied
- [ ] Application Pool Created
- [ ] Website Created
- [ ] SSL Certificate Assigned
- [ ] Folder Permissions Set
- [ ] Environment Variables Added
- [ ] P12 Certificate Copied
- [ ] IIS Restarted
- [ ] Login Tested
- [ ] LHDN Tested
- [ ] Email Tested
- [ ] Backup Taken
- [ ] Go Live Approved
