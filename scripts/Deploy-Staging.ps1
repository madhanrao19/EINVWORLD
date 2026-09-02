<#
.SYNOPSIS
    Publishes EINVWORLD (Release, win-x64) and copies it to the Staging App folder without ever
    touching server-only files that carry secrets.

.DESCRIPTION
    A plain `dotnet publish` with a FileSystem PublishProfile, or a naive `robocopy /E` /`/MIR`
    of the publish output, will happily overwrite `web.config` on the server. On this deployment,
    `web.config` is NOT the generic one built from source — it carries the real
    <environmentVariables> block (DB connection strings, LHDN client secret, SMTP password,
    Turnstile secret key, DataProtection key-ring path, etc.) that the checked-in appsettings*.json
    files deliberately leave blank. Overwriting it takes the site down at next start with something
    like `ArgumentNullException: connectionString` from the Serilog SQL sink, because the
    environment variables that used to fill those blanks are gone. (This happened once, by hand,
    on 2026-09-02 — recovered from the pre-deploy backup within seconds, but avoidable. This
    script exists so it can't happen again.)

    Steps:
      1. `dotnet publish` (Release, win-x64, framework-dependent) to a local folder.
      2. Back up the current server `App\` folder to a timestamped sibling folder (your rollback
         point — never deleted automatically).
      3. Copy the fresh publish output into `App\`, EXCLUDING `web.config` and
         `appsettings.Production.json` (the two files DEPLOY-NOTES.md calls out as
         "never overwrite server secrets"). Existing files at the destination not present in the
         publish output are left alone (no `/MIR` — this script never deletes anything on the
         server).

    This script does NOT stop/start the IIS site or app pool, and does NOT apply EF migrations —
    both are manual steps by design (see DEPLOY-NOTES.md §0). Stop the site before running this,
    start it again after, and check `Migrations\Apply_*.sql` against the target's
    `__EFMigrationsHistory` first if any migration has landed since the last deploy.

.PARAMETER DestAppPath
    UNC (or local) path to the target `App\` folder. Defaults to the Staging deployment path from
    Properties\PublishProfiles\EINVWORLD STAGING.pubxml.

.PARAMETER SourcePublishPath
    Where `dotnet publish` writes its output. Defaults to bin\Release\net10.0\win-x64\publish under
    the repo root. Combine with -SkipPublish to reuse an existing publish output as-is.

.PARAMETER SkipPublish
    Skip the `dotnet publish` step and copy whatever is already at -SourcePublishPath.

.PARAMETER SkipBackup
    Skip backing up the current server App folder first. Not recommended — only use this if you
    already have a known-good backup or rollback point from earlier in the same session.

.PARAMETER ExtraExcludeFiles
    Additional filenames (not full paths) to exclude from the copy, on top of the built-in
    web.config / appsettings.Production.json exclusions. Space-separated.

.PARAMETER WhatIf
    Preview what would be copied/backed up without changing anything on the server.

.EXAMPLE
    # Standard staging deploy (stop the site first, start it again after):
    .\Deploy-Staging.ps1

.EXAMPLE
    # Reuse an already-built publish output, skip the backup (already have one from earlier today):
    .\Deploy-Staging.ps1 -SkipPublish -SkipBackup

.EXAMPLE
    # Dry run — see what would change without touching the server:
    .\Deploy-Staging.ps1 -WhatIf

.NOTES
    Run from the repo root or anywhere — paths are resolved relative to this script's location.
    Requires network access to the destination UNC path (e.g. \\192.168.1.26\e$\...).
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$DestAppPath = '\\192.168.1.26\e$\EINVWORLD_STAGING\App',

    [string]$SourcePublishPath,

    [switch]$SkipPublish,

    [switch]$SkipBackup,

    [string[]]$ExtraExcludeFiles = @()
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

if (-not (Test-Path $DestAppPath)) {
    throw "Destination path not reachable: $DestAppPath. Check network access / the share is up before retrying."
}

# ── 1. Publish ──────────────────────────────────────────────────────────────────────────────────
if (-not $SkipPublish) {
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

# ── 2. Backup ───────────────────────────────────────────────────────────────────────────────────
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

# ── 3. Deploy (copy, never delete, never touch excluded files) ────────────────────────────────────
Write-Host ""
Write-Host "Copying publish output into App\ (excluding: $($excludeFiles -join ', ')) ..." -ForegroundColor Cyan

$rcArgs = @($SourcePublishPath, $DestAppPath, '/E', '/R:2', '/W:2', '/NFL', '/NDL', '/NJH', '/XF') + $excludeFiles
if ($WhatIfPreference) { $rcArgs += '/L' }

if ($PSCmdlet.ShouldProcess($DestAppPath, 'Copy publish output (excluding server-only files)')) {
    robocopy @rcArgs | Select-Object -Last 8
    if ($LASTEXITCODE -ge 8) { throw "Deploy robocopy failed (exit code $LASTEXITCODE) - check output above. Your pre-deploy backup is intact." }
    Write-Host "Copy complete." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Done. Remaining manual steps ===" -ForegroundColor Cyan
Write-Host "  1. If any new migration landed since the last deploy, apply its Migrations\Apply_*.sql" -ForegroundColor Yellow
Write-Host "     against the target DB BEFORE starting the site (see DEPLOY-NOTES.md)." -ForegroundColor Yellow
Write-Host "  2. Start the site / app pool." -ForegroundColor Yellow
Write-Host "  3. Verify: GET /health and /health/ready both return 200; sign in; open an invoice." -ForegroundColor Yellow
