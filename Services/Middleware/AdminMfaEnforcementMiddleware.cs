using System.Threading.Tasks;
using eInvWorld.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace eInvWorld.Services.Middleware
{
    /// <summary>
    /// Enforces two-factor authentication for the Admin role using a "block until enrolled" policy:
    /// an authenticated Admin who has not yet enabled 2FA is redirected to the enrollment page until
    /// they set it up. The entire /Identity area (login, logout, account management, 2FA setup) is
    /// allowed through, so the admin can always reach the enrollment flow — there is no hard lockout.
    /// Disable in an emergency with Security:EnforceAdminMfa = false.
    /// </summary>
    public sealed class AdminMfaEnforcementMiddleware
    {
        private const string EnrollPath = "/Identity/Account/Manage/EnableAuthenticator";

        private readonly RequestDelegate _next;
        private readonly bool _enabled;

        public AdminMfaEnforcementMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _enabled = config.GetValue("Security:EnforceAdminMfa", true);
        }

        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
        {
            if (_enabled
                && context.User?.Identity?.IsAuthenticated == true
                && context.User.IsInRole("Admin")
                && !IsAllowedPath(context.Request.Path))
            {
                var user = await userManager.GetUserAsync(context.User);
                // user.TwoFactorEnabled is loaded with the entity — no extra query.
                if (user != null && !user.TwoFactorEnabled)
                {
                    context.Response.Redirect(EnrollPath + "?mfaRequired=true");
                    return;
                }
            }

            await _next(context);
        }

        // Always let auth/enrollment, health, and static assets through so we can never block enrollment.
        private static bool IsAllowedPath(PathString path)
        {
            if (path.StartsWithSegments("/Identity")      // login, logout, manage, 2FA setup
                || path.StartsWithSegments("/login")
                || path.StartsWithSegments("/health")
                || path.StartsWithSegments("/assets")
                || path.StartsWithSegments("/css")
                || path.StartsWithSegments("/js")
                || path.StartsWithSegments("/lib")
                || path.StartsWithSegments("/img")
                || path.StartsWithSegments("/images"))
            {
                return true;
            }

            // Real static files are served by UseStaticFiles before this runs; anything else that merely looks
            // like a file (e.g. /api/.../x.png) is an app endpoint and must not skip the 2FA requirement.
            return false;
        }
    }
}
