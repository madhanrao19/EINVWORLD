using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Controllers;
using eInvWorld.Helpers;
using eInvWorld.Services.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Xunit;

namespace EINVWORLD.Tests
{
    public class ContentSecurityPolicyTests
    {
        private static Dictionary<string, string> Directives(string policy) =>
            policy.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                  .ToDictionary(d => d.Split(' ')[0], d => d);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Scripts_RunOnlyFromSelf_TheNonce_AndNamedThirdParties(bool analytics)
        {
            var d = Directives(ContentSecurityPolicy.Build("abc123==", analytics, upgradeInsecure: true));
            var script = d["script-src"];
            Assert.Contains("'nonce-abc123=='", script);
            Assert.DoesNotContain("unsafe-inline", script);
            Assert.DoesNotContain("unsafe-eval", script);
            Assert.DoesNotContain("*", script);
            Assert.DoesNotContain("https: ", script + " ");
            Assert.Contains("https://challenges.cloudflare.com", script); // Turnstile
            Assert.Equal("script-src-attr 'none'", d["script-src-attr"]);
            Assert.DoesNotContain("unsafe-inline", d["style-src-elem"]);
        }

        [Fact]
        public void Lockdown_Directives_ArePresent()
        {
            var d = Directives(ContentSecurityPolicy.Build("n", analytics: false, upgradeInsecure: true));
            Assert.Equal("default-src 'self'", d["default-src"]);
            Assert.Equal("object-src 'none'", d["object-src"]);
            Assert.Equal("base-uri 'self'", d["base-uri"]);
            Assert.Equal("form-action 'self'", d["form-action"]);
            Assert.Equal("frame-ancestors 'self'", d["frame-ancestors"]);
            Assert.True(d.ContainsKey("upgrade-insecure-requests"));
            Assert.Equal("report-uri /csp-report", d["report-uri"]);
            Assert.Equal("report-to csp", d["report-to"]);
            Assert.DoesNotContain("https:", d["img-src"].Replace("https://", "")); // no bare https: wildcard
        }

        [Fact]
        public void Analytics_HostsAppearOnlyWhereAnalyticsIsConfigured()
        {
            Assert.DoesNotContain("google-analytics", ContentSecurityPolicy.Build("n", analytics: false, upgradeInsecure: true));
            Assert.DoesNotContain("googletagmanager", ContentSecurityPolicy.Build("n", analytics: false, upgradeInsecure: true));
            Assert.Contains("https://www.googletagmanager.com", ContentSecurityPolicy.Build("n", analytics: true, upgradeInsecure: true));
        }

        [Fact]
        public void LocalHttp_DoesNotUpgradeRequests() =>
            Assert.DoesNotContain("upgrade-insecure-requests", ContentSecurityPolicy.Build("n", false, upgradeInsecure: false));

        [Fact]
        public void Nonces_AreRandom_128Bit_AndStablePerRequest()
        {
            var nonces = Enumerable.Range(0, 2000).Select(_ => ContentSecurityPolicy.NewNonce()).ToList();
            Assert.Equal(nonces.Count, nonces.Distinct().Count());
            Assert.All(nonces, n => Assert.Equal(16, Convert.FromBase64String(n).Length));

            var a = new DefaultHttpContext(); var b = new DefaultHttpContext();
            Assert.Equal(ContentSecurityPolicy.GetNonce(a), ContentSecurityPolicy.GetNonce(a));
            Assert.NotEqual(ContentSecurityPolicy.GetNonce(a), ContentSecurityPolicy.GetNonce(b));
        }

        [Theory]
        [InlineData(null, CspMode.ReportOnly)]
        [InlineData("", CspMode.ReportOnly)]
        [InlineData("nonsense", CspMode.ReportOnly)] // a typo never turns the policy off
        [InlineData("Enforce", CspMode.Enforce)]
        [InlineData(" enforce ", CspMode.Enforce)]
        [InlineData("Off", CspMode.Off)]
        public void Mode_Parses_DefaultingToReportOnly(string? value, CspMode expected) =>
            Assert.Equal(expected, ContentSecurityPolicy.ParseMode(value));

        [Fact]
        public void ModeHeaders()
        {
            Assert.Equal("Content-Security-Policy", ContentSecurityPolicy.HeaderName(CspMode.Enforce));
            Assert.Equal("Content-Security-Policy-Report-Only", ContentSecurityPolicy.HeaderName(CspMode.ReportOnly));
            Assert.Null(ContentSecurityPolicy.HeaderName(CspMode.Off));
        }

