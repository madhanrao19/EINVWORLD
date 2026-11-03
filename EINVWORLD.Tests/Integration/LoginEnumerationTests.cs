using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Areas.Identity.Pages.Account;
using eInvWorld.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>FINDINGS.md #9: the sign-in form gives the same answer for an unknown user and a wrong password,
    /// and account status is only shown after the correct password.</summary>
    public class LoginEnumerationTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public LoginEnumerationTests(SqlServerFixture fx) => _fx = fx;

        private sealed class TurnstileOk : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"success\":true}", System.Text.Encoding.UTF8, "application/json") });
        }

        private sealed class Clients : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(new TurnstileOk());
        }

        private const string Password = "Correct-Horse-9";

        private static async Task<string> Attempt(eInvWorld.Data.ApplicationDbContext ctx, UserManager<ApplicationUser> users, string userName, string password)
        {
            var http = new DefaultHttpContext();
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["cf-turnstile-response"] = "tok" });
            var accessor = new HttpContextAccessor { HttpContext = http };
            var options = Options.Create(new IdentityOptions());
            var signIn = new SignInManager<ApplicationUser>(users, accessor, new UserClaimsPrincipalFactory<ApplicationUser>(users, options),
                options, NullLogger<SignInManager<ApplicationUser>>.Instance,
                new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())), new DefaultUserConfirmation<ApplicationUser>());
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Turnstile:VerifyUrl"] = "https://verify.example.test/" }).Build();
            var page = new LoginModel(signIn, NullLogger<LoginModel>.Instance, null!, Options.Create(new eInvWorld.Models.TaxpayerValidationSettings()),
                null!, ctx, config, new Clients())
            {
                PageContext = new PageContext { HttpContext = http },
                Input = new LoginModel.InputModel { UserName = userName, Password = password },
            };
            await page.OnPostAsync("/"); // a returnUrl, so no IUrlHelper is needed
            return string.Join("|", page.ModelState[string.Empty]?.Errors.Select(e => e.ErrorMessage) ?? Array.Empty<string>());
        }

        [Fact]
        public async Task Unknown_User_And_Wrong_Password_Look_The_Same_And_Status_Needs_The_Password()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(ctx), Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(), null!, null!, new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
                NullLogger<UserManager<ApplicationUser>>.Instance);
            var id = await CreateUserAsync(ctx, "login-enum");
            var user = (await users.FindByIdAsync(id))!;
            user.IsApproved = false; // pending approval
            await users.UpdateAsync(user);
            await users.AddPasswordAsync(user, Password);

            var unknown = await Attempt(ctx, users, "nobody-" + Guid.NewGuid().ToString("N") + "@test.local", Password);
            var wrong = await Attempt(ctx, users, user.UserName!, "Wrong-Password-1");
            var right = await Attempt(ctx, users, user.UserName!, Password);

            Assert.Equal(LoginModel.InvalidLoginMessage, unknown);
            Assert.Equal(unknown, wrong);           // no "not approved" without the password
            Assert.Contains("not approved", right); // shown once the password is right
        }

        [Theory]
        [InlineData("DuplicateUserName", true)]
        [InlineData("DuplicateEmail", true)]
        [InlineData("PasswordTooShort", false)]
        public void Register_Treats_Only_Duplicates_As_An_Existing_Account(string code, bool duplicate) =>
            Assert.Equal(duplicate, RegisterModel.IsDuplicateError(new IdentityError { Code = code }));

        [Theory]
        [InlineData("/Identity/Account/Manage/EnableAuthenticator", true)]
        [InlineData("/assets/libs/jquery/dist/jquery.min.js", true)]
        [InlineData("/api/companies/logos/a.png", false)] // looks like a file, is an app endpoint
        [InlineData("/Invoices/InvoiceLists", false)]
        public void Admin2fa_Exempts_Only_Auth_And_Static_Folders(string path, bool allowed)
        {
            var isAllowed = typeof(eInvWorld.Services.Middleware.AdminMfaEnforcementMiddleware)
                .GetMethod("IsAllowedPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            Assert.Equal(allowed, (bool)isAllowed.Invoke(null, new object[] { new PathString(path) })!);
        }
    }
}
