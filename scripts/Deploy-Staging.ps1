<#
.SYNOPSIS
    Publishes EINVWORLD (Release, win-x64), applies pending DB migrations (with a DB backup first),
    and copies it to the Staging (default) or Production App folder without ever touching
    server-only files that carry secrets.

.DESCRIPTION
    A plain `dotnet publish` with a FileSystem PublishProfile, or a naive `robocopy /E` /`/MIR`
    of the publish output, will happily overwrite `web.config` on the server. On this deployment,
    `web.config` is NOT the generic one built from source - it carries the real
    <environmentVariables> block (DB connection strings, LHDN client secret, SMTP password,
    Turnstile secret key, DataProtection key-ring path, etc.) that the checked-in appsettings*.json
    files deliberately leave blank. Overwriting it takes the site down at next start with something
    like `ArgumentNullException: connectionString` from the Serilog SQL sink, because the
    environment variables that used to fill those blanks are gone. (This happened once, by hand,
    on 2026-09-02 - recovered from the pre-deploy backup within seconds, but avoidable. This
    script exists so it can't happen again.)

    Steps:
      1. `dotnet publish` (Release, win-x64, framework-dependent) to a local folder.
      2. Back up the current server `App\` folder to a timestamped sibling folder (your rollback
         point - never deleted automatically).
      3. Copy the fresh publish output into `App\`, EXCLUDING `web.config` and
         `appsettings.Production.json` (the two files DEPLOY-NOTES.md calls out as
         "never overwrite server secrets"). Existing files at the destination not present in the
         publish output are left alone (no `/MIR` - this script never deletes anything on the
         server).

    Database migrations (unless -SkipMigrations): for each database in the target's web.config
    (ConnectionStrings__DefaultConnection -> ApplicationDbContext, ConnectionStrings__WebsiteDb ->
    WebsiteDbContext), the migrations in the repo that are missing from `__EFMigrationsHistory` are
    applied by running their idempotent `Migrations\Apply_<Name>.sql`, in order, AFTER a COPY_ONLY
    `BACKUP DATABASE` into the SQL instance's default backup folder. This runs before the site goes
    offline - safe because migrations are additive-only (CLAUDE.md), so the old build keeps working on
    the new schema. Any failure stops the deploy before a single app file is copied.

    The site is taken offline only for the copy step, via `app_offline.htm` (no IIS admin rights
    needed), and comes back on the next request.

.PARAMETER DestAppPath
    UNC (or local) path to the target `App\` folder. Defaults to the Staging deployment path from
    Properties\PublishProfiles\EINVWORLD STAGING.pubxml.

.PARAMETER SourcePublishPath
    Where `dotnet publish` writes its output. Defaults to bin\Release\net10.0\win-x64\publish under
    the repo root. Combine with -SkipPublish to reuse an existing publish output as-is.

.PARAMETER SkipPublish
    Skip the `dotnet publish` step and copy whatever is already at -SourcePublishPath.

.PARAMETER SkipBackup
    Skip backing up the current server App folder first. Not recommended - only use this if you
    already have a known-good backup or rollback point from earlier in the same session.

.PARAMETER ExtraExcludeFiles
    Additional filenames (not full paths) to exclude from the copy, on top of the built-in
    web.config / appsettings.Production.json exclusions. Space-separated.

.PARAMETER SkipMigrations
    Skip the database-migration step entirely.

.PARAMETER MigrationCredential
    SQL login to run migrations with (e.g. a db_ddladmin login: -MigrationCredential (Get-Credential)).
    Defaults to the login in the target's web.config connection strings.

.PARAMETER SqlServer
    SQL Server to connect to. Defaults to the host of -DestAppPath plus the port from the target's
    connection string (web.config says "localhost,1433", which only works on the server itself).

.PARAMETER WhatIf
    Preview what would be copied/backed up/migrated without changing anything on the server.
    Pending migrations are still listed (read-only query).

.EXAMPLE
    # Standard staging deploy (one command - site goes offline only during the copy):
    .\Deploy-Staging.ps1

.EXAMPLE
    # Production (asks Y/N before each step):
    .\Deploy-Staging.ps1 -DestAppPath '\\192.168.1.26\e$\EINVWORLD\App' -Confirm

.EXAMPLE
    # Reuse an already-built publish output, skip the backup (already have one from earlier today):
    .\Deploy-Staging.ps1 -SkipPublish -SkipBackup

.EXAMPLE
    # Dry run - see what would change without touching the server:
    .\Deploy-Staging.ps1 -WhatIf

.NOTES
    Run from the repo root or anywhere - paths are resolved relative to this script's location.
    Requires network access to the destination UNC path (e.g. \\192.168.1.26\e$\...).
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$DestAppPath = '\\192.168.1.26\e$\EINVWORLD_STAGING\App',

    [string]$SourcePublishPath,

    [switch]$SkipPublish,

    [switch]$SkipBackup,

    [string[]]$ExtraExcludeFiles = @(),

    [switch]$SkipMigrations,

    [pscredential]$MigrationCredential,

    [string]$SqlServer
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

if (-not $SourcePublishPath) {
    $SourcePublishPath = Join-Path $repoRoot 'bin\Release\net10.0\win-x64\publish'
}

# Files that must never be overwritten on the server - see .DESCRIPTION above.
$excludeFiles = @('web.config', 'appsettings.Production.json') + $ExtraExcludeFiles

Write-Host "=== EINVWORLD Staging Deploy ===" -ForegroundColor Cyan
Write-Host "Source (publish output): $SourcePublishPath"
Write-Host "Destination (server App\): $DestAppPath"
Write-Host "Excluded from copy: $($excludeFiles -join ', ')" -ForegroundColor Yellow
Write-Host ""

if ((Test-Path $DestAppPath) -and ($DestAppPath -notlike '\\*') -and -not $SkipPublish) {
    # Publishing ON the web server means the build SDK must exist there too (that's how the
    # "SDK 10.0.300 not found" failure happens). Build on the dev box instead: publish there,
    # copy the output folder across, then run this script here with -SkipPublish.
    Write-Warning "DestAppPath is local and -SkipPublish was not passed, so this will build on the web server. Publish on your dev box and copy bin\Release\net10.0\win-x64\publish over, then re-run with -SkipPublish."
}

if (-not (Test-Path $DestAppPath)) {
    throw "Destination path not reachable: $DestAppPath. Check network access / the share is up before retrying."
}

# -- 1. Publish ----------------------------------------------------------------------------------
if (-not $SkipPublish) {
    # global.json pins the SDK (10.0.300). On a machine that only has an older SDK (e.g. the web
    # server with just 8.0.411), `dotnet publish` dies with the opaque "A compatible .NET SDK was
    # not be found ... Requested SDK version: 10.0.300" error, surfaced here as exit code
    # -2147450752. Install the pinned SDK per-user (no admin, no PATH churn) instead.
    # Any SDK satisfying global.json's rollForward counts as present (latestFeature = same
        # major.minor, version >= pinned), so this only installs when the pin is genuinely unmet.
        $pinnedSdk = (Get-Content (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
        $pin = if ($pinnedSdk) { [version]$pinnedSdk } else { $null }
        $have = @(dotnet --list-sdks | ForEach-Object { [version](($_ -split ' ')[0]) } |
            Where-Object { $pin -and $_.Major -eq $pin.Major -and $_.Minor -eq $pin.Minor -and $_ -ge $pin })
        if ($pinnedSdk -and -not $have) {
            $dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
            Write-Host "SDK $pinnedSdk not installed (global.json) - installing per-user into $dotnetDir ..." -ForegroundColor Yellow
            $installer = Join-Path $env:TEMP 'dotnet-install.ps1'
            Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
            & $installer -Version $pinnedSdk -InstallDir $dotnetDir -NoPath
            if ($LASTEXITCODE -ne 0) { throw "dotnet-install.ps1 failed (exit $LASTEXITCODE) - install SDK $pinnedSdk manually, or use -SkipPublish with a pre-built output." }
            $env:PATH = "$dotnetDir;$env:PATH"
            Write-Host "SDK $pinnedSdk installed." -ForegroundColor Green
        }
    if ($PSCmdlet.ShouldProcess($repoRoot, 'dotnet publish (Release, win-x64)')) {
        Write-Host "Publishing..." -ForegroundColor Cyan
        Push-Location $repoRoot
        try {
            dotnet publish EINVWORLD.csproj -c Release -r win-x64 --self-contained false -o $SourcePublishPath
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
        }
        finally {
            Pop-Location
        }
    }
}
else {
    Write-Host "Skipping publish (-SkipPublish); using existing output at $SourcePublishPath" -ForegroundColor Yellow
    if (-not (Test-Path (Join-Path $SourcePublishPath 'EINVWORLD.exe'))) {
        throw "No EINVWORLD.exe found at $SourcePublishPath - nothing to deploy. Run without -SkipPublish first."
    }
}

# -- 2. Backup -----------------------------------------------------------------------------------
if (-not $SkipBackup) {
    $deployedVersion = 'unknown'
    $verFile = Join-Path $DestAppPath 'appsettings.json'
    if (Test-Path $verFile) {
        $match = (Get-Content $verFile -Raw) | Select-String '"Version":\s*"(v[\d.]+)"'
        if ($match) { $deployedVersion = $match.Matches[0].Groups[1].Value }
    }
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupPath = Join-Path (Split-Path $DestAppPath -Parent) "App_Backup_${deployedVersion}_$stamp"

    if ($PSCmdlet.ShouldProcess($backupPath, "Back up current App\ ($deployedVersion)")) {
        Write-Host "Backing up current App\ ($deployedVersion) to $backupPath ..." -ForegroundColor Cyan
        $rcArgs = @($DestAppPath, $backupPath, '/E', '/R:2', '/W:2', '/NFL', '/NDL', '/NJH')
        robocopy @rcArgs | Select-Object -Last 6
        # Robocopy exit codes 0-7 are all non-fatal ("files copied" etc); only 8+ means real trouble.
        if ($LASTEXITCODE -ge 8) { throw "Backup robocopy failed (exit code $LASTEXITCODE) - aborting before touching App\." }
        Write-Host "Backup complete: $backupPath" -ForegroundColor Green
    }
}
else {
    Write-Host "Skipping backup (-SkipBackup) - make sure you have a rollback point already." -ForegroundColor Yellow
}

# -- 2b. Database migrations (backup DB -> run pending Apply_*.sql -> verify) ----------------------
if (-not $SkipMigrations) {
    Write-Host ""
    Write-Host "Checking database migrations..." -ForegroundColor Cyan

    # Migrations in this build, per DbContext, from the Designer files' attributes.
    $codeMigrations = @{}
    foreach ($f in Get-ChildItem (Join-Path $repoRoot 'Migrations') -Filter '*.Designer.cs') {
        $text = Get-Content $f.FullName -Raw
        $ctx = [regex]::Match($text, '\[DbContext\(typeof\((\w+)\)\)\]').Groups[1].Value
        $id  = [regex]::Match($text, '\[Migration\("([^"]+)"\)\]').Groups[1].Value
        if ($ctx -and $id) { $codeMigrations[$ctx] = @($codeMigrations[$ctx] | Where-Object { $_ }) + $id }
    }

    $webConfig = Join-Path $DestAppPath 'web.config'
    if (-not (Test-Path $webConfig)) { throw "No web.config at $webConfig - cannot find the database connection strings. Use -SkipMigrations to deploy without migrating." }
    $envVars = @{}
    ([xml](Get-Content $webConfig -Raw)).SelectNodes('//environmentVariable') | ForEach-Object { $envVars[$_.name] = $_.value }
    $targets = @(
        @{ Context = 'ApplicationDbContext'; Setting = 'ConnectionStrings__DefaultConnection' },
        @{ Context = 'WebsiteDbContext';     Setting = 'ConnectionStrings__WebsiteDb' }
    )

    # sqlcmd writes errors to stderr; in Windows PowerShell 5.1 that becomes a terminating
    # NativeCommandError under ErrorActionPreference=Stop, so relax it here and check the exit code.
    function Invoke-Sqlcmd2([string]$server, [string]$db, $b, [string[]]$sqlArgs) {
        $auth = @(if ($MigrationCredential) { '-U'; $MigrationCredential.UserName }
                  elseif ($b.IntegratedSecurity) { '-E' }
                  else { '-U'; $b.UserID })
        $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        try { $out = & sqlcmd -S $server -d $db @auth -C -I -b -l 30 -h -1 -W @sqlArgs 2>&1 }
        finally { $ErrorActionPreference = $eap }
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed on [$db] (exit $LASTEXITCODE): $(($out | Out-String).Trim())" }
        $out | ForEach-Object { "$_".Trim() } | Where-Object { $_ }
    }

    $plan = @()
    try {
        foreach ($t in $targets) {
            $cs = $envVars[$t.Setting]
            if (-not $cs) { throw "$($t.Setting) not found in $webConfig. Use -SkipMigrations to deploy without migrating." }
            $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $cs
            $server = $SqlServer
            if (-not $server) {
                $server = $b.DataSource
                # web.config says "localhost,1433" (it's read on the server); swap in the UNC host.
                if ($DestAppPath -like '\\*' -and $server -match '^(localhost|\.|\(local\)|127\.0\.0\.1)(,\d+)?$') {
                    $server = ($DestAppPath -split '\\')[2] + $Matches[2]   # UNC host + ",port"
                }
            }
            $env:SQLCMDPASSWORD = if ($MigrationCredential) { $MigrationCredential.GetNetworkCredential().Password } else { $b.Password }

            $applied = Invoke-Sqlcmd2 $server $b.InitialCatalog $b @('-Q', "SET NOCOUNT ON; IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NOT NULL SELECT MigrationId FROM [__EFMigrationsHistory]")
            $pending = @($codeMigrations[$t.Context] | Where-Object { $_ -notin $applied } | Sort-Object)
            if ($pending.Count -eq 0) {
                Write-Host "  [$($b.InitialCatalog)] up to date." -ForegroundColor Green
                continue
            }
            if (@($applied).Count -eq 0) { throw "[$($b.InitialCatalog)] has no __EFMigrationsHistory rows - refusing to auto-migrate an empty/unknown database. Set it up manually (DEPLOY-NOTES.md section 1)." }

            $scripts = foreach ($id in $pending) {
                $sql = Join-Path $repoRoot ("Migrations\Apply_{0}.sql" -f ($id -replace '^\d+_', ''))
                if (-not (Test-Path $sql)) { throw "[$($b.InitialCatalog)] pending migration $id has no $sql - apply it manually (DEPLOY-NOTES.md section 1). Nothing was changed." }
                $sql
            }
            Write-Host "  [$($b.InitialCatalog)] pending: $($pending -join ', ')" -ForegroundColor Yellow
            $plan += @{ Server = $server; Db = $b.InitialCatalog; Builder = $b; Pending = $pending; Scripts = @($scripts); Password = $env:SQLCMDPASSWORD }
        }

        foreach ($p in $plan) {
            $env:SQLCMDPASSWORD = $p.Password
            $label = "[$($p.Db)] backup + apply $($p.Pending.Count) migration(s)"
            if (-not $PSCmdlet.ShouldProcess($p.Db, $label)) { continue }

            $bak = "$($p.Db)_pre-deploy_$(Get-Date -Format 'yyyyMMdd_HHmmss').bak"
            # Instance default backup folder, falling back to its data folder when none is configured.
            $bakSql = "SET NOCOUNT ON; DECLARE @d nvarchar(400) = CAST(COALESCE(SERVERPROPERTY('InstanceDefaultBackupPath'), SERVERPROPERTY('InstanceDefaultDataPath')) AS nvarchar(400)); " +
                      "IF RIGHT(@d, 1) <> N'\' SET @d += N'\'; DECLARE @f nvarchar(500) = @d + N'$bak'; " +
                      "BACKUP DATABASE [$($p.Db)] TO DISK = @f WITH COPY_ONLY, CHECKSUM, INIT; SELECT @f;"
            Write-Host "  [$($p.Db)] backing up..." -ForegroundColor Cyan
            $bakPath = Invoke-Sqlcmd2 $p.Server $p.Db $p.Builder @('-Q', $bakSql) | Select-Object -Last 1
            Write-Host "  [$($p.Db)] backup: $bakPath (on the SQL server)" -ForegroundColor Green

            foreach ($sql in $p.Scripts) {
                Write-Host "  [$($p.Db)] applying $(Split-Path $sql -Leaf) ..." -ForegroundColor Cyan
                try { Invoke-Sqlcmd2 $p.Server $p.Db $p.Builder @('-i', $sql) | Out-Null }
                catch { throw "Migration FAILED - app files NOT deployed, site untouched. DB backup: $bakPath. $_" }
            }

            $nowApplied = Invoke-Sqlcmd2 $p.Server $p.Db $p.Builder @('-Q', "SET NOCOUNT ON; SELECT MigrationId FROM [__EFMigrationsHistory]")
            $missing = @($p.Pending | Where-Object { $_ -notin $nowApplied })
            if ($missing.Count) { throw "[$($p.Db)] scripts ran but history still lacks: $($missing -join ', '). App files NOT deployed. DB backup: $bakPath" }
            Write-Host "  [$($p.Db)] migrated: $($p.Pending -join ', ')" -ForegroundColor Green
        }
    }
    finally {
        Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue -WhatIf:$false
    }
}
else {
    Write-Host "Skipping database migrations (-SkipMigrations)." -ForegroundColor Yellow
}

# -- 3. Deploy (copy, never delete, never touch excluded files) ------------------------------------
Write-Host ""
Write-Host "Copying publish output into App\ (excluding: $($excludeFiles -join ', ')) ..." -ForegroundColor Cyan

$rcArgs = @($SourcePublishPath, $DestAppPath, '/E', '/R:2', '/W:2', '/NFL', '/NDL', '/NJH', '/XF') + $excludeFiles
if ($WhatIfPreference) { $rcArgs += '/L' }

# app_offline.htm makes the ASP.NET Core Module stop the app and release its DLL locks, so no manual
# site stop is needed. Dropped AFTER the backup (so a restored backup never ships it) and always removed,
# even if the copy fails - removing it brings the site back up on the next request.
$offlineFile = Join-Path $DestAppPath 'app_offline.htm'
if ($PSCmdlet.ShouldProcess($DestAppPath, 'Copy publish output (excluding server-only files)')) {
    Set-Content -Path $offlineFile -Value '<html><body><h2>EINVWORLD is being updated - back in a minute.</h2></body></html>' -Encoding utf8
    try {
        Start-Sleep -Seconds 5   # give ANCM time to shut the app down
        robocopy @rcArgs | Select-Object -Last 8
        if ($LASTEXITCODE -ge 8) { throw "Deploy robocopy failed (exit code $LASTEXITCODE) - check output above. Your pre-deploy backup is intact." }
        Write-Host "Copy complete." -ForegroundColor Green
    }
    finally {
        Remove-Item -Path $offlineFile -Force -ErrorAction SilentlyContinue
        Write-Host "Site back online (app_offline.htm removed)." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "=== Done. Remaining manual steps ===" -ForegroundColor Cyan
if ($SkipMigrations) {
    Write-Host "  - Migrations were skipped: apply any pending Migrations\Apply_*.sql yourself (DEPLOY-NOTES.md)." -ForegroundColor Yellow
}
Write-Host "  - Verify: GET /health and /health/ready both return 200; sign in; open an invoice." -ForegroundColor Yellow
