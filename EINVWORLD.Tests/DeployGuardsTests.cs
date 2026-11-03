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
