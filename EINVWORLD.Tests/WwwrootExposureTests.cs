using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Everything under wwwroot is served to anyone without signing in (UseStaticFiles runs before
    /// authentication). Invoice data and debug pages must never live there: v1.50.18 still shipped 2024 draft
    /// invoices (with an NRIC, TIN, email and phone) and sample upload CSVs that Staging and Production served publicly.
    /// The never-public list has one source of truth, the &lt;NeverPublicPath&gt; items in EINVWORLD.csproj; these tests
    /// keep every other copy (publish exclusion, .gitignore, the deploy check's known paths, the repo) in line with it.
    /// </summary>
    public class WwwrootExposureTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EINVWORLD.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir); // the test runs from inside the repo
            return dir!.FullName;
        }

        private static string[] NeverPublicGlobs() =>
            XDocument.Load(Path.Combine(RepoRoot(), "EINVWORLD.csproj")).Descendants()
                .Where(e => e.Name.LocalName == "NeverPublicPath")
                .Select(e => (string)e.Attribute("Include")!)
                .ToArray();

        // MSBuild glob (backslashes, "**") -> regex over an App\-relative path; a trailing ".br"/".gz" copy matches too.
        internal static Regex GlobRegex(string glob) =>
            new("^" + Regex.Escape(glob.Replace('/', '\\'))
                    .Replace(@"\*\*", ".*").Replace(@"\*", @"[^\\]*").Replace(@"\?", ".") + @"(\.br|\.gz)?$",
                RegexOptions.IgnoreCase);

        private static bool IsNeverPublic(string relativePath) =>
            NeverPublicGlobs().Any(g => GlobRegex(g).IsMatch(relativePath.Replace('/', '\\')));

        [Fact]
        public void TheListCoversTheKnownLeaks()
        {
            var globs = NeverPublicGlobs();
            foreach (var required in new[] { @"wwwroot\Drafts\**", @"wwwroot\tempcsv\**", @"wwwroot\test-*.html", @"wwwroot\theme-test.html" })
                Assert.Contains(required, globs);
        }

        [Fact]
        public void PublishExcludesTheList()
        {
            // Verified with a probe publish when added: files matching the list never reach a deploy.
            var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "EINVWORLD.csproj"));
            Assert.Contains("<Content Remove=\"@(NeverPublicPath)\" />", csproj);
        }

        [Fact]
        public void NoNeverPublicFileIsInTheRepo()
        {
            var root = RepoRoot();
            var offenders = Directory.EnumerateFiles(Path.Combine(root, "wwwroot"), "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f))
                .Where(IsNeverPublic)
                .ToList();
            Assert.True(offenders.Count == 0, "Never-public files under wwwroot: " + string.Join(", ", offenders));
        }

        [Fact]
        public void GitIgnoreCoversTheList()
        {
            var ignored = File.ReadAllLines(Path.Combine(RepoRoot(), ".gitignore")).Select(l => l.Trim()).ToHashSet();
            foreach (var glob in NeverPublicGlobs())
            {
                var gitPattern = glob.Replace('\\', '/').Replace("/**", "/");
                Assert.True(ignored.Contains(gitPattern), $".gitignore lacks '{gitPattern}' for <NeverPublicPath Include=\"{glob}\" />");
            }
        }

        [Fact]
        public void EveryKnownLeakedPathIsOnTheList()
        {
            var known = File.ReadAllLines(Path.Combine(RepoRoot(), "scripts", "never-public-known-paths.txt"))
                .Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
            Assert.True(known.Count >= 20, $"found {known.Count}");
            Assert.All(known, p => Assert.True(IsNeverPublic(p), $"{p} matches no <NeverPublicPath> pattern"));
        }

        [Theory]
        [InlineData(@"wwwroot\Drafts\**", @"wwwroot\Drafts\EINV00149.json", true)]
        [InlineData(@"wwwroot\Drafts\**", @"wwwroot\Drafts\sub\x.json.gz", true)]
        [InlineData(@"wwwroot\test-*.html", @"wwwroot\test-sorting.html.br", true)]
        [InlineData(@"wwwroot\test-*.html", @"wwwroot\js\test-helper.html", false)]
        [InlineData(@"wwwroot\theme-test.html", @"wwwroot\theme-test.html", true)]
        [InlineData(@"wwwroot\Drafts\**", @"wwwroot\uploads\Drafts.png", false)]
        public void GlobMatching(string glob, string path, bool matches) => Assert.Equal(matches, GlobRegex(glob).IsMatch(path));
    }
}
