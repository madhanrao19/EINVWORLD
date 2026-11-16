<#
    Stale-file guards for Deploy-Staging.ps1 (dot-sourced; tested by EINVWORLD.Tests\DeployGuardsTests.cs).

    v1.50.19 shipped 2024 draft invoices (with an NRIC) to Staging because `dotnet publish` never empties its
    output folder, so files dropped from the build kept shipping. These guards make that fail closed:
      - the script only publishes into an empty folder, and records what that clean publish produced;
      - nothing is copied unless the folder still holds exactly that file list and this repo's version;
      - files that must never be public are refused at the source and removed from the server.
    Pure ASCII on purpose: PowerShell 5.1 misreads UTF-8 without a BOM.
#>

# Paths (under App\) that must never be served. Single source of truth: the <NeverPublicPath> items in
# EINVWORLD.csproj, which publish also excludes. Read as MSBuild globs, used here as -like patterns.
function Get-NeverPublicGlobs([string]$csprojPath) {
    $xml = [xml][IO.File]::ReadAllText($csprojPath)
    $globs = @($xml.SelectNodes('//*[local-name()="NeverPublicPath"]') | ForEach-Object { $_.GetAttribute('Include') } | Where-Object { $_ })
    if (-not $globs.Count) { throw "No <NeverPublicPath> items in $csprojPath - refusing to run without the never-public list." }
    $globs
}
# MSBuild glob -> -like pattern; the trailing * also covers the .br/.gz copies publish generates.
function ConvertTo-LikePattern([string]$glob) {
    $p = $glob.Replace('\**', '\*')
    if ($p.EndsWith('*')) { $p } else { "$p*" }
}
$NeverPublicGlobs = Get-NeverPublicGlobs (Join-Path $PSScriptRoot '..\EINVWORLD.csproj')
$ForbiddenPublishPatterns = @($NeverPublicGlobs | ForEach-Object { ConvertTo-LikePattern $_ })

# Written into the publish folder right after a clean publish; never copied to the server.
$PublishStampName = '.deploy-publish-files.txt'

# .NET enumeration: child paths always start with the root's own FullName (Get-ChildItem can return long
# names under a short 8.3 root, which would break the relative paths).
function Get-RelativeFiles([string]$root) {
    $dir = New-Object IO.DirectoryInfo $root
    if (-not $dir.Exists) { return @() }
    $prefix = $dir.FullName.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1
    $dir.GetFiles('*', [IO.SearchOption]::AllDirectories) | ForEach-Object { $_.FullName.Substring($prefix) }
}

function Find-ForbiddenFiles([string[]]$relativePaths) {
    @($relativePaths | Where-Object { $p = $_; $ForbiddenPublishPatterns | Where-Object { $p -like $_ } })
}

