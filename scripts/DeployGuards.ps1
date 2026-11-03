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

# Public hostname for a deploy target; $null for a target with no known public site (e.g. a local test folder).
function Get-DefaultPublicBaseUrl([string]$destAppPath) {
    switch -Regex ($destAppPath) {
        '\\EINVWORLD_STAGING\\App\\?$' { return 'https://staging.einvworld.com' }
        '\\EINVWORLD\\App\\?$' { return 'https://www.einvworld.com' }
        default { return $null }
    }
}
