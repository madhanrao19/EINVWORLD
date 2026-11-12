using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// v1.50.48: GET /Identity/Account/Logout signed the user out, so any other site could log a user out with a link
    /// or a redirect (logout CSRF, 12 Nov 2026 QA). Signing out now happens only on POST, which Razor Pages checks for
    /// the antiforgery token; a GET shows the confirm page. These tests keep it that way.
    /// </summary>
    public class LogoutPostOnlyTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EINVWORLD.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        [Fact]
        public void Get_Shows_The_Confirm_Page_Without_Signing_Out()
        {
            // A null SignInManager would throw if OnGet still signed out.
            var page = new eInvWorld.Areas.Identity.Pages.Account.LogoutModel(null!, NullLogger<eInvWorld.Areas.Identity.Pages.Account.LogoutModel>.Instance);
            Assert.IsType<PageResult>(page.OnGet());
        }

        [Fact]
        public void Logout_Page_Does_Not_Opt_Out_Of_Antiforgery()
        {
            var type = typeof(eInvWorld.Areas.Identity.Pages.Account.LogoutModel);
            Assert.Null(type.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>());
            Assert.All(type.GetMethods(), m => Assert.Null(m.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>()));
        }

        // Navigating to the logout URL (location.href / assign / replace, or a link) is a GET.
        private static readonly Regex GetNavigation = new(
            @"(location(\.href)?\s*=|location\.(assign|replace)\s*\(|<a\b[^>]*href\s*=)\s*['""][^'""]*Account/Logout",
            RegexOptions.IgnoreCase);
        private static readonly Regex LogoutForm = new(@"<form\b[^>]*Account/Logout[^>]*>", RegexOptions.IgnoreCase);

        [Fact]
        public void No_View_Or_Script_Logs_Out_With_A_Get()
        {
            var root = RepoRoot();
            var files = new[] { "Pages", "Areas", "Views" }.Select(d => Path.Combine(root, d)).Where(Directory.Exists)
                .SelectMany(d => Directory.EnumerateFiles(d, "*.cshtml", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "wwwroot", "js"), "*.js", SearchOption.AllDirectories))
                .ToList();
            Assert.NotEmpty(files);

            var offenders = files.SelectMany(f =>
            {
                var text = File.ReadAllText(f);
                var rel = Path.GetRelativePath(root, f);
                return GetNavigation.Matches(text).Select(m => $"{rel}: {m.Value}")
                    .Concat(LogoutForm.Matches(text).Where(m => !Regex.IsMatch(m.Value, @"method\s*=\s*[""']post[""']", RegexOptions.IgnoreCase))
                        .Select(m => $"{rel}: form without method=\"post\""));
            }).ToList();

            Assert.True(offenders.Count == 0, "Logout must be a POST form:\n" + string.Join("\n", offenders));
        }

        [Theory]
        [InlineData(null, "/Dashboard/Dashboard")]
        [InlineData("", "/Dashboard/Dashboard")]
        [InlineData("/Identity/Account/Logout?logoutReason=timeout", "/Dashboard/Dashboard")]  // expired-session idle timeout
        [InlineData("/identity/account/LOGOUT", "/Dashboard/Dashboard")]
        [InlineData("/Invoices/InvoiceLists", "/Invoices/InvoiceLists")]
        public void Sign_In_Never_Returns_To_The_Logout_Page(string? returnUrl, string expected) =>
            Assert.Equal(expected, eInvWorld.Areas.Identity.Pages.Account.LoginModel.WithoutLogoutReturn(returnUrl!) ?? "/Dashboard/Dashboard");

        [Theory]
        [InlineData("Pages/Shared/_LayoutTabler.cshtml")]
        [InlineData("Pages/Shared/_LoginLayoutTabler.cshtml")]
        [InlineData("Pages/Shared/_Layout.cshtml")]
        [InlineData("Pages/Shared/_LoginLayout.cshtml")]
        public void Idle_Timeout_Posts_The_Logout_With_The_Antiforgery_Token(string layout)
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot(), layout));
            Assert.Contains("form.method = 'post';", text);
            Assert.Contains("form.action = '/Identity/Account/Logout?logoutReason=timeout';", text);
            Assert.Contains("token.name = '__RequestVerificationToken';", text);
            Assert.Contains("@Xsrf.GetAndStoreTokens(Context).RequestToken", text);
            Assert.Contains("@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Xsrf", text);
        }
    }
}