# Empties the publish folder before a publish. Only folders inside the repo's bin\ are deleted; any other
# non-empty folder is refused, so a mistyped -SourcePublishPath can never wipe or reuse anything.
function Initialize-PublishFolder([string]$path, [string]$repoRoot) {
    $full = [IO.Path]::GetFullPath($path)
    if (-not (Test-Path -LiteralPath $full)) { return }
    $bin = [IO.Path]::GetFullPath((Join-Path $repoRoot 'bin')).TrimEnd('\') + '\'
    if ($full.StartsWith($bin, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $full -Recurse -Force
    }
    elseif (@(Get-RelativeFiles $full).Count) {
        throw "Publish folder $full is outside the repo's bin\ and not empty. Publish never deletes old files, so they would ship too. Pass an empty or new -SourcePublishPath."
    }
}

function Write-PublishStamp([string]$publishRoot) {
    $files = @(Get-RelativeFiles $publishRoot | Where-Object { $_ -ne $PublishStampName } | Sort-Object)
    Set-Content -LiteralPath (Join-Path $publishRoot $PublishStampName) -Value $files -Encoding utf8
}

function Get-AppVersion([string]$appsettingsPath) {
    if (-not (Test-Path -LiteralPath $appsettingsPath)) { return $null }
    $m = [regex]::Match([IO.File]::ReadAllText($appsettingsPath), '"Version":\s*"([^"]+)"')
    if ($m.Success) { $m.Groups[1].Value } else { $null }
}

# The copy gate: throws unless the publish folder is exactly what this script's last clean publish produced,
# for this repo's version, with nothing that must not be public.
function Assert-PublishOutput([string]$publishRoot, [string]$repoRoot) {
    $stampPath = Join-Path $publishRoot $PublishStampName
    if (-not (Test-Path -LiteralPath $stampPath)) {
        throw "$publishRoot has no $PublishStampName, so it was not produced by this script's clean publish (old files may be mixed in). Re-run without -SkipPublish."
    }
    $expected = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    Get-Content -LiteralPath $stampPath | Where-Object { $_ } | ForEach-Object { [void]$expected.Add($_) }
    $actual = @(Get-RelativeFiles $publishRoot | Where-Object { $_ -ne $PublishStampName })

    $extra = @($actual | Where-Object { -not $expected.Contains($_) })
    if ($extra.Count) {
        throw "Publish folder has $($extra.Count) file(s) the clean publish did not produce, e.g. $(($extra | Select-Object -First 5) -join ', '). Re-run without -SkipPublish."
    }
    if ($actual.Count -ne $expected.Count) {
        throw "Publish folder is missing $($expected.Count - $actual.Count) file(s) the clean publish produced. Re-run without -SkipPublish."
    }
    $forbidden = Find-ForbiddenFiles $actual
    if ($forbidden.Count) {
        throw "Publish output contains files that must never be public: $(($forbidden | Select-Object -First 5) -join ', '). Check the <Content Remove> entries in EINVWORLD.csproj."
    }
    $built = Get-AppVersion (Join-Path $publishRoot 'appsettings.json')
    $repo = Get-AppVersion (Join-Path $repoRoot 'appsettings.json')
    if ($built -ne $repo) {
        throw "Publish output is $built but the repo is $repo - an old build. Re-run without -SkipPublish."
    }
}

# Files matching the forbidden patterns on the server, whoever put them there (an old build, or by hand).
# Only the folders the patterns name are read (recursively for "**"), never the whole upload tree.
function Find-ForbiddenServerFiles([string]$appPath) {
    $found = @()
    foreach ($glob in $NeverPublicGlobs) {
        $segments = $glob -split '\\'
        $static = @($segments | ForEach-Object -Begin { $stop = $false } -Process { if ($_ -match '[*?]') { $stop = $true }; if (-not $stop) { $_ } })
        $dirRel = ($static | Select-Object -First ([Math]::Min($static.Count, $segments.Count - 1))) -join '\'
        $dir = Join-Path $appPath $dirRel
        if (-not (Test-Path -LiteralPath $dir -PathType Container)) { continue }
        if ($glob.Contains('**')) {
            $found += @(Get-RelativeFiles $dir | ForEach-Object { "$dirRel\$_" })
        } else {
            $found += @([IO.Directory]::GetFiles($dir) | ForEach-Object { "$dirRel\" + [IO.Path]::GetFileName($_) })
        }
    }
    Find-ForbiddenFiles @($found | Sort-Object -Unique)
}

# Known leaked paths (scripts\never-public-known-paths.txt), as App\-relative paths with backslashes.
function Get-KnownNeverPublicPaths([string]$listPath = (Join-Path $PSScriptRoot 'never-public-known-paths.txt')) {
    @(Get-Content -LiteralPath $listPath | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { $_.Trim().Replace('/', '\') })
}

# Default HTTP probe: status code and Cloudflare cache status only. The body goes to NUL and is never read or logged.
# No cache-busting headers: the point is to see what a stranger gets, including a copy cached at the edge.
$InvokeStatusProbe = {
    param([string]$url)
    $out = & curl.exe -s -m 10 -o NUL -w '%{http_code}|%header{cf-cache-status}' $url 2>$null
    if (-not $out) { $out = '000|' }
    $parts = "$out".Split('|')
    [pscustomobject]@{ Status = [int]$parts[0]; Cache = $parts[1].Trim().ToUpperInvariant() }
}

# One probe result -> PASS / FAIL / RETRY. FAIL: any 2xx (content), or any Cloudflare cache hit (a stored copy).
# RETRY: no answer (000) or a server error (5xx), which say nothing about the file.
function Get-PublicCheckVerdict([int]$status, [string]$cache) {
    if ($status -ge 200 -and $status -lt 300) { return 'FAIL' }
    if ($cache -in 'HIT', 'STALE', 'UPDATING', 'REVALIDATED') { return 'FAIL' }
    if ($status -eq 0 -or $status -ge 500) { return 'RETRY' }
    'PASS'
}

# Post-deploy public security verification: requests every never-public path through the real public hostname,
# spaced out, retrying no-answers. Returns one row per path; any FAIL - or a path still unanswered after the
# retries (UNVERIFIED) - fails the verification. Logs status, cache status and verdict only, never content.
function Test-NeverPublicUrls {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string[]]$RelativePaths,
        [scriptblock]$Probe = $InvokeStatusProbe,
        [int]$DelayMs = 1500,
        [int]$MaxAttempts = 4
    )
    $paths = @($RelativePaths | Where-Object { $_ -notmatch '\.(br|gz)$' } | Sort-Object -Unique)
    foreach ($rel in $paths) {
        $url = $BaseUrl.TrimEnd('/') + '/' + (($rel -replace '^wwwroot\\', '') -replace '\\', '/')
        $verdict = 'UNVERIFIED'; $status = 0; $cache = ''
        for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
            if ($DelayMs -gt 0) { Start-Sleep -Milliseconds ($DelayMs * $attempt) }   # spaced, and backs off on retries
            $r = & $Probe $url
            $status = [int]$r.Status; $cache = [string]$r.Cache
            $v = Get-PublicCheckVerdict $status $cache
            if ($v -ne 'RETRY') { $verdict = $v; break }
        }
        [pscustomobject]@{ Path = $rel; Url = $url; Status = $status; Cache = $cache; Attempts = [Math]::Min($attempt, $MaxAttempts); Verdict = $verdict }
    }
}

# Post-deploy health check: GET /health/live and /health/ready through the public site until each answers 200.
# The app starts on the first request after the copy, so a slow or failed first answer is retried with growing gaps.
function Test-HealthUrls {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [scriptblock]$Probe = $InvokeStatusProbe,
        [int]$DelayMs = 3000,
        [int]$MaxAttempts = 6
    )
    foreach ($path in '/health/live', '/health/ready') {
        $url = $BaseUrl.TrimEnd('/') + $path
        $status = 0
        for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
            $status = [int](& $Probe $url).Status
            if ($status -eq 200 -or $attempt -eq $MaxAttempts) { break }
            if ($DelayMs -gt 0) { Start-Sleep -Milliseconds ($DelayMs * $attempt) }
        }
        [pscustomobject]@{ Url = $url; Status = $status; Attempts = $attempt; Healthy = ($status -eq 200) }
    }
}

# Free bytes on the volume holding $path, local or UNC ([IO.DriveInfo] can't read shares). The nearest existing
# parent is used, since the publish folder may not exist yet. $null when it can't be read.
if (-not ('DeployGuards.Disk' -as [type])) {
    Add-Type -Namespace DeployGuards -Name Disk -MemberDefinition '[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool GetDiskFreeSpaceEx(string dir, out ulong freeToCaller, out ulong total, out ulong totalFree);'
}
function Get-FreeSpaceBytes([string]$path) {
    $dir = [IO.Path]::GetFullPath($path)
    while ($dir -and -not [IO.Directory]::Exists($dir)) { $dir = [IO.Path]::GetDirectoryName($dir) }
    $free = [uint64]0; $total = [uint64]0; $totalFree = [uint64]0
    if (-not $dir -or -not [DeployGuards.Disk]::GetDiskFreeSpaceEx($dir, [ref]$free, [ref]$total, [ref]$totalFree)) { return $null }
    [int64]$free
}

# BLOCK below what the step needs, WARN below 20 GB, UNKNOWN if free space could not be read.
$DiskWarnBytes = 20GB
function Get-DiskSpaceVerdict($freeBytes, [int64]$neededBytes) {
    if ($null -eq $freeBytes) { return 'UNKNOWN' }
    if ([int64]$freeBytes -lt $neededBytes) { return 'BLOCK' }
    if ([int64]$freeBytes -lt $DiskWarnBytes) { return 'WARN' }
    'OK'
}

# Deletes local build output after a fully verified deploy. Only folders strictly inside the repo's bin\ or obj\
# are deleted; anything else (a custom -SourcePublishPath, bin\ itself, a folder holding a junction, whose target
# Remove-Item -Recurse would follow on PowerShell 5.1) is REFUSED and kept. One row per path.
function Remove-DeployArtifacts([string[]]$paths, [string]$repoRoot) {
    $roots = foreach ($d in 'bin', 'obj') { [IO.Path]::GetFullPath((Join-Path $repoRoot $d)).TrimEnd('\') + '\' }
    foreach ($p in $paths) {
        $full = [IO.Path]::GetFullPath($p).TrimEnd('\')
        $result = 'REMOVED'; $bytes = [int64]0
        if (-not @($roots | Where-Object { $full.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count) { $result = 'REFUSED' }
        elseif (-not (Test-Path -LiteralPath $full -PathType Container)) { $result = 'ABSENT' }
        else {
            $dir = New-Object IO.DirectoryInfo $full
            if (($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
                @($dir.GetDirectories('*', [IO.SearchOption]::AllDirectories) | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) {
                $result = 'REFUSED'
            }
            else {
                $bytes = [int64](($dir.GetFiles('*', [IO.SearchOption]::AllDirectories) | Measure-Object Length -Sum).Sum)
                Remove-Item -LiteralPath $full -Recurse -Force
            }
        }
        [pscustomobject]@{ Path = $full; Result = $result; Bytes = $bytes }
    }
}

# ---- Copy safety: wait for the app to stop, verify the copy, roll back (2026-11-08) -------------------------------
# On 2026-11-08 a fixed 5 s pause after app_offline.htm was too short: the app still held EINVWORLD.dll, robocopy
# failed on it, and the site came back online with the old DLL and every other file new (a mixed build).

# True when another process holds the file open (or it can't be opened for writing). A missing file is not locked.
function Test-FileLocked([string]$path) {
    try { $fs = [IO.File]::Open($path, 'Open', 'ReadWrite', 'None'); $fs.Close(); $false }
    catch [IO.FileNotFoundException] { $false }
    catch [IO.DirectoryNotFoundException] { $false }
    catch { $true }
}

# app_offline.htm makes the ASP.NET Core Module stop the app; its worker process keeps every loaded .dll/.exe under
# App\ open until it has exited. Polls until none is held, or the timeout. These are this App folder's own files,
# so another site's process can't hold them: unlocked means THIS app has stopped. EINVWORLD.dll is polled alone
# first (cheap over a share), then every binary once. Returns Unlocked, Seconds and up to 10 still-open files.
function Wait-AppFilesUnlocked {
    param(
        [Parameter(Mandatory)][string]$AppPath,
        [int]$TimeoutSeconds = 120,
        [int]$PollMs = 2000
    )
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $bins = @(Get-RelativeFiles $AppPath | Where-Object { $_ -match '\.(dll|exe)$' })
    $locked = @(); $clear = 0
    while ($true) {
        $locked = @('EINVWORLD.dll' | Where-Object { Test-FileLocked (Join-Path $AppPath $_) })
        if (-not $locked.Count) { $locked = @($bins | Where-Object { Test-FileLocked (Join-Path $AppPath $_) } | Select-Object -First 10) }
        # Two clear polls in a row: one could fall in the moment before a starting worker loads its DLLs.
        $clear = if ($locked.Count) { 0 } else { $clear + 1 }
        if ($clear -ge 2 -or $sw.Elapsed.TotalSeconds -ge $TimeoutSeconds) { break }
        Start-Sleep -Milliseconds $PollMs
    }
    [pscustomobject]@{ Unlocked = (-not $locked.Count); Seconds = [int]$sw.Elapsed.TotalSeconds; Locked = $locked }
}

# Files under $dest that don't match $source: missing, different size or timestamp (robocopy keeps timestamps), and
# for the start-up files, different content. Returns the relative paths that differ.
function Compare-DeployedFiles([string]$source, [string]$dest, [string[]]$relativePaths) {
    $critical = 'EINVWORLD.dll', 'EINVWORLD.exe', 'EINVWORLD.deps.json', 'EINVWORLD.runtimeconfig.json'
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        foreach ($rel in $relativePaths) {
            $s = New-Object IO.FileInfo (Join-Path $source $rel)
            $d = New-Object IO.FileInfo (Join-Path $dest $rel)
            if (-not $d.Exists -or $d.Length -ne $s.Length -or $d.LastWriteTimeUtc -ne $s.LastWriteTimeUtc) { $rel; continue }
            if ($critical -contains $rel) {
                $a = [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($s.FullName)))
                $b = [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($d.FullName)))
                if ($a -ne $b) { $rel }
            }
        }
    }
    finally { $sha.Dispose() }
}

# The pre-deploy backup copies web.config (every server secret) and, if the key ring was left inside App\, the
# DataProtection keys. A copy inherits its new parent's permissions, which let any local user read it, so both are
# cut down to Administrators and SYSTEM (by SID: the names are localised). The rollback never reads either.
function Protect-BackupSecrets([string]$backupPath) {
    if (-not (Test-Path -LiteralPath $backupPath)) { return }
    # Every web.config, not only the top one: servers have held full copies with secrets in App\temp\.
    $targets = @(Get-ChildItem -LiteralPath $backupPath -Recurse -File -Filter 'web.config*' -Force | ForEach-Object FullName) +
               @(Join-Path $backupPath 'DataProtectionKeys')
    foreach ($p in $targets) {
        if (-not (Test-Path -LiteralPath $p)) { continue }
        # Children before their folder: once the folder is closed, a non-elevated owner can no longer list it.
        $items = @(Get-ChildItem -LiteralPath $p -Recurse -Force | Sort-Object { $_.FullName.Length } -Descending) + (Get-Item -LiteralPath $p -Force)
        foreach ($item in $items) {
            $flags = if ($item.PSIsContainer) { '(OI)(CI)F' } else { 'F' }
            $out = icacls $item.FullName /inheritance:r /grant:r "*S-1-5-32-544:$flags" "*S-1-5-18:$flags" 2>&1
            if ($LASTEXITCODE -ne 0) { throw "Could not restrict $($item.FullName) to Administrators and SYSTEM: $out" }
            # /grant:r keeps explicit entries for anyone else (e.g. a file's creator): remove those too.
            $others = @($item.GetAccessControl('Access').GetAccessRules($true, $false, [Security.Principal.SecurityIdentifier]) |
                ForEach-Object { $_.IdentityReference.Value } | Where-Object { $_ -notin 'S-1-5-32-544', 'S-1-5-18' } | Select-Object -Unique)
            foreach ($sid in $others) {
                $out = icacls $item.FullName /remove:g "*$sid" /remove:d "*$sid" 2>&1
                if ($LASTEXITCODE -ne 0) { throw "Could not remove $sid from $($item.FullName): $out" }
            }
        }
    }
}

# Rollback: undoes what a deploy changed in App\, from the pre-deploy backup. A deploy writes only this release's
# files and the manifest, and deletes only files the previous manifest listed - so exactly those are put back as the
# backup has them, and the release's files the backup lacks are deleted. Everything else (IIS stdout logs written
# since the backup, uploads, server-only files, web.config) is left alone, so a rollback never loses newer data.
# The caller must have stopped the app (app_offline.htm + Wait-AppFilesUnlocked). Throws unless App\ then matches.
function Restore-AppFromBackup([string]$backupPath, [string]$appPath, [string[]]$shippedRelative, [int]$retryWaitSeconds = 5, [string]$logPath,
                               [string[]]$skip = @('web.config', 'app_offline.htm')) {   # the deploy's copy exclusions
    $manifest = '.deploy-manifest.txt'
    $inBackup = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    Get-RelativeFiles $backupPath | ForEach-Object { [void]$inBackup.Add($_) }
    $written = @(@($shippedRelative) + $manifest)
    $previous = @(if ($inBackup.Contains($manifest)) { Get-Content -LiteralPath (Join-Path $backupPath $manifest) })
    $touched = @(($written + $previous) | Where-Object {
        $_ -and $_ -notmatch '\.\.' -and -not [IO.Path]::IsPathRooted($_) -and $skip -notcontains (Split-Path $_ -Leaf)
    } | Sort-Object -Unique)
    $restore = @($touched | Where-Object { $inBackup.Contains($_) })
    foreach ($rel in $restore) {
        $dst = Join-Path $appPath $rel
        # Unchanged since the backup (the copy never got to it): leave it, as robocopy would. The check below hashes the start-up files.
        $s = New-Object IO.FileInfo (Join-Path $backupPath $rel); $d = New-Object IO.FileInfo $dst
        if ($d.Exists -and $d.Length -eq $s.Length -and $d.LastWriteTimeUtc -eq $s.LastWriteTimeUtc) { continue }
        for ($attempt = 1; ; $attempt++) {
            try {
                New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null
                Copy-Item -LiteralPath (Join-Path $backupPath $rel) -Destination $dst -Force   # keeps the timestamp
                break
            }
            catch {
                if ($attempt -ge 4) { throw "Rollback could not restore ${rel}: $($_.Exception.Message)" }
                Start-Sleep -Seconds $retryWaitSeconds
            }
        }
    }
    $removed = @($written | Where-Object { -not $inBackup.Contains($_) -and (Test-Path -LiteralPath (Join-Path $appPath $_) -PathType Leaf) })
    foreach ($rel in $removed) { Remove-Item -LiteralPath (Join-Path $appPath $rel) -Force }
    if ($logPath) { Add-Content -LiteralPath $logPath -Value "$(Get-Date -Format o) ROLLBACK from ${backupPath}: restored $($restore.Count) file(s), removed $($removed.Count) file(s) the backup lacks." }
    $bad = @(Compare-DeployedFiles $backupPath $appPath $restore) +
           @($written | Where-Object { -not $inBackup.Contains($_) -and (Test-Path -LiteralPath (Join-Path $appPath $_) -PathType Leaf) })
    if ($bad.Count) { throw "Rollback incomplete: $($bad.Count) file(s) still differ from the backup, e.g. $(($bad | Select-Object -First 5) -join ', ')." }
}

# ---- Source guard: only a clean checkout of origin/main is deployed (2026-11-13) -----------------------------------
# The script publishes the working folder, so uncommitted or unmerged code could ship under a released version number.

# Reasons this source must not be deployed; nothing when it is a clean checkout of origin/main.
# $porcelain = the lines of `git status --porcelain`. Untracked *.md outside wwwroot are ignored: publish never ships them.
function Get-SourceProblems([string[]]$porcelain, [string]$head, [string]$originMain) {
    $dirty = @($porcelain | Where-Object { $_ -and $_ -notmatch '^\?\? "?(?!wwwroot/)[^"]*\.md"?$' })
    if ($dirty.Count) { "the working folder has $($dirty.Count) uncommitted or untracked change(s), e.g. $(($dirty | Select-Object -First 3 | ForEach-Object { $_.Trim() }) -join ', ')" }
    if (-not $head -or -not $originMain) { "git could not read the commits (HEAD '$head', origin/main '$originMain')" }
    elseif ($head -ne $originMain) { "HEAD $head is not origin/main $originMain" }
}

# Commit a build was made from: the .NET SDK stamps it into the DLL's product version ("1.0.0+<sha>"). $null if absent.
function Get-BuiltCommit([string]$dllPath) {
    if (-not (Test-Path -LiteralPath $dllPath)) { return $null }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath).ProductVersion -match '\+([0-9a-f]{40})') { $Matches[1] } else { $null }
}

function Test-ProductionTarget([string]$destAppPath) { $destAppPath -match '\\EINVWORLD\\App\\?$' }

# Verified database backup, mandatory before every Production deploy (migrations or not): for each database,
# BACKUP ... COPY_ONLY, CHECKSUM into the instance's default backup folder, RESTORE VERIFYONLY ... WITH CHECKSUM, then
# msdb must list the file as checksummed, undamaged and copy-only. Runs as the caller's Windows identity, which needs
# BACKUP DATABASE + CREATE DATABASE (VERIFYONLY's requirement), i.e. sysadmin or dbcreator - the app's own login
# rightly has neither. Self-contained, so the deploy can run it on the SQL host with Invoke-Command. Throws on any failure;
# returns one { Database, File } per database.
function Invoke-VerifiedDbBackup([string]$dataSource, [string[]]$databases, [string]$label) {
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $conn = New-Object System.Data.SqlClient.SqlConnection "Data Source=$dataSource;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True"
    $conn.Open()
    try {
        foreach ($db in $databases) {
            if ([string]::IsNullOrWhiteSpace($db)) { throw 'a database name is empty - nothing was backed up for it' }
            $k = $conn.CreateCommand()
            $k.CommandTimeout = 3600
            $k.CommandText = "SET NOCOUNT ON; " +
                "DECLARE @d nvarchar(400) = CAST(COALESCE(SERVERPROPERTY('InstanceDefaultBackupPath'), SERVERPROPERTY('InstanceDefaultDataPath')) AS nvarchar(400)); " +
                "IF RIGHT(@d, 1) <> N'\' SET @d += N'\'; DECLARE @f nvarchar(500) = @d + @file; " +
                "DECLARE @sql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@db) + N' TO DISK = @f WITH COPY_ONLY, CHECKSUM, INIT'; " +
                "EXEC sp_executesql @sql, N'@f nvarchar(500)', @f; " +
                "RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM; " +
                "SELECT TOP 1 f.physical_device_name, bs.has_backup_checksums, bs.is_damaged, bs.is_copy_only FROM msdb.dbo.backupset bs " +
                "JOIN msdb.dbo.backupmediafamily f ON f.media_set_id = bs.media_set_id WHERE f.physical_device_name = @f ORDER BY bs.backup_finish_date DESC;"
            [void]$k.Parameters.AddWithValue('@db', $db)
            [void]$k.Parameters.AddWithValue('@file', "${db}_predeploy_${label}_$stamp.bak")
            $r = $k.ExecuteReader()
            try {
                if (-not $r.Read()) { throw "[$db] the backup is not recorded in msdb" }
                if (-not $r.GetBoolean(1) -or $r.GetBoolean(2) -or -not $r.GetBoolean(3)) { throw "[$db] the backup is not a checksummed, undamaged COPY_ONLY backup" }
                [pscustomobject]@{ Database = $db; File = $r.GetString(0) }
            }
            finally { $r.Close() }
        }
    }
    finally { $conn.Close() }
}

# Public hostname for a deploy target; $null for a target with no known public site (e.g. a local test folder).
function Get-DefaultPublicBaseUrl([string]$destAppPath) {
    switch -Regex ($destAppPath) {
        '\\EINVWORLD_STAGING\\App\\?$' { return 'https://staging.einvworld.com' }
        '\\EINVWORLD\\App\\?$' { return 'https://www.einvworld.com' }
        default { return $null }
    }
}
