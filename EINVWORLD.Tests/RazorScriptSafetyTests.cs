using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Razor output inside a JavaScript template literal is an XSS hole: Razor HTML-encodes, but "${...}"
    /// contains nothing it encodes, so a saved item description like "${fetch(...)}" ran as script on Create
    /// e-Invoice (fixed in v1.50.19). Pages must pass data as JSON and build markup with einvworld.optionsHtml /
    /// einvworld.escapeHtml (einvworld-ui.js). This scans every view so the pattern can't come back.
    /// </summary>
    public class RazorScriptSafetyTests
    {
        private static readonly Regex Script = new(@"<script\b[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private static readonly Regex TemplateLiteral = new(@"`([^`]*)`", RegexOptions.Singleline);
        // A Razor expression or code block: @x, @(…), @foreach, @if … (not @@ or a Razor comment).
        private static readonly Regex RazorOutput = new(@"(?<!@)@(?!@|\*)[A-Za-z_(]");

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EINVWORLD.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        [Fact]
        public void NoRazorOutputInsideJavaScriptTemplateLiterals()
        {
            var root = RepoRoot();
            var offenders = Directory.EnumerateFiles(Path.Combine(root, "Pages"), "*.cshtml", SearchOption.AllDirectories)
                .SelectMany(file =>
                {
                    var text = File.ReadAllText(file);
                    return Script.Matches(text)
                        .SelectMany(s => TemplateLiteral.Matches(s.Groups[1].Value)
                            .Where(t => RazorOutput.IsMatch(t.Groups[1].Value))
                            .Select(t => $"{Path.GetRelativePath(root, file)}: `{t.Groups[1].Value.Trim()[..System.Math.Min(60, t.Groups[1].Value.Trim().Length)]}…`"));
                })
                .ToList();

            Assert.True(offenders.Count == 0, "Razor output inside a JS template literal:\n" + string.Join("\n", offenders));
        }

        [Theory]
        [InlineData("`<option>@item.Text</option>`", true)]
        [InlineData("`${a} and @Model.X`", true)]
        [InlineData("`@foreach (var c in Model.List) { }`", true)]
        [InlineData("`${name} only`", false)]
        [InlineData("`user@@example.com`", false)]
        [InlineData("`mail me: a@b.com`", true)]   // conservative: a literal "@" next to a letter is flagged too
        public void Detector_Flags_Razor_In_Literals(string literal, bool flagged) =>
            Assert.Equal(flagged, RazorOutput.IsMatch(TemplateLiteral.Match(literal).Groups[1].Value));
    }
}
