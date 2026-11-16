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

        private void Passes(string body)
        {
            if (!OperatingSystem.IsWindows()) return;
            var result = Run(body);
            Assert.True(result == "OK", result); // the whole message: Assert.Equal's diff cuts it at the first path
        }
        private void Throws(string body, string because)
        {
            if (!OperatingSystem.IsWindows()) return;
            var result = Run(body);
            Assert.StartsWith("THROW", result);
            Assert.Contains(because, result);
        }

        // Source guard (v1.50.51): only a clean checkout of origin/main is deployed.
        [Theory]
        [InlineData("@()", "a", "a", "")]                                                  // clean, on origin/main
        [InlineData("@('?? docs/NOTES.md', '?? \"docs/my notes.md\"')", "a", "a", "")]     // untracked docs are never published
        [InlineData("@(' M Program.cs')", "a", "a", "uncommitted")]
        [InlineData("@('?? Pages/New.cshtml.cs')", "a", "a", "uncommitted")]               // untracked code would be compiled in
        [InlineData("@('?? wwwroot/readme.md')", "a", "a", "uncommitted")]                 // wwwroot content is published
        [InlineData("@()", "a", "b", "is not origin/main")]
        [InlineData("@()", "a", "", "could not read")]
        public void SourceGuard_AllowsOnlyACleanOriginMain(string porcelain, string head, string originMain, string problem)
        {
            if (!OperatingSystem.IsWindows()) return;
            Run($"$p = @(Get-SourceProblems {porcelain} '{head}' '{originMain}'); 'PROBLEMS:' + ($p -join '|')");
            var problems = _lastOutput[(_lastOutput.IndexOf("PROBLEMS:", StringComparison.Ordinal) + 9)..].Split('\n')[0].Trim();
            if (problem == "") Assert.Equal("", problems); else Assert.Contains(problem, problems);
        }

        // Verified DB backup (mandatory for Production): runs for real against the SQL Server the integration tests use.
        [Fact]
        public void VerifiedDbBackup_BacksUpVerifiesAndRecordsEachDatabase()
        {
            var cs = Environment.GetEnvironmentVariable("INTEGRATION_SQLSERVER");
            if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(cs)) return;
            var server = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).DataSource;
            var db = "deploybak_" + Guid.NewGuid().ToString("N")[..12];
            void Exec(string sql)
            {
                using var c = new Microsoft.Data.SqlClient.SqlConnection(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs) { InitialCatalog = "master" }.ConnectionString);
                c.Open();
                using var k = c.CreateCommand(); k.CommandText = sql; k.ExecuteNonQuery();
            }
            Exec($"CREATE DATABASE [{db}]");
            try
            {
                Passes($@"$r = @(Invoke-VerifiedDbBackup '{server}' @('{db}') 'v9.9.9')
if ($r.Count -ne 1 -or $r[0].Database -ne '{db}') {{ throw ""unexpected result: $($r | Out-String)"" }}
if ($r[0].File -notlike '*{db}_predeploy_v9.9.9_*.bak' -or -not (Test-Path -LiteralPath $r[0].File)) {{ throw ""no backup file: $($r[0].File)"" }}
Remove-Item -LiteralPath $r[0].File
# The way the deploy calls it: the function as a script block through Invoke-Command, the database list as one argument.
$r2 = @(Invoke-Command -ScriptBlock ${{function:Invoke-VerifiedDbBackup}} -ArgumentList '{server}', @('{db}'), 'v1.0.0-to-v1.0.1')
if ($r2.Count -ne 1 -or -not (Test-Path -LiteralPath $r2[0].File)) {{ throw ""Invoke-Command path failed: $($r2 | Out-String)"" }}
Remove-Item -LiteralPath $r2[0].File
$lacking = @(Invoke-Command -ScriptBlock ${{function:Get-DbBackupRightsProblems}} -ArgumentList '{server}', @('{db}'))
if ($lacking.Count) {{ throw ""rights check failed for the database owner: $lacking"" }}");
                // A missing database stops the run (the deploy stops before anything changes).
                Throws($"Invoke-VerifiedDbBackup '{server}' @('{db}', 'no_such_db_{Guid.NewGuid():N}') 'v9.9.9'", "does not exist");
                Throws($"Invoke-VerifiedDbBackup '{server}' @('') 'v9.9.9'", "database name is empty");
            }
            finally
            {
                Exec($"ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{db}]");
                // The failing run backed the first database up before it stopped: remove that file too.
                using var c = new Microsoft.Data.SqlClient.SqlConnection(cs);
                c.Open();
                using var k = c.CreateCommand();
                k.CommandText = "SELECT CAST(COALESCE(SERVERPROPERTY('InstanceDefaultBackupPath'), SERVERPROPERTY('InstanceDefaultDataPath')) AS nvarchar(400))";
                if (k.ExecuteScalar() is string dir && Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, db + "_predeploy_*.bak")) File.Delete(f);
            }
        }

        [Fact] public void SourceGuard_KnowsProductionFromStaging() =>
            Passes(@"if (-not (Test-ProductionTarget '\\srv\e$\EINVWORLD\App')) { throw 'prod' }; if (Test-ProductionTarget '\\srv\e$\EINVWORLD_STAGING\App') { throw 'staging' }");

        [Fact] public void BuiltCommit_IsNullForAFileWithoutAStamp() =>
            Passes("if ($null -ne (Get-BuiltCommit \"$pub\\EINVWORLD.dll\")) { throw 'stamp' }; if ($null -ne (Get-BuiltCommit \"$pub\\missing.dll\")) { throw 'missing' }");

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

        // ---- Copy safety: wait for the app to stop, roll back a failed copy (2026-11-08 mixed-build incident) ----

        private static readonly DateTime OldBuildTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Holds a file the way a running app holds its loaded DLLs: others may read it, nobody may write it.</summary>
        private static FileStream HoldOpen(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        /// <summary>Releases <paramref name="held"/> <paramref name="afterMs"/> ms after <paramref name="trigger"/> appears.</summary>
        private static Task ReleaseAfter(FileStream held, string trigger, int afterMs) => Task.Run(async () =>
        {
            var until = DateTime.UtcNow.AddMinutes(2);
            while (!File.Exists(trigger) && DateTime.UtcNow < until) await Task.Delay(50);
            await Task.Delay(afterMs);
            held.Dispose();
        });

        private static void OldFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, OldBuildTime);
        }

        [Fact]
        public async Task Wait_ReturnsOnceTheAppReleasesItsDll()
        {
            if (!OperatingSystem.IsWindows()) return;
            var dll = Path.Combine(_root, "App", "EINVWORLD.dll");
            OldFile(dll, "old");
            var release = ReleaseAfter(HoldOpen(dll), Path.Combine(_root, "go"), 3000);
            var result = Run(@"Set-Content ""$repo\..\go"" 'x'
$w = Wait-AppFilesUnlocked -AppPath ""$repo\..\App"" -TimeoutSeconds 30 -PollMs 200
if (-not $w.Unlocked -or $w.Seconds -lt 2) { throw ""unlocked $($w.Unlocked) after $($w.Seconds) s"" }");
            await release;
            Assert.Equal("OK", result);
        }

        [Fact]
        public void Wait_TimesOut_AndNamesTheNativeDllStillHeld()
        {
            if (!OperatingSystem.IsWindows()) return;
            OldFile(Path.Combine(_root, "App", "EINVWORLD.dll"), "free");
            var native = Path.Combine(_root, "App", "runtimes", "win-x64", "native", "Magick.Native.dll");
            OldFile(native, "held");
            using var held = HoldOpen(native);
            Passes(@"$w = Wait-AppFilesUnlocked -AppPath ""$repo\..\App"" -TimeoutSeconds 2 -PollMs 200
if ($w.Unlocked -or $w.Seconds -lt 2 -or ($w.Locked -join '|') -ne 'runtimes\win-x64\native\Magick.Native.dll') { throw ""unlocked $($w.Unlocked), held: $($w.Locked -join '|')"" }");
        }

        // A backup's web.config and in-app key folder inherited "local users can read" from the backup's parent folder.
        [Fact] public void Backup_SecretsAreReadableOnlyByAdministratorsAndSystem() => Passes(@"
Protect-BackupSecrets ""$repo\..\NoSuchBackup""
# Only an elevated run (the deployer, CI) can read back what it has just closed to everyone but administrators.
if (([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
$bak = ""$repo\..\Bak""
New-Item -ItemType Directory -Force ""$bak\DataProtectionKeys"", ""$bak\temp"" | Out-Null
Set-Content ""$bak\web.config"" 'secrets'; Set-Content ""$bak\DataProtectionKeys\key-1.xml"" 'key'; Set-Content ""$bak\EINVWORLD.dll"" 'x'
Set-Content ""$bak\temp\web.config"" 'stray copy with secrets'
Protect-BackupSecrets $bak
# .NET, not Get-Acl: on the CI runner Windows PowerShell can't load Microsoft.PowerShell.Security.
function Sids($p) { (Get-Item -LiteralPath $p -Force).GetAccessControl().GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]) }
foreach ($p in ""$bak\web.config"", ""$bak\temp\web.config"", ""$bak\DataProtectionKeys"", ""$bak\DataProtectionKeys\key-1.xml"") {
    $extra = @(Sids $p | Where-Object { $_.IdentityReference.Value -notin 'S-1-5-32-544', 'S-1-5-18' })
    if ($extra.Count) { throw ""$p is still open to $($extra.IdentityReference.Value -join ', ')"" }
    if (@(Sids $p).Count -lt 2) { throw ""$p lost Administrators or SYSTEM"" }
}
if (-not @(Sids ""$bak\EINVWORLD.dll"" | Where-Object IsInherited).Count) { throw 'ordinary backup files must keep their inherited permissions' }
}");

        [Fact] public void Restore_PutsTheBackupBack_RemovesNewFiles_KeepsServerFiles() => Passes(@"
$app = ""$repo\..\App""; $bak = ""$repo\..\Bak""
function F($p, $c, $t) { New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null; Set-Content $p $c; (Get-Item $p).LastWriteTimeUtc = [datetime]$t }
F ""$bak\EINVWORLD.dll"" 'old' '2026-01-01'; F ""$bak\lib.dll"" 'old' '2026-01-01'; F ""$bak\web.config"" 'backup-cfg' '2026-01-01'
F ""$app\EINVWORLD.dll"" 'NEW' '2026-11-08'; F ""$app\lib.dll"" 'old' '2026-01-01'; F ""$app\only-new.dll"" 'NEW' '2026-11-08'
F ""$app\web.config"" 'server-cfg' '2026-11-08'; F ""$app\wwwroot\uploads\logo.png"" 'upload' '2026-11-08'; F ""$app\app_offline.htm"" 'off' '2026-11-08'
Restore-AppFromBackup $bak $app @('EINVWORLD.dll', 'lib.dll', 'only-new.dll') 1
if ((Get-Content ""$app\EINVWORLD.dll"") -ne 'old') { throw 'EINVWORLD.dll not restored' }
if (Test-Path ""$app\only-new.dll"") { throw 'file new in the failed release was kept' }
if ((Get-Content ""$app\web.config"") -ne 'server-cfg') { throw 'web.config overwritten' }
if (-not (Test-Path ""$app\wwwroot\uploads\logo.png"") -or -not (Test-Path ""$app\app_offline.htm"")) { throw 'server file removed' }");

        [Fact]
        public void Restore_FailsLoudly_WhenAFileCannotBeRestored()
        {
            if (!OperatingSystem.IsWindows()) return;
            OldFile(Path.Combine(_root, "Bak", "EINVWORLD.dll"), "old");
            var dll = Path.Combine(_root, "App", "EINVWORLD.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(dll)!);
            File.WriteAllText(dll, "NEW");
            using var held = HoldOpen(dll);
            Throws(@"Restore-AppFromBackup ""$repo\..\Bak"" ""$repo\..\App"" @('EINVWORLD.dll') 1", "Rollback could not restore EINVWORLD.dll");
        }

        // The real Deploy-Staging.ps1 in an isolated fake repo (its own scripts\, bin\, obj\) deploying to a local
        // The fake repo is not a git checkout, so each run passes -AllowUnverifiedSource unless it tests the source guard.
        // App folder, with a local site standing in for the public one. The App folder starts with an old build
        // (EINVWORLD.dll 'old-build', wwwroot\js\site.js) plus web.config; the release ships 'new-build'.
        private string E2eApp => Path.Combine(_root, "e2e", "App");

        private string DeployEndToEnd(Func<string, int, (int Status, string? Cache)> respond, string extraArgs = "", bool allowUnverifiedSource = true)
        {
            using var server = new FakeSite(respond);
            var scripts = Path.GetDirectoryName(Guards())!;
            foreach (var (rel, content) in new[] { ("EINVWORLD.dll", "old-build"), (@"wwwroot\js\site.js", "old-js") })
                if (!File.Exists(Path.Combine(E2eApp, rel))) OldFile(Path.Combine(E2eApp, rel), content);   // a test may already hold it open
            var body = @"
$e = ""$repo\..\e2e""; $r2 = ""$e\repo""; $p2 = ""$r2\bin\Release\net10.0\win-x64\publish""; $o2 = ""$r2\obj\Release\net10.0\win-x64""
New-Item -ItemType Directory -Force ""$r2\scripts"", $p2, $o2, ""$e\App"", ""$p2\wwwroot\js"" | Out-Null
Copy-Item 'SCRIPTS\Deploy-Staging.ps1', 'SCRIPTS\DeployGuards.ps1', 'SCRIPTS\app_offline.htm' ""$r2\scripts""
Copy-Item 'SCRIPTS\..\EINVWORLD.csproj' $r2
Set-Content ""$r2\scripts\never-public-known-paths.txt"" 'wwwroot/Drafts/EINV00149.json'
Copy-Item ""$repo\appsettings.json"" $r2; Copy-Item ""$repo\appsettings.json"" $p2
Set-Content ""$p2\EINVWORLD.exe"" 'x'; Set-Content ""$p2\EINVWORLD.dll"" 'new-build'; Set-Content ""$p2\wwwroot\js\site.js"" 'new-js-longer'
Write-PublishStamp $p2; Set-Content ""$o2\a.obj"" 'x'
Set-Content ""$e\App\web.config"" '<configuration />'
try { & ""$r2\scripts\Deploy-Staging.ps1"" -DestAppPath ""$e\App"" -SkipPublish -SkipMigrations -SkipRuntimeCheck -PublicBaseUrl 'http://127.0.0.1:PORT' -HealthRetryDelayMs 0 EXTRA -Confirm:$false 6>&1 | ForEach-Object { 'LOG ' + $_ }; 'DEPLOY:OK' }
catch { 'DEPLOY:FAILED ' + $_.Exception.Message }
'STATE deployed=' + (Test-Path ""$e\App\EINVWORLD.exe"") + ' publish=' + (Test-Path $p2) + ' obj=' + (Test-Path $o2) + ' webconfig=' + ((Get-Content ""$e\App\web.config"") -eq '<configuration />')
'FILES dll=' + (Get-Content ""$e\App\EINVWORLD.dll"") + ' offline=' + (Test-Path ""$e\App\app_offline.htm"") + ' drafts=' + (Test-Path ""$e\App\wwwroot\Drafts\EINV00149.json"")"
                .Replace("SCRIPTS", scripts).Replace("PORT", server.Port.ToString()).Replace("EXTRA", extraArgs + (allowUnverifiedSource ? " -AllowUnverifiedSource" : ""));
            Run(body);
            return _lastOutput;
        }

        private static (int Status, string? Cache) Healthy(string path, int _) => path.StartsWith("/health/") ? (200, null) : (302, "DYNAMIC");

        [Fact]
        public void Deploy_SourceNotACleanOriginMain_StopsBeforeAnyChange()
        {
            if (!OperatingSystem.IsWindows()) return;
            var output = DeployEndToEnd(Healthy, allowUnverifiedSource: false);
            Assert.Contains("DEPLOY:FAILED Source check:", output);
            Assert.Contains("Nothing was changed", output);
            Assert.Contains("FILES dll=old-build offline=False", output);                    // App\ untouched, never taken offline
            Assert.Empty(Directory.GetDirectories(Path.Combine(_root, "e2e"), "App_Backup_*")); // not even a backup
        }

        [Fact]
        public async Task Deploy_AppSlowToStop_WaitsForItThenDeploys()
        {
            if (!OperatingSystem.IsWindows()) return;
            OldFile(Path.Combine(E2eApp, "EINVWORLD.dll"), "old-build");
            var release = ReleaseAfter(HoldOpen(Path.Combine(E2eApp, "EINVWORLD.dll")), Path.Combine(E2eApp, "app_offline.htm"), 3000);
            var output = DeployEndToEnd(Healthy);
            await release;
            Assert.Contains("DEPLOY:OK", output);
            Assert.Contains("FILES dll=new-build offline=False", output);
            var waited = System.Text.RegularExpressions.Regex.Match(output, @"App stopped after (\d+) s");
            Assert.True(waited.Success && int.Parse(waited.Groups[1].Value) >= 2, output);
            Assert.Contains("Integrity check: all", output);
        }

        [Fact]
        public void Deploy_AppNeverStops_CopiesNothing_AndBringsTheOldBuildBack()
        {
            if (!OperatingSystem.IsWindows()) return;
            OldFile(Path.Combine(E2eApp, "EINVWORLD.dll"), "old-build");
            using var held = HoldOpen(Path.Combine(E2eApp, "EINVWORLD.dll"));
            var output = DeployEndToEnd(Healthy, "-ShutdownTimeoutSeconds 3");
            Assert.Contains("DEPLOY:FAILED STOPPED BEFORE COPY", output);
            Assert.Contains("EINVWORLD.dll", output);
            Assert.Contains("STATE deployed=False publish=True obj=True webconfig=True", output);
            Assert.Contains("FILES dll=old-build offline=False", output);
        }

        [Fact]
        public void Deploy_CopyFailsHalfway_RollsBackToTheOldBuild_WithoutNeverPublicFiles()
        {
            if (!OperatingSystem.IsWindows()) return;
            OldFile(Path.Combine(E2eApp, "wwwroot", "js", "site.js"), "old-js");
            OldFile(Path.Combine(E2eApp, "wwwroot", "Drafts", "EINV00149.json"), "draft");
            using var held = HoldOpen(Path.Combine(E2eApp, "wwwroot", "js", "site.js"));   // not a binary: the wait passes, the copy fails
            var output = DeployEndToEnd(Healthy, "-CopyRetryWaitSeconds 1");
            Assert.Contains("DEPLOY:FAILED DEPLOY FAILED and was ROLLED BACK", output);
            Assert.Contains("STATE deployed=False publish=True obj=True webconfig=True", output);   // EINVWORLD.exe was new: removed again
            Assert.True(output.Contains("FILES dll=old-build offline=False drafts=False"), output);
            var log = Directory.GetFiles(Path.Combine(_root, "e2e", "DeployLogs"), "*_robocopy.log").Single();
            Assert.Contains("site.js", File.ReadAllText(log));
        }

        [Fact]
        public void Deploy_UnhealthyAfterTheCopy_RollsBack()
        {
            if (!OperatingSystem.IsWindows()) return;
            var output = DeployEndToEnd((path, _) => path.StartsWith("/health/") ? (503, null) : (302, "DYNAMIC"));
            Assert.Contains("DEPLOY:FAILED HEALTH CHECK FAILED: the new build was ROLLED BACK", output);
            Assert.Contains("STILL UNHEALTHY", output);
            Assert.Contains("FILES dll=old-build offline=False", output);
        }

        [Fact]
        public void Deploy_NoHealthAnswerAtAll_KeepsTheVerifiedBuild_AndFails()
        {
            if (!OperatingSystem.IsWindows()) return;
            var output = DeployEndToEnd((path, _) => path.StartsWith("/health/") ? (0, null) : (302, "DYNAMIC"));
            Assert.Contains("DEPLOY:FAILED HEALTH UNVERIFIED", output);
            Assert.Contains("FILES dll=new-build offline=False", output);
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

        [Fact]
        public void Deploy_OnlyReadinessFails_IsNotRolledBack()
        {
            if (!OperatingSystem.IsWindows()) return;
            var output = DeployEndToEnd((path, _) => path == "/health/ready" ? (503, null) : Healthy(path, 0));
            Assert.True(output.Contains("DEPLOY:FAILED HEALTH CHECK FAILED: the app started"), output);
            Assert.True(output.Contains("FILES dll=new-build offline=False"), output);
        }

        [Fact]
        public async Task Deploy_CopyThatSkippedAChangedDll_IsCaughtByTheIntegrityCheck_AndRolledBack()
        {
            if (!OperatingSystem.IsWindows()) return;
            // Same size and timestamp as the build's EINVWORLD.dll ('new-build'), different content: robocopy reports
            // success without copying it, and only the integrity check's hash notices.
            var dll = Path.Combine(E2eApp, "EINVWORLD.dll");
            OldFile(dll, "old-buil!\r\n");   // the length of the build's 'new-build' line
            var trigger = Path.Combine(E2eApp, "app_offline.htm");
            var align = Task.Run(async () =>
            {
                var until = DateTime.UtcNow.AddMinutes(2);
                while (!File.Exists(trigger) && DateTime.UtcNow < until) await Task.Delay(20);
                var pub = Path.Combine(_root, "e2e", "repo", "bin", "Release", "net10.0", "win-x64", "publish", "EINVWORLD.dll");
                File.SetLastWriteTimeUtc(dll, File.GetLastWriteTimeUtc(pub));
            });
            var output = DeployEndToEnd(Healthy, "-ShutdownTimeoutSeconds 30");
            await align;
            Assert.True(output.Contains("don't match the build, e.g. EINVWORLD.dll"), output);
            Assert.True(output.Contains("DEPLOY:FAILED DEPLOY FAILED and was ROLLED BACK"), output);
            Assert.True(output.Contains("FILES dll=old-buil! offline=False"), output);
        }

        [Fact]
        public void Deploy_CopyFailsWithoutABackup_LeavesTheSiteOffline()
        {
            if (!OperatingSystem.IsWindows()) return;
            OldFile(Path.Combine(E2eApp, "wwwroot", "js", "site.js"), "old-js");
            using var held = HoldOpen(Path.Combine(E2eApp, "wwwroot", "js", "site.js"));
            var output = DeployEndToEnd(Healthy, "-SkipBackup -CopyRetryWaitSeconds 1");
            Assert.Contains("DEPLOY:FAILED DEPLOY FAILED and there is no backup", output);
            Assert.Contains("offline=True", output);
        }

        [Fact]
        public void Deploy_Succeeds_ThenDeletesOnlyTheLocalBuildOutput()
        {
            if (!OperatingSystem.IsWindows()) return;
            var output = DeployEndToEnd(Healthy);
            Assert.Contains("DEPLOY:OK", output);
            Assert.Contains("STATE deployed=True publish=False obj=False webconfig=True", output);
            Assert.Contains("FILES dll=new-build offline=False", output);
        }

        [Fact]
        public void Deploy_FailingTheSecurityCheck_KeepsTheBuildOutputForDiagnosis()
        {
            if (!OperatingSystem.IsWindows()) return;
            var output = DeployEndToEnd((path, _) => path == "/Drafts/EINV00149.json" || path.StartsWith("/health/") ? (200, null) : (302, "DYNAMIC"));
            Assert.Contains("DEPLOY:FAILED SECURITY VERIFICATION FAILED", output);
            Assert.Contains("STATE deployed=True publish=True obj=True webconfig=True", output);
            Assert.DoesNotContain(Secret, output);
        }

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
