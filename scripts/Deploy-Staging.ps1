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
      3. Copy the fresh publish output into `App\`, EXCLUDING `web.config` (the only file that holds
         server secrets). Every appsettings*.json is shipped from the repo, including
         `appsettings.Production.json`, which carries Production's non-secret values (folders, log
         path, Turnstile site key, LHDN host) so nothing has to be edited on the server after a
         deploy. Per-server overrides go in web.config environment variables. No `/MIR`: the only files ever deleted are ones the
         PREVIOUS run of this script shipped (listed in `App\.deploy-manifest.txt`) that are no
         longer in this release, so old JS/CSS/DLLs don't pile up. Files it never shipped (uploads
         under wwwroot, server-only files) are never touched; the first run only lists candidates.

    Database migrations (unless -SkipMigrations): for each database in the target's web.config
    (ConnectionStrings__DefaultConnection -> ApplicationDbContext, ConnectionStrings__WebsiteDb ->
    WebsiteDbContext), the migrations in the repo that are missing from `__EFMigrationsHistory` are
    applied by running their idempotent `Migrations\Apply_<Name>.sql`, in order, AFTER a COPY_ONLY
    `BACKUP DATABASE` into the SQL instance's default backup folder. This runs before the site goes
    offline - safe because migrations are additive-only (CLAUDE.md), so the old build keeps working on
    the new schema. Any failure stops the deploy before a single app file is copied.

    The site is taken offline only for the copy step, via `app_offline.htm` (no IIS admin rights
    needed), and comes back on the next request. Nothing is copied until the app has released every
    .dll/.exe in App\ (-ShutdownTimeoutSeconds). After the copy every shipped file is checked against
    the build. If the copy, that check or the health check fails, App\ is restored from the pre-deploy
    backup and verified, and the script fails (nonzero exit). A mixed build is never put online: with
    -SkipBackup, or if the restore itself fails, the site stays offline (app_offline.htm) for a human.
    Robocopy logs go to <target>\DeployLogs\ next to the backups.

    Disk space and clean-up: before anything changes, the server disk must hold a full backup of
    App\ plus 1 GB and this PC's disk 3 GB (else the deploy stops; below 20 GB free it warns). After
    the copy, the public security check and a /health/live + /health/ready check (both through the
    public site), and only if all passed, the local publish output and obj\Release\*\win-x64 are
    deleted - never anything outside the repo's bin\ and obj\, never the NuGet cache or the server
    backups. A failed run keeps them for diagnosis; -KeepPublishOutput keeps them on purpose.

.PARAMETER DestAppPath
    UNC (or local) path to the target `App\` folder. Defaults to the Staging deployment path from
    Properties\PublishProfiles\EINVWORLD STAGING.pubxml.

.PARAMETER SourcePublishPath
    Where `dotnet publish` writes its output. Defaults to bin\Release\net10.0\win-x64\publish under
    the repo root. Combine with -SkipPublish to reuse an existing publish output as-is. Emptied before
    each publish when it is inside the repo's bin\; any other folder must be empty or new.

.PARAMETER SkipPublish
    Skip the `dotnet publish` step and copy the output already at -SourcePublishPath. Only accepted when
    that folder still holds exactly the files this script's last clean publish recorded
    (.deploy-publish-files.txt) and the repo's version - otherwise the deploy stops before any copy.
    Files that must never be public (wwwroot\Drafts, wwwroot\tempcsv, test pages) are refused at the
    source and removed from the server after the copy (scripts\DeployGuards.ps1).

.PARAMETER SkipBackup
    Skip backing up the current server App folder first. Not recommended - only use this if you
    already have a known-good backup or rollback point from earlier in the same session.

.PARAMETER ExtraExcludeFiles
    Additional filenames (not full paths) to exclude from the copy, on top of the built-in
    web.config exclusion. Space-separated.

.PARAMETER SkipMigrations
    Skip the database-migration step entirely.

.PARAMETER MigrationCredential
    SQL login to run migrations with (a db_owner login: -MigrationCredential (Get-Credential)).
    Defaults to the login in the target's web.config connection strings.

.PARAMETER SqlServer
    SQL Server to connect to. Defaults to the host of -DestAppPath plus the port from the target's
    connection string (web.config says "localhost,1433", which only works on the server itself).

.PARAMETER SkipRuntimeCheck
    Skip step 0, the server .NET runtime check (see the "Server .NET runtime" section in the script).

.PARAMETER PublicBaseUrl
    Public site the post-deploy security check goes through (Cloudflare included). Defaults to
    https://staging.einvworld.com for the Staging App folder and https://www.einvworld.com for Production;
    any other target skips the check with a warning unless this is given. After the copy, every known
    never-public path (scripts\never-public-known-paths.txt) and every never-public file this deploy removed
    is requested; any content (2xx) or Cloudflare cache hit - or no answer after retries - fails the run.

.PARAMETER KeepPublishOutput
    Keep the local publish output after a successful deploy (normally deleted, see "Disk space and clean-up"),
    e.g. to deploy the same build to Production next with -SkipPublish.

.PARAMETER ShutdownTimeoutSeconds
    How long to wait, after app_offline.htm, for the app to stop and release every .dll/.exe in App\ (default
    120: the app's own 30 s graceful shutdown plus IIS's 90 s limit). Nothing is copied before that; on timeout
    the deploy stops, nothing was copied, and the previous version comes back online.

.PARAMETER CopyRetryWaitSeconds
    Seconds robocopy waits between its 3 retries of a file (default 5).

.PARAMETER HealthRetryDelayMs
    Base gap between post-deploy health-check attempts (default 3000, growing per attempt).

.PARAMETER WhatIf
    Preview what would be copied/backed up/migrated without changing anything on the server.
    Pending migrations are still listed (read-only query). Also runs read-only preflight checks and
    warns about anything that would make the real run fail: write access to the backup/App folders,
    PowerShell remoting to the server (if the Hosting Bundle needs installing), and the SQL login's
    BACKUP/DDL/INSERT rights (if migrations are pending).

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

    [string]$SqlServer,

    [switch]$SkipRuntimeCheck,

    [string]$PublicBaseUrl,

    [switch]$KeepPublishOutput,

    [ValidateRange(1, 900)][int]$ShutdownTimeoutSeconds = 120,

    [ValidateRange(1, 60)][int]$CopyRetryWaitSeconds = 5,

    [ValidateRange(0, 60000)][int]$HealthRetryDelayMs = 3000
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
. (Join-Path $PSScriptRoot 'DeployGuards.ps1')   # stale-file guards: Initialize-PublishFolder, Assert-PublishOutput, ...

if (-not $SourcePublishPath) {
    $SourcePublishPath = Join-Path $repoRoot 'bin\Release\net10.0\win-x64\publish'
}

# Files that must never be overwritten on the server - see .DESCRIPTION above.
$excludeFiles = @('web.config', $PublishStampName) + $ExtraExcludeFiles

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

if ($WhatIfPreference) {
    # -WhatIf skips every write, so prove the real run can write where the backup and the copy go.
    # [IO.File] because -WhatIf turns New-Item/Remove-Item into no-ops.
    foreach ($dir in @((Split-Path $DestAppPath -Parent), $DestAppPath)) {
        $probe = Join-Path $dir ".deploy-whatif-probe-$PID.tmp"
        try { [IO.File]::WriteAllText($probe, 'probe'); [IO.File]::Delete($probe); Write-Host "Preflight: can write to $dir - OK." -ForegroundColor Green }
        catch { Write-Warning "Preflight: can't write to $dir ($($_.Exception.Message)) - the real run's backup/copy will fail." }
    }
}

# Disk space: the server needs room for the backup (a full App\ copy) and the copy; this PC for the publish.
# Below what a step needs the deploy stops before changing anything; below 20 GB it only warns.
$appBytes = 0
if (-not $SkipBackup) {
    try { $appBytes = [int64](((New-Object IO.DirectoryInfo $DestAppPath).GetFiles('*', [IO.SearchOption]::AllDirectories) | Measure-Object Length -Sum).Sum) }
    catch { Write-Warning "Can't size $DestAppPath ($($_.Exception.Message)) - the disk check below assumes the backup needs nothing extra." }
}
foreach ($disk in @(@{ Name = 'Server'; Path = $DestAppPath; Need = $appBytes + 1GB },
                    @{ Name = 'Local'; Path = $SourcePublishPath; Need = $(if ($SkipPublish) { 0 } else { 2GB }) + 1GB })) {
    $free = Get-FreeSpaceBytes $disk.Path
    $msg = "$($disk.Name) disk ($($disk.Path)): $('{0:N1}' -f ($free / 1GB)) GB free, needs $('{0:N1}' -f ($disk.Need / 1GB)) GB"
    switch (Get-DiskSpaceVerdict $free $disk.Need) {
        'BLOCK' {
            if ($WhatIfPreference) { Write-Warning "Preflight: $msg - the real run will stop here." }
            else { throw "Not enough disk space. $msg. Nothing was changed - free some space and re-run." }
        }
        'WARN' { Write-Warning "$msg - below 20 GB, free some space soon." }
        'UNKNOWN' { Write-Warning "Can't read free space for $($disk.Path) - check it by hand." }
        default { Write-Host "$msg - OK." -ForegroundColor Green }
    }
}

# -- 0. Server .NET runtime (Hosting Bundle) -------------------------------------------------------
# The app is framework-dependent: runtime security fixes (e.g. System.Security.Cryptography.Pkcs,
# v1.46.7) come from the server's Hosting Bundle, not the App folder. Require at least the
# Microsoft.AspNetCore.* package version this build references; install the latest patch if older.
if (-not $SkipRuntimeCheck) {
    $csproj = Get-Content (Join-Path $repoRoot 'EINVWORLD.csproj') -Raw
    $minRuntime = [regex]::Matches($csproj, 'Include="Microsoft\.AspNetCore\.[^"]+" Version="(\d+\.\d+\.\d+)"') |
        ForEach-Object { [version]$_.Groups[1].Value } | Sort-Object | Select-Object -Last 1
    $serverHost = if ($DestAppPath -like '\\*') { ($DestAppPath -split '\\')[2] } else { $null }
    $sharedDir = if ($serverHost) { "\\$serverHost\c$\Program Files\dotnet\shared\Microsoft.AspNetCore.App" }
                 else { Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.AspNetCore.App' }
    function Get-ServerRuntimes {
        if (-not (Test-Path $sharedDir)) { return @() }
        @(Get-ChildItem $sharedDir -Directory | ForEach-Object { $v = $null; if ([version]::TryParse($_.Name, [ref]$v)) { $v } })
    }

    Write-Host "Checking server ASP.NET Core runtime (need >= $minRuntime) ..." -ForegroundColor Cyan
    if ($serverHost -and -not (Test-Path "\\$serverHost\c$\Program Files")) {
        throw "Can't read \\$serverHost\c$ to check the server's .NET runtime. Check it on the server (dotnet --list-runtimes: Microsoft.AspNetCore.App >= $minRuntime), then re-run with -SkipRuntimeCheck."
    }
    $current = Get-ServerRuntimes | Where-Object { $_.Major -eq $minRuntime.Major -and $_.Minor -eq $minRuntime.Minor } |
        Sort-Object | Select-Object -Last 1
    if ($current -and $current -ge $minRuntime) {
        Write-Host "  Server has Microsoft.AspNetCore.App $current - OK." -ForegroundColor Green
    }
    else {
        $channel = "$($minRuntime.Major).$($minRuntime.Minor)"
        $feed = Invoke-RestMethod "https://builds.dotnet.microsoft.com/dotnet/release-metadata/$channel/releases.json" -UseBasicParsing
        $latest = $feed.releases | Where-Object { $_.'aspnetcore-runtime' } | Select-Object -First 1
        $bundle = $latest.'aspnetcore-runtime'.files | Where-Object { $_.name -eq 'dotnet-hosting-win.exe' }
        if (-not $bundle -or [version]$latest.'aspnetcore-runtime'.version -lt $minRuntime) {
            throw "No .NET $channel Hosting Bundle >= $minRuntime in Microsoft's release feed yet - install it on the server manually."
        }
        $target = $latest.'aspnetcore-runtime'.version
        Write-Host "  Server has $(if ($current) { $current } else { "no $channel runtime" }) - Hosting Bundle $target needed." -ForegroundColor Yellow

        if ($PSCmdlet.ShouldProcess($(if ($serverHost) { $serverHost } else { 'this machine' }), "install .NET Hosting Bundle $target")) {
            $exe = Join-Path $env:TEMP "dotnet-hosting-$target-win.exe"
            Invoke-WebRequest $bundle.url -OutFile $exe -UseBasicParsing
            if ((Get-FileHash $exe -Algorithm SHA512).Hash -ne $bundle.hash) {
                Remove-Item $exe -Force
                throw "Hosting Bundle download failed its SHA-512 check against Microsoft's release feed - not installed."
            }
            # Silent install; /norestart = never reboot the server. Exit 3010 = success, reboot pending.
            $install = { param($path) (Start-Process $path -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru).ExitCode }
            $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            if ($serverHost) {
                $serverExe = "C:\Windows\Temp\$(Split-Path $exe -Leaf)"
                $remoteExe = "\\$serverHost\c$\Windows\Temp\$(Split-Path $exe -Leaf)"
                Copy-Item $exe $remoteExe -Force
                $code = $null
                # Try 1: remoting with this login (works on a domain). Try 2: a server admin login.
                try { $code = Invoke-Command -ComputerName $serverHost -ScriptBlock $install -ArgumentList $serverExe }
                catch {
                    Write-Warning "Couldn't run the installer on $serverHost with your login (PowerShell remoting / WinRM): $($_.Exception.Message)"
                    if ((Read-Host "  Retry with a $serverHost administrator login? [Y/n]") -notmatch '^[Nn]') {
                        try {
                            # A non-domain server reached by IP needs NTLM, which WinRM only allows for hosts in this PC's TrustedHosts.
                            if ($isAdmin) {
                                Start-Service WinRM
                                $trusted = (Get-Item WSMan:\localhost\Client\TrustedHosts).Value
                                if ($trusted -ne '*' -and ($trusted -split ',' | ForEach-Object { $_.Trim() }) -notcontains $serverHost -and
                                    (Read-Host "  Add $serverHost to this PC's WinRM TrustedHosts (needed to sign in to a non-domain server)? [Y/n]") -notmatch '^[Nn]') {
                                    Set-Item WSMan:\localhost\Client\TrustedHosts -Value $serverHost -Concatenate -Force
                                }
                            }
                            else { Write-Warning "Not an elevated PowerShell, so $serverHost can't be added to this PC's WinRM TrustedHosts - this retry may fail the same way." }
                            $cred = Get-Credential -Message "Administrator login for $serverHost (e.g. $serverHost\Administrator)"
                            $code = Invoke-Command -ComputerName $serverHost -Credential $cred -ScriptBlock $install -ArgumentList $serverExe
                        }
                        catch { Write-Warning "That didn't work either: $($_.Exception.Message)" }
                    }
                }
                if ($null -eq $code) {
                    # Last resort: the operator runs it on the server while the deploy waits, then re-checks the runtime.
                    Write-Host "  Install it on $serverHost by hand: sign in there (e.g. Remote Desktop), open PowerShell as administrator and run:" -ForegroundColor Yellow
                    Write-Host "    Start-Process $serverExe -ArgumentList '/install','/quiet','/norestart' -Wait"
                    do {
                        if ((Read-Host "  Press Enter once it has finished (or type Q to stop the deploy)") -match '^[Qq]') {
                            throw "Deploy stopped - $serverHost still needs Hosting Bundle $target (installer left at $serverExe). Nothing deployed."
                        }
                        $now = Get-ServerRuntimes | Sort-Object | Select-Object -Last 1
                        if (-not $now -or $now -lt $minRuntime) { Write-Warning "$serverHost still has $now (< $minRuntime) - not installed yet." }
                    } until ($now -and $now -ge $minRuntime)
                    $code = 0
                }
                Remove-Item $remoteExe -Force -ErrorAction SilentlyContinue
            }
            else {
                if (-not $isAdmin) { throw "Installing the Hosting Bundle needs an elevated (Run as administrator) PowerShell." }
                $code = & $install $exe
            }
            Remove-Item $exe -Force -ErrorAction SilentlyContinue
            if ($code -notin 0, 3010) { throw "Hosting Bundle installer exited with $code - nothing deployed. Install it on the server manually." }
            $now = Get-ServerRuntimes | Sort-Object | Select-Object -Last 1
            if (-not $now -or $now -lt $minRuntime) { throw "Installer finished but the server still has $now (< $minRuntime) - nothing deployed." }
            Write-Host "  Hosting Bundle $target installed$(if ($code -eq 3010) { ' (Windows reports a reboot is pending - schedule one)' })." -ForegroundColor Green
            Write-Host "  This site picks up the new runtime when this deploy restarts it; other sites on the server do at their next app-pool recycle." -ForegroundColor Yellow
        }
        elseif ($WhatIfPreference -and $serverHost) {
            # Read-only: the same remoting call the real install makes, so -WhatIf flags a blocked WinRM up front.
            try {
                Invoke-Command -ComputerName $serverHost -ScriptBlock { $true } | Out-Null
                Write-Host "  Preflight: PowerShell remoting to $serverHost with your login - OK, the install can run unattended." -ForegroundColor Green
            }
            catch { Write-Warning "Preflight: PowerShell remoting to $serverHost failed with your login ($($_.Exception.Message)). The real run will ask for a $serverHost admin login, or for you to install it on the server by hand." }
        }
    }
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
    $dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
    # Gated on ShouldProcess: under -WhatIf, $WhatIfPreference leaks into dotnet-install.ps1, whose
    # write-access probe then "creates" its test file as a no-op and falsely reports no write access.
    if ($pinnedSdk -and -not $have -and $PSCmdlet.ShouldProcess($dotnetDir, "install .NET SDK $pinnedSdk (per-user)")) {
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
        # dotnet publish never deletes old files from -o (v1.50.19 shipped old draft invoices that way):
        # publish into an empty folder and record what it produced.
        Initialize-PublishFolder $SourcePublishPath $repoRoot
        Push-Location $repoRoot
        try {
            dotnet publish EINVWORLD.csproj -c Release -r win-x64 --self-contained false -o $SourcePublishPath
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
            Write-PublishStamp $SourcePublishPath
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

# Copy gate (also for -SkipPublish): exactly the clean publish's files, this repo's version, nothing forbidden.
if ($WhatIfPreference -and -not $SkipPublish) {
    Write-Host "Publish output check skipped (-WhatIf did not publish)." -ForegroundColor Yellow
}
else {
    Assert-PublishOutput $SourcePublishPath $repoRoot
    Write-Host "Publish output check: matches the clean publish, version $(Get-AppVersion (Join-Path $SourcePublishPath 'appsettings.json'))." -ForegroundColor Green
}
$forbiddenOnServer = Find-ForbiddenServerFiles $DestAppPath
if ($forbiddenOnServer.Count) {
    Write-Host "$($forbiddenOnServer.Count) file(s) on the server must not be public and will be removed after the copy (the backup keeps them):" -ForegroundColor Yellow
    $forbiddenOnServer | Select-Object -First 20 | ForEach-Object { Write-Host "    $_" }
}

# -- 2. Backup -----------------------------------------------------------------------------------
$backupPath = $null   # set only once a backup really exists - the rollback depends on it
$deployedVersion = 'unknown'
if (-not $SkipBackup) {
    $verFile = Join-Path $DestAppPath 'appsettings.json'
    if (Test-Path $verFile) {
        $match = (Get-Content $verFile -Raw) | Select-String '"Version":\s*"(v[\d.]+)"'
        if ($match) { $deployedVersion = $match.Matches[0].Groups[1].Value }
    }
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $plannedBackup = Join-Path (Split-Path $DestAppPath -Parent) "App_Backup_${deployedVersion}_$stamp"

    if ($PSCmdlet.ShouldProcess($plannedBackup, "Back up current App\ ($deployedVersion)")) {
        Write-Host "Backing up current App\ ($deployedVersion) to $plannedBackup ..." -ForegroundColor Cyan
        $rcArgs = @($DestAppPath, $plannedBackup, '/E', '/R:2', '/W:2', '/NFL', '/NDL', '/NJH')
        robocopy @rcArgs | Select-Object -Last 6
        # Robocopy exit codes 0-7 are all non-fatal ("files copied" etc); only 8+ means real trouble.
        if ($LASTEXITCODE -ge 8) { throw "Backup robocopy failed (exit code $LASTEXITCODE) - aborting before touching App\." }
        $backupPath = $plannedBackup
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
            if (-not $PSCmdlet.ShouldProcess($p.Db, $label)) {
                if ($WhatIfPreference) {
                    # Read-only: does the migration login hold what the real run needs (BACKUP first, then DDL)?
                    # Built in a variable first: written inline as a multi-line "a" + "b" inside the command call,
                    # PowerShell 5.1 passed each piece to sqlcmd as a separate argument ("Unexpected argument").
                    $permSql = "SET NOCOUNT ON; " +
                        "SELECT v.p FROM (VALUES ('BACKUP DATABASE'), ('CREATE TABLE'), ('ALTER ANY SCHEMA')) v(p) WHERE ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', v.p), 0) <> 1 " +
                        "UNION ALL SELECT 'INSERT on __EFMigrationsHistory' WHERE ISNULL(HAS_PERMS_BY_NAME(N'dbo.__EFMigrationsHistory', 'OBJECT', 'INSERT'), 0) <> 1"
                    $missingPerms = @(Invoke-Sqlcmd2 $p.Server $p.Db $p.Builder @('-Q', $permSql))
                    if ($missingPerms.Count) { Write-Warning "Preflight: [$($p.Db)] the migration login lacks $($missingPerms -join ', ') - the real run's backup/migration will fail. Use -MigrationCredential with a db_owner login." }
                    else { Write-Host "  Preflight: [$($p.Db)] migration login can back up and apply DDL - OK." -ForegroundColor Green }
                }
                continue
            }

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

# -- 3. Deploy (copy, never touch excluded files; remove only files the last deploy shipped) -------
Write-Host ""
Write-Host "Copying publish output into App\ (excluding: $($excludeFiles -join ', ')) ..." -ForegroundColor Cyan

$deployLogDir = Join-Path (Split-Path $DestAppPath -Parent) 'DeployLogs'
$copyLog = Join-Path $deployLogDir "deploy_$(Get-Date -Format 'yyyyMMdd_HHmmss')_robocopy.log"
$rcArgs = @($SourcePublishPath, $DestAppPath, '/E', '/R:3', "/W:$CopyRetryWaitSeconds", '/NFL', '/NDL', '/NJH', '/NP', '/TEE', "/LOG+:$copyLog", '/XF') + $excludeFiles

# Every file this release ships (the publish output minus server-owned files): what the copy is checked against,
# what the next deploy may remove, and what a rollback deletes when the backup lacks it.
$shipped = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
Get-RelativeFiles $SourcePublishPath | ForEach-Object {
    if ($excludeFiles -notcontains (Split-Path $_ -Leaf)) { [void]$shipped.Add($_) }
}

# Stops the app (app_offline.htm must already be in App\), restores the pre-deploy backup, verifies it and removes
# never-public files the backup may hold. Throws if any of that fails. $script:restoreStarted tells the caller whether
# App\ was touched: if the app never stopped, nothing was restored and App\ is as it was before the rollback.
# Known limit: files the OLD app changed under App\ between the backup and app_offline.htm revert to the backup's copy
# (documents, logs and keys live outside App\, so in practice only never-public leftovers and ANCM stdout logs).
$script:restoreStarted = $false
function Invoke-AppRollback {
    $w = Wait-AppFilesUnlocked -AppPath $DestAppPath -TimeoutSeconds $ShutdownTimeoutSeconds
    if (-not $w.Unlocked) { throw "the app still holds $($w.Locked -join ', ') after $($w.Seconds) s" }
    $script:restoreStarted = $true
    Write-Host "Rolling back: restoring $backupPath into App\ ..." -ForegroundColor Yellow
    Restore-AppFromBackup $backupPath $DestAppPath @($shipped) $CopyRetryWaitSeconds $copyLog
    foreach ($rel in Find-ForbiddenServerFiles $DestAppPath) { Remove-Item -LiteralPath (Join-Path $DestAppPath $rel) -Force }
    Write-Host "Rollback verified: App\ matches $backupPath ($deployedVersion)." -ForegroundColor Yellow
}

# Settings files the server has that differ from this build are replaced (the backup keeps the old copy).
foreach ($name in 'appsettings.json', 'appsettings.Production.json', 'appsettings.Staging.json') {
    $src = Join-Path $SourcePublishPath $name
    $dst = Join-Path $DestAppPath $name
    if ($excludeFiles -notcontains $name -and (Test-Path $src) -and (Test-Path $dst) -and
        [IO.File]::ReadAllText($src) -cne [IO.File]::ReadAllText($dst)) {   # not Get-FileHash: it returns nothing under -WhatIf on PS 5.1
        Write-Host "  $name on the server differs from this build and will be replaced (old copy is in the backup)." -ForegroundColor Yellow
    }
}
if ($WhatIfPreference) { $rcArgs += '/L' }

# app_offline.htm makes the ASP.NET Core Module stop the app and release its DLL locks, so no manual
# site stop is needed. Dropped AFTER the backup (so a restored backup never ships it). Removed once App\ is
# consistent again - the new build verified, nothing copied, or the backup restored - and only then: if a
# rollback is impossible or fails, it stays, so a mixed build is never served.
$offlineFile = Join-Path $DestAppPath 'app_offline.htm'
$deployed = $false
if ($PSCmdlet.ShouldProcess($DestAppPath, 'Copy publish output (excluding server-only files)')) {
    New-Item -ItemType Directory -Force $deployLogDir | Out-Null
    # Maintenance page (scripts\app_offline.htm, self-contained): it shows when the update started and the
    # expected finish, and reloads itself once the site answers again.
    $offlineTemplate = Join-Path $PSScriptRoot 'app_offline.htm'
    $offlineHtml = '<html><body><h2>EINVWORLD is being updated - back in a minute.</h2></body></html>'
    if (Test-Path $offlineTemplate) {
        $offlineStart = [DateTimeOffset]::Now
        $offlineHtml = (Get-Content -Path $offlineTemplate -Raw -Encoding UTF8).
            Replace('{{START}}', $offlineStart.ToString('o')).
            Replace('{{END}}', $offlineStart.AddMinutes(5).ToString('o')).
            Replace('{{REF}}', 'DEPLOY-' + $offlineStart.ToString('yyyyMMdd-HHmm'))
    }
    Set-Content -Path $offlineFile -Value $offlineHtml -Encoding utf8
    $keepOffline = $false
    try {
        # Never copy while the app runs: wait (bounded) until it has released every binary in App\.
        Write-Host "Waiting for the app to stop (up to $ShutdownTimeoutSeconds s) ..." -ForegroundColor Cyan
        $wait = Wait-AppFilesUnlocked -AppPath $DestAppPath -TimeoutSeconds $ShutdownTimeoutSeconds
        if (-not $wait.Unlocked) {
            throw "STOPPED BEFORE COPY: after $($wait.Seconds) s these files are still held open (or can't be written): $($wait.Locked -join ', '). Nothing was copied; the previous version comes back online. Check the app pool on the server (a hung request or worker), then re-run, or raise -ShutdownTimeoutSeconds."
        }
        Write-Host "App stopped after $($wait.Seconds) s: no .dll/.exe in App\ is held open." -ForegroundColor Green

        try {
            robocopy @rcArgs | Select-Object -Last 8
            if ($LASTEXITCODE -ge 8) { throw "Deploy robocopy failed (exit code $LASTEXITCODE) - errors are in $copyLog" }
            Write-Host "Copy complete." -ForegroundColor Green

            # Remove files the PREVIOUS deploy shipped that this release no longer has. Driven only by the
            # manifest this script wrote last time, so anything it never shipped (uploads under wwwroot,
            # server-only files, the backup) is never touched. The pre-deploy backup still has every file.
            $manifestPath = Join-Path $DestAppPath '.deploy-manifest.txt'
            if (Test-Path $manifestPath) {
                $stale = @(Get-Content $manifestPath | Where-Object {
                    $_ -and -not $shipped.Contains($_) -and $_ -notmatch '\.\.' -and -not [IO.Path]::IsPathRooted($_) -and
                    $excludeFiles -notcontains (Split-Path $_ -Leaf)
                })
                $removed = 0
                foreach ($rel in $stale) {
                    $full = Join-Path $DestAppPath $rel
                    if (Test-Path -LiteralPath $full -PathType Leaf) {
                        Remove-Item -LiteralPath $full -Force
                        Write-Host "  removed (no longer in this release): $rel"
                        $removed++
                    }
                }
                Write-Host "Stale-file clean-up: removed $removed file(s) dropped since the last deploy." -ForegroundColor Green
            }
            else {
                # First run with this script version: no manifest yet, so delete nothing; list what a human should review.
                $unshipped = @(Get-RelativeFiles (Join-Path $DestAppPath 'wwwroot') | ForEach-Object { "wwwroot\$_" } |
                    Where-Object { -not $shipped.Contains($_) })
                Write-Host "No deploy manifest yet - nothing removed this time (the next deploy cleans up automatically)." -ForegroundColor Yellow
                if ($unshipped.Count) {
                    Write-Host "  $($unshipped.Count) file(s) under wwwroot are not in this release - review, and delete by hand only if they are old build files (NOT uploads):" -ForegroundColor Yellow
                    $unshipped | Select-Object -First 40 | ForEach-Object { Write-Host "    $_" }
                }
            }
            Set-Content -Path $manifestPath -Value ($shipped | Sort-Object) -Encoding utf8

            # Never-public files this script did not ship (an old build or a hand copy left them, so the manifest
            # can't remove them - Production's Drafts\ and tempcsv\). The pre-deploy backup keeps a copy.
            foreach ($rel in Find-ForbiddenServerFiles $DestAppPath) {
                Remove-Item -LiteralPath (Join-Path $DestAppPath $rel) -Force
                Write-Host "  removed (must not be public): $rel"
            }
            $left = Find-ForbiddenServerFiles $DestAppPath
            if ($left.Count) { throw "These files must not be public and are still on the server: $($left -join ', ')" }

            # Integrity: every shipped file must now be exactly the build's (start-up files by content).
            $bad = @(Compare-DeployedFiles $SourcePublishPath $DestAppPath @($shipped))
            if ($bad.Count) { throw "$($bad.Count) deployed file(s) don't match the build, e.g. $(($bad | Select-Object -First 5) -join ', ')" }
            Write-Host "Integrity check: all $($shipped.Count) shipped file(s) match the build." -ForegroundColor Green
            $deployed = $true
        }
        catch {
            $failure = $_.Exception.Message
            Write-Host "DEPLOY FAILED during the copy: $failure" -ForegroundColor Red
            if (-not $backupPath) {
                $keepOffline = $true
                throw "DEPLOY FAILED and there is no backup to roll back to (-SkipBackup): App\ may hold a mixed build, so the site is LEFT OFFLINE (app_offline.htm). Restore App\ by hand, then delete $offlineFile. Cause: $failure"
            }
            try { Invoke-AppRollback }
            catch {
                $keepOffline = $true
                throw "DEPLOY FAILED and the ROLLBACK FAILED ($($_.Exception.Message)): the site is LEFT OFFLINE (app_offline.htm). Restore $backupPath into App\ by hand (never web.config), then delete $offlineFile. Cause: $failure"
            }
            throw "DEPLOY FAILED and was ROLLED BACK to $deployedVersion ($backupPath, verified). Cause: $failure. Robocopy log: $copyLog"
        }
    }
    finally {
        if ($keepOffline) { Write-Host "Site LEFT OFFLINE: $offlineFile stays until App\ is restored." -ForegroundColor Red }
        else {
            Remove-Item -Path $offlineFile -Force -ErrorAction SilentlyContinue
            Write-Host "Site back online (app_offline.htm removed)." -ForegroundColor Green
        }
    }
}

# -- 4. Health check through the public hostname (the first request starts the new build) ----------------
# Runs before the security check: an app that won't start answers 5xx everywhere, which must roll back, not
# read as an unverifiable security check.
if (-not $PublicBaseUrl) { $PublicBaseUrl = Get-DefaultPublicBaseUrl $DestAppPath }
if ($PublicBaseUrl) {
    $health = @(Test-HealthUrls -BaseUrl $PublicBaseUrl -DelayMs $HealthRetryDelayMs)
    $health | ForEach-Object { Write-Host ("  {0,3} after {1} attempt(s)  {2}" -f $_.Status, $_.Attempts, $_.Url) }
    $unhealthy = @($health | Where-Object { -not $_.Healthy })
    if ($unhealthy.Count -and $deployed -and -not @($unhealthy | Where-Object { $_.Status -ne 0 }).Count) {
        # No answer at all (000): this PC's network, the tunnel, or a first start longer than curl's timeout. That
        # proves nothing about the build, so the verified new build is kept rather than rolled back.
        throw "HEALTH UNVERIFIED: no answer from $PublicBaseUrl after the deploy, so the integrity-verified new build was KEPT (not rolled back). Check this PC's connection and the site, then re-run the health and security checks (DEPLOY-NOTES.md); roll back from $(if ($backupPath) { $backupPath } else { 'your backup' }) if the site is really down."
    }
    if ($unhealthy.Count) {
        if (-not ($deployed -and $backupPath)) {
            throw "HEALTH CHECK FAILED: the site is not healthy$(if ($deployed) { ' and there is no backup to roll back to (-SkipBackup)' }). Check SystemLogs / the stdout log. Local build output kept for diagnosis."
        }
        Write-Host "HEALTH CHECK FAILED after the deploy - rolling back to $deployedVersion." -ForegroundColor Red
        Set-Content -Path $offlineFile -Value $offlineHtml -Encoding utf8
        try { Invoke-AppRollback }
        catch {
            if (-not $script:restoreStarted) {
                # The app never stopped, so nothing was restored: App\ still holds the verified new build.
                Remove-Item -Path $offlineFile -Force -ErrorAction SilentlyContinue
                throw "HEALTH CHECK FAILED and the rollback could not start ($($_.Exception.Message)): nothing was restored, the verified new build is back online. Check the server, then roll back from $backupPath by hand if needed."
            }
            throw "HEALTH CHECK FAILED and the ROLLBACK FAILED ($($_.Exception.Message)): the site is LEFT OFFLINE (app_offline.htm). Restore $backupPath into App\ by hand (never web.config), then delete $offlineFile."
        }
        Remove-Item -Path $offlineFile -Force -ErrorAction SilentlyContinue
        $after = @(Test-HealthUrls -BaseUrl $PublicBaseUrl -DelayMs $HealthRetryDelayMs)
        $state = if (@($after | Where-Object { -not $_.Healthy }).Count) { 'is STILL UNHEALTHY - check the server' } else { 'is healthy again' }
        throw "HEALTH CHECK FAILED: the new build was ROLLED BACK to $deployedVersion ($backupPath, verified) and the site $state. Local build output kept for diagnosis."
    }
    Write-Host "Health check: PASS." -ForegroundColor Green

    # -- 5. Public security verification (through the real hostname, so a Cloudflare-cached copy counts) -----
    $checkPaths = @(Get-KnownNeverPublicPaths) + @($forbiddenOnServer)
    Write-Host ""
    Write-Host "Public security check: never-public paths through $PublicBaseUrl (status and cache only, never content) ..." -ForegroundColor Cyan
    $results = @(Test-NeverPublicUrls -BaseUrl $PublicBaseUrl -RelativePaths $checkPaths)
    $results | ForEach-Object { Write-Host ("  {0,-10} {1,3} {2,-8} {3}" -f $_.Verdict, $_.Status, $(if ($_.Cache) { $_.Cache } else { '-' }), $_.Url) }
    $bad = @($results | Where-Object { $_.Verdict -ne 'PASS' })
    if ($bad.Count) {
        throw "SECURITY VERIFICATION FAILED: $($bad.Count) of $($results.Count) never-public path(s) are served, cached at Cloudflare, or could not be verified. The app files ARE deployed. Remove the files from the server, purge the Cloudflare cache, then re-run the check: . .\scripts\DeployGuards.ps1; Test-NeverPublicUrls -BaseUrl $PublicBaseUrl -RelativePaths (Get-KnownNeverPublicPaths)"
    }
    Write-Host "Public security check: PASS ($($results.Count) path(s), none served or cached)." -ForegroundColor Green
}
else {
    Write-Warning "No public hostname for $DestAppPath - post-deploy public security and health checks skipped (pass -PublicBaseUrl to run them)."
}

# -- 6. Local clean-up: only now that the deploy, the public security check and the health check all passed ----
# A failed run stops before this, so its build output stays for diagnosis. Only folders inside the repo's bin\
# and obj\ are deleted (Remove-DeployArtifacts refuses anything else); the NuGet cache and backups are never touched.
if ($WhatIfPreference) { Write-Host "Local clean-up skipped (-WhatIf)." -ForegroundColor Yellow }
elseif (-not $PublicBaseUrl) { Write-Host "Local clean-up skipped: the public checks did not run." -ForegroundColor Yellow }
elseif ($KeepPublishOutput) { Write-Host "Publish output kept (-KeepPublishOutput): $SourcePublishPath" -ForegroundColor Yellow }
else {
    $ridObj = @(Get-ChildItem (Join-Path $repoRoot 'obj\Release') -Directory -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'win-x64' })
    try {
        foreach ($r in @(Remove-DeployArtifacts (@($SourcePublishPath) + $ridObj) $repoRoot)) {
            switch ($r.Result) {
                'REMOVED' { Write-Host ("  removed {0} ({1:N0} MB)" -f $r.Path, ($r.Bytes / 1MB)) }
                'REFUSED' { Write-Host "  kept $($r.Path) (outside the repo's bin\/obj\, or holds a link)" -ForegroundColor Yellow }
            }
        }
        Write-Host "Local clean-up done; the next deploy publishes into a fresh folder." -ForegroundColor Green
    }
    catch { Write-Warning "Local clean-up failed (the deploy itself succeeded): $($_.Exception.Message)" }
}

Write-Host ""
Write-Host "=== Done. Remaining manual steps ===" -ForegroundColor Cyan
if ($SkipMigrations) {
    Write-Host "  - Migrations were skipped: apply any pending Migrations\Apply_*.sql yourself (DEPLOY-NOTES.md)." -ForegroundColor Yellow
}
Write-Host "  - Verify: GET /health and /health/ready both return 200; sign in; open an invoice." -ForegroundColor Yellow
