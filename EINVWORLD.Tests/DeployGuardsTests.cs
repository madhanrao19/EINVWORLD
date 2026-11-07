using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// scripts\DeployGuards.ps1 keeps stale files out of a deploy: v1.50.19's Staging deploy still shipped 2024 draft
    /// invoices (with an NRIC) because the publish folder was reused. Each case runs the real guard functions in
    /// Windows PowerShell against a throwaway fake repo + publish folder.
    /// </summary>
    public class DeployGuardsTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "deployguards-" + Guid.NewGuid().ToString("N"));
        private string _lastOutput = "";

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static string Guards()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EINVWORLD.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "scripts", "DeployGuards.ps1");
        }

        // A fake repo at v1.0.0 with a clean publish (stamped) in bin\publish; $repo and $pub are set for the body.
        private string Run(string body)
        {
            var script = Path.Combine(_root, "case.ps1");
            Directory.CreateDirectory(_root);
            File.WriteAllText(script, $@"$ErrorActionPreference = 'Stop'
. '{Guards()}'
$repo = '{_root}\repo'; $pub = ""$repo\bin\publish""
New-Item -ItemType Directory -Force ""$pub\wwwroot\js"", ""$pub\wwwroot\uploads"" | Out-Null
Set-Content ""$repo\appsettings.json"" '{{ ""AppInfo"": {{ ""Version"": ""v1.0.0"" }} }}'
Copy-Item ""$repo\appsettings.json"" ""$pub\appsettings.json""
Set-Content ""$pub\EINVWORLD.dll"" 'x'; Set-Content ""$pub\wwwroot\js\site.js"" 'x'
try {{
{body}
'RESULT:OK'
}} catch {{ 'RESULT:THROW ' + $_.Exception.Message }}
");
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{script}\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            _lastOutput = output;
            var i = output.IndexOf("RESULT:", StringComparison.Ordinal);
            Assert.True(i >= 0, output);
            return output[(i + 7)..].Trim();
        }

        private void Passes(string body) { if (OperatingSystem.IsWindows()) Assert.Equal("OK", Run(body)); }
        private void Throws(string body, string because)
        {
            if (!OperatingSystem.IsWindows()) return;
            var result = Run(body);
            Assert.StartsWith("THROW", result);
            Assert.Contains(because, result);
        }

        [Fact] public void CleanPublish_Passes() => Passes("Write-PublishStamp $pub; Assert-PublishOutput $pub $repo");

        [Fact] public void FileAddedAfterThePublish_Stops() =>
            Throws("Write-PublishStamp $pub; Set-Content \"$pub\\wwwroot\\old.js\" 'x'; Assert-PublishOutput $pub $repo", "did not produce");

        [Fact] public void FolderNotFromACleanPublish_Stops() => Throws("Assert-PublishOutput $pub $repo", "no .deploy-publish-files.txt");

        [Fact] public void MissingFile_Stops() =>
            Throws("Write-PublishStamp $pub; Remove-Item \"$pub\\EINVWORLD.dll\"; Assert-PublishOutput $pub $repo", "missing 1");

        [Fact] public void OldBuild_Stops() =>
            Throws("Write-PublishStamp $pub; Set-Content \"$repo\\appsettings.json\" '{ \"Version\": \"v1.0.1\" }'; Assert-PublishOutput $pub $repo", "old build");

        [Theory]
        [InlineData(@"wwwroot\Drafts\EINV00149.json")]
        [InlineData(@"wwwroot\tempcsv\upload.csv")]
        [InlineData(@"wwwroot\test-sorting.html.gz")]
        [InlineData(@"wwwroot\theme-test.html")]
        public void NeverPublicFile_InTheBuild_Stops(string rel) =>
            Throws($"New-Item -ItemType Directory -Force (Split-Path \"$pub\\{rel}\") | Out-Null; Set-Content \"$pub\\{rel}\" 'x'; " +
                   "Write-PublishStamp $pub; Assert-PublishOutput $pub $repo", "must never be public");

        [Fact] public void PublishFolderInsideRepoBin_IsEmptied() =>
            Passes("Initialize-PublishFolder $pub $repo; if (Test-Path $pub) { throw 'not emptied' }");

        [Fact] public void NonEmptyFolderOutsideRepoBin_IsRefusedAndKept() =>
            Throws("$other = \"$repo\\..\\elsewhere\"; New-Item -ItemType Directory $other | Out-Null; Set-Content \"$other\\keep.txt\" 'x'; " +
                   "try { Initialize-PublishFolder $other $repo } finally { if (-not (Test-Path \"$other\\keep.txt\")) { 'RESULT:DELETED'; exit } }",
                   "outside the repo's bin");

        [Fact] public void EmptyFolderOutsideRepoBin_IsAccepted() =>
            Passes("$other = \"$repo\\..\\fresh\"; New-Item -ItemType Directory $other | Out-Null; Initialize-PublishFolder $other $repo");

        [Fact] public void ServerScan_FindsOnlyNeverPublicFiles() => Passes(@"
$app = ""$repo\..\App""
foreach ($f in 'wwwroot\Drafts\a.json', 'wwwroot\tempcsv\b.csv', 'wwwroot\test-columns.html.br', 'wwwroot\theme-test.html',
               'wwwroot\uploads\logos\Drafts.png', 'wwwroot\js\test-helper.js', 'wwwroot\site.html') {
    New-Item -ItemType Directory -Force (Split-Path ""$app\$f"") | Out-Null; Set-Content ""$app\$f"" 'x'
}
$found = @(Find-ForbiddenServerFiles $app | Sort-Object)
$want = @('wwwroot\Drafts\a.json', 'wwwroot\tempcsv\b.csv', 'wwwroot\test-columns.html.br', 'wwwroot\theme-test.html')
if (($found -join '|') -ne ($want -join '|')) { throw ""found: $($found -join ', ')"" }");

        // ---- Single source of truth: the guards read <NeverPublicPath> from EINVWORLD.csproj ----

        [Fact] public void Patterns_ComeFromTheCsproj() => Passes(@"
$want = 'wwwroot\Drafts\*|wwwroot\tempcsv\*|wwwroot\test-*.html*|wwwroot\theme-test.html*|wwwroot\json\*'
if (($ForbiddenPublishPatterns -join '|') -ne $want) { throw ""patterns: $($ForbiddenPublishPatterns -join '|')"" }");

        [Fact] public void CsprojWithoutTheList_IsRefused() =>
            Throws("Set-Content \"$repo\\x.csproj\" '<Project><ItemGroup><Content Remove=\"a\" /></ItemGroup></Project>'; Get-NeverPublicGlobs \"$repo\\x.csproj\"",
                   "No <NeverPublicPath> items");

        // ---- Post-deploy public security verification ----

        [Fact] public void Verdicts() => Passes(@"
$cases = @(@(200,'','FAIL'), @(206,'DYNAMIC','FAIL'), @(302,'HIT','FAIL'), @(404,'STALE','FAIL'), @(302,'REVALIDATED','FAIL'),
           @(302,'DYNAMIC','PASS'), @(302,'BYPASS','PASS'), @(404,'','PASS'), @(401,'MISS','PASS'), @(403,'EXPIRED','PASS'),
           @(0,'','RETRY'), @(502,'','RETRY'), @(503,'DYNAMIC','RETRY'))
foreach ($c in $cases) { $v = Get-PublicCheckVerdict $c[0] $c[1]; if ($v -ne $c[2]) { throw ""$($c[0]) $($c[1]) -> $v, want $($c[2])"" } }");

        [Fact] public void NoAnswer_IsRetriedWithGrowingGaps_ThenUnverified() => Passes(@"
$script:calls = @()
$probe = { param($url) $script:calls += [DateTime]::UtcNow; [pscustomobject]@{ Status = 0; Cache = '' } }
$r = @(Test-NeverPublicUrls -BaseUrl 'https://x' -RelativePaths @('wwwroot\Drafts\a.json') -Probe $probe -DelayMs 100 -MaxAttempts 3)
if ($r[0].Verdict -ne 'UNVERIFIED' -or $script:calls.Count -ne 3) { throw ""verdict $($r[0].Verdict), calls $($script:calls.Count)"" }
$gap1 = ($script:calls[1] - $script:calls[0]).TotalMilliseconds; $gap2 = ($script:calls[2] - $script:calls[1]).TotalMilliseconds
if ($gap1 -lt 180 -or $gap2 -lt 280) { throw ""gaps $gap1 / $gap2 ms: retries must back off"" }");

        [Fact] public void OneNoAnswerThenRedirect_Passes() => Passes(@"
$script:n = 0
$probe = { param($url) $script:n++; if ($script:n -eq 1) { [pscustomobject]@{ Status = 0; Cache = '' } } else { [pscustomobject]@{ Status = 302; Cache = 'DYNAMIC' } } }
$r = @(Test-NeverPublicUrls -BaseUrl 'https://x' -RelativePaths @('wwwroot\Drafts\a.json') -Probe $probe -DelayMs 0)
if ($r[0].Verdict -ne 'PASS' -or $r[0].Attempts -ne 2) { throw ""$($r[0].Verdict) after $($r[0].Attempts)"" }");

        [Fact] public void KnownPaths_BecomePublicUrls_WithoutCompressedCopies() => Passes(@"
$seen = New-Object System.Collections.ArrayList
$probe = { param($url) [void]$seen.Add($url); [pscustomobject]@{ Status = 302; Cache = 'DYNAMIC' } }
$paths = @(Get-KnownNeverPublicPaths) + @('wwwroot\Drafts\EINV00149.json.br', 'wwwroot\Drafts\EINV00149.json')
$r = @(Test-NeverPublicUrls -BaseUrl 'https://www.einvworld.com/' -RelativePaths $paths -Probe $probe -DelayMs 0)
if ($r.Count -ne 22 -or $seen.Count -ne 22) { throw ""rows $($r.Count), requests $($seen.Count)"" }
if ($seen -notcontains 'https://www.einvworld.com/Drafts/EINV00149.json') { throw ""urls: $($seen[0])"" }");

        // Real HTTP: a local server stands in for the public site and the verifier runs real curl against it.
        // ServedFile is the proof the check fails when a forbidden file becomes publicly accessible.

        private const string Secret = "NRIC-880101-14-5555-SECRET";

        private (string Output, string[] Rows) CheckAgainstServer(Func<string, int, (int Status, string? Cache)> respond)
        {
            using var server = new FakeSite(respond);
            Run($"$r = @(Test-NeverPublicUrls -BaseUrl 'http://127.0.0.1:{server.Port}' -RelativePaths (Get-KnownNeverPublicPaths) -DelayMs 0 -MaxAttempts 2); " +
                "$r | ForEach-Object { 'ROW ' + $_.Verdict + ' ' + $_.Status + ' ' + $_.Path }");
            return (_lastOutput, _lastOutput.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("ROW ")).ToArray());
        }

        [Fact]
        public void ServedFile_FailsTheCheck_AndItsContentIsNeverLogged()
        {
            if (!OperatingSystem.IsWindows()) return;
            var (output, rows) = CheckAgainstServer((path, _) => path == "/Drafts/EINV00149.json" ? (200, null) : (302, "DYNAMIC"));
            Assert.Equal(22, rows.Length);
            Assert.Equal(new[] { @"ROW FAIL 200 wwwroot\Drafts\EINV00149.json" }, rows.Where(r => !r.StartsWith("ROW PASS")).ToArray());
            Assert.DoesNotContain(Secret, output);
        }

        [Fact]
        public void CloudflareCachedCopy_FailsTheCheck()
        {
            if (!OperatingSystem.IsWindows()) return;
            var (_, rows) = CheckAgainstServer((path, _) => path.StartsWith("/tempcsv/") ? (302, "HIT") : (302, "BYPASS"));
            Assert.Equal(13, rows.Count(r => r.StartsWith("ROW FAIL 302 wwwroot\\tempcsv\\")));
            Assert.Equal(9, rows.Count(r => r.StartsWith("ROW PASS")));
        }

        [Fact]
        public void DroppedConnections_AreRetried_AndADeadPathIsUnverified()
        {
            if (!OperatingSystem.IsWindows()) return;
            // Every path drops its first request; theme-test.html never answers.
            var (_, rows) = CheckAgainstServer((path, hit) => hit == 1 || path == "/theme-test.html" ? (0, null) : (302, "DYNAMIC"));
            Assert.Equal(new[] { @"ROW UNVERIFIED 0 wwwroot\theme-test.html" }, rows.Where(r => !r.StartsWith("ROW PASS")).ToArray());
            Assert.Equal(21, rows.Count(r => r.StartsWith("ROW PASS 302")));
        }

        [Fact]
        public void AllRedirectedToLogin_Passes()
        {
            if (!OperatingSystem.IsWindows()) return;
            var (_, rows) = CheckAgainstServer((_, _) => (302, "DYNAMIC"));
            Assert.Equal(22, rows.Count(r => r.StartsWith("ROW PASS 302")));
        }

        // ---- Disk-space preflight, health check and post-deploy clean-up ----

        [Fact] public void DiskSpaceVerdicts() => Passes(@"
$cases = @(@($null, 1GB, 'UNKNOWN'), @(5GB, 10GB, 'BLOCK'), @(15GB, 1GB, 'WARN'), @(30GB, 1GB, 'OK'), @(19GB, 25GB, 'BLOCK'))
foreach ($c in $cases) { $v = Get-DiskSpaceVerdict $c[0] $c[1]; if ($v -ne $c[2]) { throw ""$($c[0]) / $($c[1]) -> $v, want $($c[2])"" } }
$free = Get-FreeSpaceBytes ""$repo\not\created\yet""
if (-not ($free -gt 0)) { throw ""free space of a missing folder's volume: $free"" }");

        [Fact] public void Cleanup_DeletesOnlyFoldersInsideRepoBinAndObj() => Passes(@"
$obj = ""$repo\obj\Release\net10.0\win-x64""; New-Item -ItemType Directory -Force $obj | Out-Null; Set-Content ""$obj\a.dll"" 'x'
$other = ""$repo\..\elsewhere""; New-Item -ItemType Directory $other | Out-Null; Set-Content ""$other\keep.txt"" 'x'
$r = @(Remove-DeployArtifacts @($pub, $obj, $other, ""$repo\bin"", ""$repo\bin\..\Pages"", ""$repo\bin\gone"") $repo)
$got = ($r | ForEach-Object { $_.Result }) -join ','
if ($got -ne 'REMOVED,REMOVED,REFUSED,REFUSED,REFUSED,ABSENT') { throw ""results: $got"" }
if ((Test-Path $pub) -or (Test-Path $obj)) { throw 'build output not deleted' }
if (-not (Test-Path ""$other\keep.txt"") -or -not (Test-Path ""$repo\bin"")) { throw 'deleted something outside the approved folders' }");

        [Fact] public void Cleanup_RefusesAFolderHoldingAJunction_AndKeepsItsTarget() => Passes(@"
$target = ""$repo\..\target""; New-Item -ItemType Directory $target | Out-Null; Set-Content ""$target\keep.txt"" 'x'
New-Item -ItemType Junction -Path ""$pub\link"" -Value $target | Out-Null
$r = @(Remove-DeployArtifacts @($pub) $repo)
if ($r[0].Result -ne 'REFUSED') { throw ""result: $($r[0].Result)"" }
if (-not (Test-Path ""$target\keep.txt"")) { throw 'junction target deleted' }
(Get-Item ""$pub\link"").Delete()");

        [Fact] public void Health_RetriesUntil200_AndReportsAnUnhealthySite() => Passes(@"
$script:n = 0
$probe = { param($url) $script:n++; [pscustomobject]@{ Status = $(if ($script:n -eq 1) { 503 } else { 200 }); Cache = '' } }
$r = @(Test-HealthUrls -BaseUrl 'https://x/' -Probe $probe -DelayMs 0)
if ($r.Count -ne 2 -or -not $r[0].Healthy -or $r[0].Attempts -ne 2 -or $r[1].Url -ne 'https://x/health/ready' -or -not $r[1].Healthy) { throw ""first: $($r[0].Status)/$($r[0].Attempts)"" }
$down = { param($url) [pscustomobject]@{ Status = 503; Cache = '' } }
$r = @(Test-HealthUrls -BaseUrl 'https://x' -Probe $down -DelayMs 0 -MaxAttempts 3)
if ($r[0].Healthy -or $r[0].Attempts -ne 3) { throw ""down: $($r[0].Healthy) after $($r[0].Attempts)"" }");

        // The real Deploy-Staging.ps1 in an isolated fake repo (its own scripts\, bin\, obj\) deploying to a local
        // App folder, with a local site standing in for the public one. It runs in its own powershell.exe so the
        // exit code is the real one: DEPLOY:OK only for exit 0.
        private string E2e => Path.Combine(_root, "e2e");
        private string App => Path.Combine(E2e, "App");
        private string Publish => Path.Combine(E2e, @"repo\bin\Release\net10.0\win-x64\publish");

        private string DeployEndToEnd(Func<string, int, (int Status, string? Cache)> respond, string extraArgs = "")
        {
            using var server = new FakeSite(respond);
            var scripts = Path.GetDirectoryName(Guards())!;
            var body = @"
$e = ""$repo\..\e2e""; $r2 = ""$e\repo""; $p2 = ""$r2\bin\Release\net10.0\win-x64\publish""; $o2 = ""$r2\obj\Release\net10.0\win-x64""
New-Item -ItemType Directory -Force ""$r2\scripts"", $p2, $o2, ""$e\App"" | Out-Null
Copy-Item 'SCRIPTS\Deploy-Staging.ps1', 'SCRIPTS\DeployGuards.ps1', 'SCRIPTS\app_offline.htm' ""$r2\scripts""
Copy-Item 'SCRIPTS\..\EINVWORLD.csproj' $r2
Set-Content ""$r2\scripts\never-public-known-paths.txt"" 'wwwroot/Drafts/EINV00149.json'
Copy-Item ""$repo\appsettings.json"" $r2; Copy-Item ""$repo\appsettings.json"" $p2
Set-Content ""$p2\EINVWORLD.exe"" 'x'; Write-PublishStamp $p2; Set-Content ""$o2\a.obj"" 'x'
Set-Content ""$e\App\web.config"" '<configuration />'
$ErrorActionPreference = 'Continue'
& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""$r2\scripts\Deploy-Staging.ps1"" -DestAppPath ""$e\App"" -SkipPublish -SkipMigrations -SkipRuntimeCheck -PublicBaseUrl 'http://127.0.0.1:PORT' -HealthRetryDelayMs 0 -CopyRetryWaitSeconds 1 EXTRA 2>&1 | ForEach-Object { 'LOG ' + $_ }
if ($LASTEXITCODE -eq 0) { 'DEPLOY:OK' } else { 'DEPLOY:FAILED exit=' + $LASTEXITCODE }
$ErrorActionPreference = 'Stop'
'STATE deployed=' + (Test-Path ""$e\App\EINVWORLD.exe"") + ' publish=' + (Test-Path $p2) + ' obj=' + (Test-Path $o2) + ' webconfig=' + ((Get-Content ""$e\App\web.config"") -eq '<configuration />') +
  ' offline=' + (Test-Path ""$e\App\app_offline.htm"") + ' exe=' + (Get-Content ""$e\App\EINVWORLD.exe"") + ' added=' + (Test-Path ""$e\App\added.dll"") +
  ' log=' + ((Get-Content ""$e\App\logs\stdout.log"" -ErrorAction SilentlyContinue) -join '|') + ' deploylogs=' + @(Get-ChildItem ""$e\DeployLogs"" -Filter *.log -ErrorAction SilentlyContinue).Count"
                .Replace("SCRIPTS", scripts).Replace("PORT", server.Port.ToString()).Replace("EXTRA", extraArgs);
            Run(body);
            return _lastOutput;
        }

        [Fact]
        public void Deploy_Succeeds_ThenDeletesOnlyTheLocalBuildOutput()
        {
            if (!OperatingSystem.IsWindows()) return;
            var output = DeployEndToEnd((path, _) => path.StartsWith("/health/") ? (200, null) : (302, "DYNAMIC"));
            Assert.Contains("DEPLOY:OK", output);
            Assert.Contains("STATE deployed=True publish=False obj=False webconfig=True", output);
        }

        [Fact]
        public void Deploy_FailingTheSecurityCheck_KeepsTheBuildOutputForDiagnosis()
        {
            if (!OperatingSystem.IsWindows()) return;
            var output = DeployEndToEnd((path, _) => path == "/Drafts/EINV00149.json" || path.StartsWith("/health/") ? (200, null) : (302, "DYNAMIC"));
            Assert.Contains("DEPLOY:FAILED", output);
            Assert.Contains("SECURITY VERIFICATION FAILED", output);
            Assert.Contains("STATE deployed=True publish=True obj=True webconfig=True", output);
            Assert.DoesNotContain(Secret, output);
        }

        // ---- Copy safety: unlock wait, copy failure, integrity, rollback (the 2026-11-08 locked-EINVWORLD.dll incident) ----

        private static bool Healthy(string path) => path.StartsWith("/health/");
        private static (int, string?) Up(string path, int _) => Healthy(path) ? (200, null) : (302, "DYNAMIC");

        // App\ holds the previous release (old EINVWORLD.dll/.exe, an IIS stdout log); the release adds added.dll.
        private void SeedOldBuild()
        {
            Directory.CreateDirectory(Path.Combine(App, "logs"));
            Directory.CreateDirectory(Publish);
            File.WriteAllText(Path.Combine(App, "EINVWORLD.exe"), "old");
            File.WriteAllText(Path.Combine(App, "EINVWORLD.dll"), "old");
            File.WriteAllText(Path.Combine(App, @"logs\stdout.log"), "old-log");
            File.WriteAllText(Path.Combine(Publish, "EINVWORLD.dll"), "new");
            File.WriteAllText(Path.Combine(Publish, "added.dll"), "new");
        }

        // Holds a file the way a running app holds its loaded DLL: others may read it, nobody may replace it.
        private static FileStream HoldOpen(string path, FileShare share = FileShare.Read) => new(path, FileMode.Open, FileAccess.Read, share);

        [Fact]
        public async Task Deploy_WaitsForTheAppToReleaseItsDll_ThenDeploys()
        {
            if (!OperatingSystem.IsWindows()) return;
            SeedOldBuild();
            var dll = HoldOpen(Path.Combine(App, "EINVWORLD.dll"));
            // The app releases its DLL 8 s after app_offline.htm appears: the deploy must wait, not copy over it.
            var release = Task.Run(async () =>
            {
                while (!File.Exists(Path.Combine(App, "app_offline.htm"))) await Task.Delay(50);
                await Task.Delay(8000);   // longer than the old fixed 5 s pause
                dll.Dispose();
            });
            var output = DeployEndToEnd(Up, "-ShutdownTimeoutSeconds 60");
            await release;
            Assert.Contains("DEPLOY:OK", output);
            Assert.Contains("App stopped after", output);
            Assert.Contains("offline=False exe=x added=True", output);
            Assert.Equal("new", File.ReadAllText(Path.Combine(App, "EINVWORLD.dll")));
        }

        [Fact]
        public void Deploy_AppNeverStops_CopiesNothing_AndExitsNonzero()
        {
            if (!OperatingSystem.IsWindows()) return;
            SeedOldBuild();
            using var dll = HoldOpen(Path.Combine(App, "EINVWORLD.dll"));
            var output = DeployEndToEnd(Up, "-ShutdownTimeoutSeconds 2");
            Assert.Contains("DEPLOY:FAILED exit=1", output);
            Assert.Contains("STOPPED BEFORE COPY", output);
            Assert.Contains("EINVWORLD.dll", output);
            Assert.Contains("webconfig=True offline=False exe=old added=False", output);
        }

        // A publish file nobody can read: robocopy has already copied the root files (a mixed build) when it fails on it.
        private FileStream UnreadablePublishFile()
        {
            Directory.CreateDirectory(Path.Combine(Publish, @"wwwroot\js"));
            File.WriteAllText(Path.Combine(Publish, @"wwwroot\js\locked.js"), "new");
            return HoldOpen(Path.Combine(Publish, @"wwwroot\js\locked.js"), FileShare.None);
        }

        [Fact]
        public void Deploy_CopyFailsHalfway_RollsBackToTheOldBuild_AndKeepsTheRobocopyLog()
        {
            if (!OperatingSystem.IsWindows()) return;
            SeedOldBuild();
            using var locked = UnreadablePublishFile();
            var output = DeployEndToEnd(Up);
            Assert.Contains("DEPLOY:FAILED exit=1", output);
            Assert.Contains("ROLLED BACK", output);
            Assert.Contains("webconfig=True offline=False exe=old added=False log=old-log deploylogs=1", output);
            Assert.Equal("old", File.ReadAllText(Path.Combine(App, "EINVWORLD.dll")));
        }

        [Fact]
        public void Deploy_CopyFailsWithoutABackup_LeavesTheSiteOffline()
        {
            if (!OperatingSystem.IsWindows()) return;
            SeedOldBuild();
            using var locked = UnreadablePublishFile();
            var output = DeployEndToEnd(Up, "-SkipBackup");
            Assert.Contains("DEPLOY:FAILED exit=1", output);
            Assert.Contains("LEFT OFFLINE", output);
            Assert.Contains("offline=True", output);
        }

        [Fact]
        public void Deploy_NewBuildFailsToStart_RollsBack_AndKeepsTheStartupLog()
        {
            if (!OperatingSystem.IsWindows()) return;
            SeedOldBuild();
            var log = Path.Combine(App, @"logs\stdout.log");
            // The new build crashes on start: health answers 500 and IIS writes the reason to the stdout log.
            var output = DeployEndToEnd((path, _) =>
            {
                if (!Healthy(path)) return (302, "DYNAMIC");
                lock (_root) { if (!File.ReadAllText(log).Contains("crash")) File.AppendAllText(log, "\r\ncrash"); }
                return (500, null);
            });
            Assert.Contains("DEPLOY:FAILED exit=1", output);
            Assert.Contains("HEALTH CHECK FAILED: the new build was ROLLED BACK", output);
            Assert.Contains("webconfig=True offline=False exe=old added=False log=old-log|crash", output);
            Assert.Equal("old", File.ReadAllText(Path.Combine(App, "EINVWORLD.dll")));
        }

        [Fact]
        public void Deploy_OnlyReadinessFails_IsNotRolledBack()
        {
            if (!OperatingSystem.IsWindows()) return;
            SeedOldBuild();
            var output = DeployEndToEnd((path, _) => path == "/health/ready" ? (503, null) : Up(path, 0));
            Assert.Contains("DEPLOY:FAILED exit=1", output);
            Assert.Contains("Not rolled back", output);
            Assert.Contains("offline=False exe=x added=True", output);
        }

        [Fact] public void Rollback_UndoesAPartialDeploy_AndLeavesEverythingElseAlone() => Passes(@"
$b = ""$repo\..\backup""; $a = ""$repo\..\App""
New-Item -ItemType Directory -Force ""$b\wwwroot"", ""$a\wwwroot\uploads"", ""$a\logs"" | Out-Null
Set-Content ""$b\EINVWORLD.dll"" 'old'; Set-Content ""$b\wwwroot\site.js"" 'old'; Set-Content ""$b\stale.dll"" 'old'
Set-Content ""$b\.deploy-manifest.txt"" @('EINVWORLD.dll', 'wwwroot\site.js', 'stale.dll'); Set-Content ""$b\web.config"" 'backup-cfg'
Set-Content ""$b\appsettings.Production.json"" 'backup-settings'
Set-Content ""$repo\..\outside.txt"" 'keep'; Set-Content ""$b\..\outside.txt"" 'keep'
Copy-Item ""$b\*"" $a -Recurse -Force
# The failed deploy: EINVWORLD.dll replaced, site.js not yet, added.dll new, stale.dll removed, a new manifest.
Set-Content ""$a\EINVWORLD.dll"" 'new'; Set-Content ""$a\added.dll"" 'new'; Remove-Item ""$a\stale.dll""
Set-Content ""$a\.deploy-manifest.txt"" @('EINVWORLD.dll', 'wwwroot\site.js', 'added.dll')
# Written since the backup by others: must survive.
Set-Content ""$a\web.config"" 'server-cfg'; Set-Content ""$a\logs\stdout.log"" 'crash'; Set-Content ""$a\wwwroot\uploads\u.png"" 'u'
# A tampered manifest (traversal, rooted) and a file this run excludes from the copy must not be restored.
Add-Content ""$b\.deploy-manifest.txt"" @('..\outside.txt', ""$repo\..\outside.txt"", 'appsettings.Production.json')
Set-Content ""$a\appsettings.Production.json"" 'server-settings'
Restore-AppFromBackup $b $a @('EINVWORLD.dll', 'wwwroot\site.js', 'added.dll') 0 '' @('web.config', 'app_offline.htm', 'appsettings.Production.json')
if ((Get-Content ""$a\appsettings.Production.json"") -ne 'server-settings') { throw 'restored a file the copy excludes' }
foreach ($f in 'EINVWORLD.dll', 'wwwroot\site.js', 'stale.dll') { if ((Get-Content ""$a\$f"") -ne 'old') { throw ""$f not restored"" } }
if ((Get-Content ""$a\.deploy-manifest.txt"")[0..2] -join ',' -ne 'EINVWORLD.dll,wwwroot\site.js,stale.dll') { throw 'manifest not restored' }
if (Test-Path ""$a\added.dll"") { throw 'the failed release''s new file is still there' }
if ((Get-Content ""$a\web.config"") -ne 'server-cfg' -or (Get-Content ""$a\logs\stdout.log"") -ne 'crash' -or -not (Test-Path ""$a\wwwroot\uploads\u.png"")) { throw 'touched a file the deploy never wrote' }");

        [Fact] public void Rollback_ThatCannotRestoreAFile_Throws() => Throws(@"
$b = ""$repo\..\backup""; $a = ""$repo\..\App""
New-Item -ItemType Directory -Force $b, $a | Out-Null
Set-Content ""$b\EINVWORLD.dll"" 'old'; Set-Content ""$a\EINVWORLD.dll"" 'new'
$h = [IO.File]::Open(""$a\EINVWORLD.dll"", 'Open', 'Read', 'Read')
try { Restore-AppFromBackup $b $a @('EINVWORLD.dll') 0 } finally { $h.Close() }", "Rollback could not restore EINVWORLD.dll");

        [Fact] public void UnlockWait_ReportsTheHeldFile_AndPassesOnceReleased() => Passes(@"
$a = ""$repo\..\App""; New-Item -ItemType Directory -Force ""$a\runtimes"" | Out-Null
Set-Content ""$a\EINVWORLD.dll"" 'x'; Set-Content ""$a\runtimes\native.dll"" 'x'; Set-Content ""$a\notes.txt"" 'x'
$h = [IO.File]::Open(""$a\runtimes\native.dll"", 'Open', 'Read', 'Read')
try { $w = Wait-AppFilesUnlocked -AppPath $a -TimeoutSeconds 1 -PollMs 100 } finally { $h.Close() }
if ($w.Unlocked -or ($w.Locked -join ',') -ne 'runtimes\native.dll') { throw ""held: $($w.Unlocked) $($w.Locked)"" }
$w = Wait-AppFilesUnlocked -AppPath $a -TimeoutSeconds 1 -PollMs 100
if (-not $w.Unlocked) { throw 'still locked after release' }");

        /// <summary>Minimal HTTP/1.1 server on a free loopback port. Status 0 = close without answering (curl sees 000).</summary>
        private sealed class FakeSite : IDisposable
        {
            private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource _stop = new();
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _hits = new();
            public int Port { get; }

            public FakeSite(Func<string, int, (int Status, string? Cache)> respond)
            {
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _ = Task.Run(async () =>
                {
                    while (!_stop.IsCancellationRequested)
                    {
                        TcpClient client;
                        try { client = await _listener.AcceptTcpClientAsync(_stop.Token); } catch { return; }
                        using (client)
                        {
                            var stream = client.GetStream();
                            var buffer = new byte[8192];
                            var read = await stream.ReadAsync(buffer);
                            var path = Encoding.ASCII.GetString(buffer, 0, read).Split(' ').ElementAtOrDefault(1) ?? "/";
                            var (status, cache) = respond(path, _hits.AddOrUpdate(path, 1, (_, n) => n + 1));
                            if (status == 0) continue;
                            var body = status == 200 ? Secret : "";
                            var head = $"HTTP/1.1 {status} X\r\nContent-Length: {body.Length}\r\nConnection: close\r\n" +
                                       (status == 302 ? "Location: /login\r\n" : "") + (cache != null ? $"cf-cache-status: {cache}\r\n" : "") + "\r\n";
                            var bytes = Encoding.ASCII.GetBytes(head + body);
                            await stream.WriteAsync(bytes);
                        }
                    }
                });
            }

            public void Dispose() { _stop.Cancel(); _listener.Stop(); }
        }
    }
}
