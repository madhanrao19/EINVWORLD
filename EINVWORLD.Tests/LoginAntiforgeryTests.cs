using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Pins the mechanism behind Login's <c>SignOutAndShowErrorAsync</c>: when a sign-in passes the password
    /// check but fails an account check (no role, no company TIN), the form is re-shown. A form token issued
    /// while HttpContext.User is still the just-signed-in user is rejected (HTTP 400) on the next, now
    /// anonymous, Sign In; one issued after resetting HttpContext.User validates.
    /// </summary>
    public class LoginAntiforgeryTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public async Task FormToken_AfterSignOut_ValidatesOnlyWhenUserWasReset(bool resetUser, bool expectValid)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddAntiforgery(o => o.Cookie.Name = "af");
            using var provider = services.BuildServiceProvider();
            var antiforgery = provider.GetRequiredService<IAntiforgery>();

            // The POST that signed the user in, then signed them out and re-rendered the form.
            var render = new DefaultHttpContext { RequestServices = provider };
            render.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Name, "testuser") },
                "Identity.Application"));
            if (resetUser)
                render.User = new ClaimsPrincipal(new ClaimsIdentity());
            var tokens = antiforgery.GetAndStoreTokens(render);

            // The next Sign In click: same browser, auth cookie gone, so the request is anonymous.
            var retry = new DefaultHttpContext { RequestServices = provider };
            retry.Request.Method = "POST";
            retry.Request.Headers.Cookie = $"af={tokens.CookieToken}";
            retry.Request.Headers[tokens.HeaderName!] = tokens.RequestToken;

            Assert.Equal(expectValid, await antiforgery.IsRequestValidAsync(retry));
        }
    }
}
