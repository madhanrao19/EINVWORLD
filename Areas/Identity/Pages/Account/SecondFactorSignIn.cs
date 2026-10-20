using System;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Finishes a sign-in that went through the second factor (authenticator code or recovery code).
    /// Login.cshtml.cs hands 2FA users off before its role / company-TIN checks, so those checks — and the
    /// session state Login sets (UserTIN, AccessToken) — must run here too, or a user with no role or no
    /// company gets in just by having 2FA on.
    /// </summary>
    internal static class SecondFactorSignIn
    {
        /// <summary>Returns an error to show (caller signs the user out), or null after populating the session.</summary>
        public static async Task<string?> CompleteAsync(
            HttpContext http, UserManager<ApplicationUser> users, ApplicationDbContext db,
            ITokenService tokens, ILogger logger, ApplicationUser user)
        {
            var roles = await users.GetRolesAsync(user);
            if (roles == null || !roles.Any())
            {
                logger.LogWarning("User {UserId} passed 2FA but has no roles assigned.", user.Id);
                return "User Roles is missing. Please contact support.";
            }

            bool isAdmin = user.UserName != null &&
                user.UserName.Equals("admin@einvworld.com", StringComparison.OrdinalIgnoreCase);
            if (isAdmin)
            {
                http.Session.SetString("UserTIN", "ADMIN");
                http.Session.SetString("AccessToken", "ADMIN-TOKEN");
                return null;
            }

            var userTin = await db.UserCompanies
                .Where(uc => uc.UserId == user.Id)
                .Select(uc => uc.PartyInfo.TIN)
                .FirstOrDefaultAsync();
            if (string.IsNullOrEmpty(userTin))
            {
                logger.LogError("No TIN found for user {UserId} after 2FA login.", user.Id);
                return "Your company TIN is missing. Please contact support.";
            }

            http.Session.SetString("UserTIN", userTin);
            try
            {
                http.Session.SetString("AccessToken", await tokens.GetAccessToken());
            }
            catch (Exception ex)
            {
                // Non-fatal, as before: the user is authenticated and explicit-TIN submit paths don't need it.
                logger.LogError(ex, "Failed to retrieve access token for user {UserId} after 2FA login.", user.Id);
            }
            return null;
        }
    }
}
