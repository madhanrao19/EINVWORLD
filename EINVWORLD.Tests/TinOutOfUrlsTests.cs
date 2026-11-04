using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// v1.50.34: TIN / ID numbers (an NRIC for individuals) must never travel in a URL, where IIS, Cloudflare and the
    /// browser history record them. "Validate with LHDN", cancel/reject and validation details send them in a POST/PUT
    /// form body with the anti-forgery token instead.
    /// </summary>
    public class TinOutOfUrlsTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EINVWORLD.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        [Theory]
        [InlineData(typeof(eInvWorld.Pages.Suppliers.CreateModel), "ValidateTIN")]
        [InlineData(typeof(eInvWorld.Pages.PublicCustomer.CreateModel), "ValidateTIN")]
        [InlineData(typeof(EINVWORLD.Pages.Invoices.CreateInvoiceModel), "ValidateShippingRecipientTin")]
        [InlineData(typeof(eInvWorld.Pages.Invoices.InvoiceEditModel), "ValidateShippingRecipientTin")]
        [InlineData(typeof(eInvWorld.Pages.Invoices.InvoiceListsModel), "ValidationDetails")]
        public void IdentifierHandlers_ArePostOnly(Type page, string handler)
        {
            Assert.NotNull(page.GetMethod($"OnPost{handler}Async"));
            Assert.Null(page.GetMethod($"OnGet{handler}Async"));
        }

        [Theory]
        [InlineData(typeof(eInvWorld.Pages.Suppliers.CreateModel))]
        [InlineData(typeof(eInvWorld.Pages.PublicCustomer.CreateModel))]
        [InlineData(typeof(EINVWORLD.Pages.Invoices.CreateInvoiceModel))]
        [InlineData(typeof(eInvWorld.Pages.Invoices.InvoiceEditModel))]
        public void TinValidationPages_HaveThePerUserLimit(Type page)
        {
            var attr = (EnableRateLimitingAttribute?)Attribute.GetCustomAttribute(page, typeof(EnableRateLimitingAttribute), inherit: true);
            Assert.Equal(TinValidationRateLimit.Policy, attr?.PolicyName);
        }

        [Theory]
        [InlineData("POST", "ValidateTIN", true)]
        [InlineData("POST", "validateshippingrecipienttin", true)]
        [InlineData("GET", "ValidateTIN", false)]
        [InlineData("POST", "", false)]
        [InlineData("POST", "SubmitDocuments", false)]
        [InlineData("POST", "ValidationDetails", false)]
        [InlineData("POST", "ValidateTIN&handler=x", true)]     // Razor Pages runs the first value, so the limit must too
        [InlineData("POST", "x&handler=ValidateTIN", false)]
        public void RateLimit_AppliesOnlyToTinValidationPosts(string method, string handler, bool expected)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = method;
            if (handler.Length > 0) ctx.Request.QueryString = new QueryString("?handler=" + handler);
            Assert.Equal(expected, TinValidationRateLimit.Applies(ctx));
        }

        [Fact]
        public void RateLimit_UsesTheRouteHandlerFirst()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = "POST";
            ctx.Request.RouteValues["handler"] = "ValidateTIN";
            ctx.Request.QueryString = new QueryString("?handler=SubmitDocuments");
            Assert.True(TinValidationRateLimit.Applies(ctx));
        }

        [Fact]
        public void RateLimiter_RunsAfterAuthentication_SoPerUserPoliciesSeeTheUser()
        {
            // Before authentication every request is anonymous and per-user policies collapse into one bucket per IP.
            var program = File.ReadAllText(Path.Combine(RepoRoot(), "Program.cs"));
            int authn = program.IndexOf("app.UseAuthentication();"), limiter = program.IndexOf("app.UseRateLimiter();"), authz = program.IndexOf("app.UseAuthorization();");
            Assert.True(authn >= 0 && limiter > authn && authz > limiter, "Expected UseAuthentication, then UseRateLimiter, then UseAuthorization");
        }

        [Fact]
        public void NoViewOrScriptPutsIdentifiersInAUrl()
        {
            var root = RepoRoot();
            var identifierParam = new Regex(@"[?&](tin|idNo|idValue|idType|regNo|nric)=", RegexOptions.IgnoreCase);
            var vendor = new Regex(@"[\\/](lib|libs|node_modules)[\\/]|\.min\.js$", RegexOptions.IgnoreCase);
            var files = new[] { "Pages", "Areas", "Views" }.Select(d => Path.Combine(root, d)).Where(Directory.Exists)
                .SelectMany(d => Directory.EnumerateFiles(d, "*.cshtml", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "wwwroot", "js"), "*.js", SearchOption.AllDirectories))
                .Where(f => !vendor.IsMatch(f));
            var offenders = files.SelectMany(f => File.ReadLines(f).Select((line, i) => (f, line, i)))
                .Where(x => identifierParam.IsMatch(x.line))
                .Select(x => $"{Path.GetRelativePath(root, x.f)}:{x.i + 1}")
                .ToList();
            Assert.True(offenders.Count == 0, "Identifier in a URL query string: " + string.Join(", ", offenders));
        }
    }
}
