using System;
using System.Diagnostics;
using System.IO;
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
    }
}