        private static TagHelperOutput Render(CspNonceTagHelper helper, string tag, params TagHelperAttribute[] attrs)
        {
            var output = new TagHelperOutput(tag, new TagHelperAttributeList(attrs), (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
            helper.Process(new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "id"), output);
            return output;
        }

        [Fact]
        public void TagHelper_AddsThisResponsesNonce_ToScriptsAndStyles_WithoutOverwriting()
        {
            var http = new DefaultHttpContext();
            var helper = new CspNonceTagHelper { ViewContext = new ViewContext { HttpContext = http } };
            var nonce = ContentSecurityPolicy.GetNonce(http);
            Assert.Equal(nonce, Render(helper, "script").Attributes["nonce"].Value);
            Assert.Equal(nonce, Render(helper, "style").Attributes["nonce"].Value);
            Assert.Equal("keep", Render(helper, "script", new TagHelperAttribute("nonce", "keep")).Attributes["nonce"].Value);
        }

        [Fact]
        public void EveryViewFolder_RegistersTheNonceTagHelper()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "EINVWORLD.csproj"))) root = root.Parent;
            Assert.NotNull(root);
            var withScripts = Directory.EnumerateFiles(root!.FullName, "*.cshtml", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                // Views\ holds only the PDF template: rendered to a string for the headless PDF renderer, never sent to a browser.
                .Where(f => !f.StartsWith(Path.Combine(root.FullName, "Views") + Path.DirectorySeparatorChar))
                .Where(f => File.ReadAllText(f).Contains("<script", StringComparison.OrdinalIgnoreCase) || File.ReadAllText(f).Contains("<style", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.NotEmpty(withScripts);
            foreach (var view in withScripts)
            {
                var registered = false;
                for (var dir = new DirectoryInfo(Path.GetDirectoryName(view)!); dir != null && dir.FullName.Length >= root.FullName.Length; dir = dir.Parent)
                {
                    var imports = Path.Combine(dir.FullName, "_ViewImports.cshtml");
                    if (File.Exists(imports) && File.ReadAllText(imports).Contains("CspNonceTagHelper")) { registered = true; break; }
                }
                Assert.True(registered, $"{view} renders <script>/<style> but no _ViewImports above it registers CspNonceTagHelper");
            }
        }

        [Fact]
        public void Reports_ClassicFormat_DropQueryStrings()
        {
            var r = Assert.Single(CspReportController.Parse(
                "{\"csp-report\":{\"effective-directive\":\"script-src-attr\",\"blocked-uri\":\"inline\"," +
                "\"document-uri\":\"https://staging.einvworld.com/Identity/Account/ResetPassword?code=SECRET&email=a@b.c\"," +
                "\"source-file\":\"https://staging.einvworld.com/js/x.js?v=1\",\"line-number\":42,\"script-sample\":\"TIN C1234567890\"}}"));
            Assert.Equal("script-src-attr", r.Directive);
            Assert.Equal("inline", r.Blocked);
            Assert.Equal("https://staging.einvworld.com/Identity/Account/ResetPassword", r.Document);
            Assert.Equal("https://staging.einvworld.com/js/x.js:42", r.Source);
            Assert.DoesNotContain("SECRET", string.Join(" ", r));
            Assert.DoesNotContain("C1234567890", string.Join(" ", r));
        }

        [Fact]
        public void Reports_ReportingApiFormat_IsRead_AndBounded()
        {
            var one = "{\"type\":\"csp-violation\",\"body\":{\"effectiveDirective\":\"img-src\",\"blockedURL\":\"https://evil.example/p.gif?tin=C1\",\"documentURL\":\"https://www.einvworld.com/Invoices/InvoiceDetails2/EINV1?x=1\"}}";
            var r = Assert.Single(CspReportController.Parse("[" + one + "]"));
            Assert.Equal("https://evil.example/p.gif", r.Blocked);
            Assert.Equal("https://www.einvworld.com/Invoices/InvoiceDetails2/EINV1", r.Document);
            Assert.Equal(20, CspReportController.Parse("[" + string.Join(",", Enumerable.Repeat(one, 50)) + "]").Count);
            Assert.Empty(CspReportController.Parse("not json"));
            Assert.Equal("data:image/png;base64", CspReportController.StripUrl("data:image/png;base64,AAAA"));
        }
    }
}
