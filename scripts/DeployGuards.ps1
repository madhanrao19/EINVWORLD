<#
    Stale-file guards for Deploy-Staging.ps1 (dot-sourced; tested by EINVWORLD.Tests\DeployGuardsTests.cs).

    v1.50.19 shipped 2024 draft invoices (with an NRIC) to Staging because `dotnet publish` never empties its
    output folder, so files dropped from the build kept shipping. These guards make that fail closed:
      - the script only publishes into an empty folder, and records what that clean publish produced;
      - nothing is copied unless the folder still holds exactly that file list and this repo's version;
      - files that must never be public are refused at the source and removed from the server.
    Pure ASCII on purpose: PowerShell 5.1 misreads UTF-8 without a BOM.
#>

# Relative paths (under App\) that must never be served. Mirrors the <Content Remove> data/debug entries in
# EINVWORLD.csproj; the trailing * also covers the .br/.gz copies publish generates.
$ForbiddenPublishPatterns = @('wwwroot\Drafts\*', 'wwwroot\tempcsv\*', 'wwwroot\test-*.html*', 'wwwroot\theme-test.html*')

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
# Only the forbidden folders and wwwroot's top level are read, never the whole upload tree.
function Find-ForbiddenServerFiles([string]$appPath) {
    $www = Join-Path $appPath 'wwwroot'
    $found = @()
    foreach ($folder in 'Drafts', 'tempcsv') {
        $found += @(Get-RelativeFiles (Join-Path $www $folder) | ForEach-Object { "wwwroot\$folder\$_" })
    }
    if (Test-Path -LiteralPath $www) {
        $found += @([IO.Directory]::GetFiles($www) | ForEach-Object { 'wwwroot\' + [IO.Path]::GetFileName($_) })
    }
    Find-ForbiddenFiles $found
}
