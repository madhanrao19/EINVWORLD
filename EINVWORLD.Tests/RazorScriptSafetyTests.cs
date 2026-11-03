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

        /// <summary>
        /// v1.50.19 shipped "einvworld.optionsHtml(@Html.Raw(...)))), "EA");" on Create and Edit Invoice: Razor ends the
        /// expression at Raw's own closing parenthesis, so the extra ")" became a JavaScript syntax error that killed the
        /// whole script block (no new-row option lists, create-invoice.js crashed). Replace each single-line
        /// @Html.Raw(...) with a value, as Razor does, and the JavaScript left on that line must balance.
        /// </summary>
        [Fact]
        public void HtmlRawInScripts_LeavesBalancedJavaScript()
        {
            var root = RepoRoot();
            var offenders = Directory.EnumerateFiles(Path.Combine(root, "Pages"), "*.cshtml", SearchOption.AllDirectories)
                .SelectMany(file => Script.Matches(File.ReadAllText(file))
                    .SelectMany(s => s.Groups[1].Value.Split('\n'))
                    .Where(l => l.Contains("@Html.Raw(") && !l.TrimStart().StartsWith("//"))
                    .Select(l => (Line: l.Trim(), Js: WithoutRazorRaw(l)))
                    .Where(x => x.Js != null && Depth(x.Js) != 0)
                    .Select(x => $"{Path.GetRelativePath(root, file)}: {x.Line[..System.Math.Min(100, x.Line.Length)]}"))
                .ToList();
            Assert.True(offenders.Count == 0, "Unbalanced parentheses around @Html.Raw(...):\n" + string.Join("\n", offenders));
        }

        [Theory]
        [InlineData("x = f(@Html.Raw(S(a.Select(u => new { V = u })))), \"EA\");", false)]  // the v1.50.19 bug
        [InlineData("x = f(@Html.Raw(S(a.Select(u => new { V = u }))), \"EA\");", true)]
        [InlineData("x = f(@Html.Raw(S(\")\")));", true)]                                     // parens inside strings don't count
        public void BalanceCheck_FindsTheExtraParenthesis(string line, bool balanced) =>
            Assert.Equal(balanced, Depth(WithoutRazorRaw(line)!) == 0);

        // The line with each @Html.Raw(...) replaced by a value; null when one doesn't close on this line.
        private static string? WithoutRazorRaw(string line)
        {
            int at;
            while ((at = line.IndexOf("@Html.Raw(", System.StringComparison.Ordinal)) >= 0)
            {
                var open = at + "@Html.Raw".Length;
                var close = MatchingParen(line, open);
                if (close < 0) return null;
                line = line[..at] + "0" + line[(close + 1)..];
            }
            return line;
        }

        private static int MatchingParen(string s, int open)
        {
            var depth = 0;
            char? quote = null;
            for (var i = open; i < s.Length; i++)
            {
                var c = s[i];
                if (quote != null) { if (c == '\\') i++; else if (c == quote) quote = null; continue; }
                if (c is '"' or '\'') quote = c;
                else if (c == '(') depth++;
                else if (c == ')' && --depth == 0) return i;
            }
            return -1;
        }

        private static int Depth(string js)
        {
            var depth = 0;
            char? quote = null;
            for (var i = 0; i < js.Length; i++)
            {
                var c = js[i];
                if (quote != null) { if (c == '\\') i++; else if (c == quote) quote = null; continue; }
                if (c == '/' && i + 1 < js.Length && js[i + 1] == '/') break;
                if (c is '"' or '\'' or '`') quote = c;
                else if (c == '(') depth++;
                else if (c == ')') depth--;
            }
            return depth;
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
