using System;
using System.Net;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Services;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Tells the account owner their password was changed, so a change they didn't make (stolen session, leaked
    /// reset link) doesn't go unnoticed. Sent after password reset, change from Manage or Profile, and an
    /// administrator reset. Never fails the password change itself.
    /// </summary>
    public static class PasswordChangedEmail
    {
        public const string Subject = "Your EINVWORLD password was changed";

        public static async Task SendAsync(EmailService email, ApplicationUser user, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) return;
            try
            {
                if (!await email.SendEmail(user.Email, Subject, Body(user.FullName, DateTime.UtcNow)))
                    logger.LogWarning("Password-changed notice could not be sent to user {UserId}.", user.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Password-changed notice could not be sent to user {UserId}.", user.Id);
            }
        }

        internal static string Body(string? fullName, DateTime utcNow)
        {
            var kl = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
            return $"<p>Hello {WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "there" : fullName)},</p>"
                 + $"<p>The password for your EINVWORLD account was changed on {kl:dd MMM yyyy, h:mm tt} (Malaysia time).</p>"
                 + "<p>If you made this change, you don't need to do anything.</p>"
                 + "<p>If you didn't, reset your password now with <b>Forgot password</b> on the sign-in page, "
                 + "then contact your administrator.</p>";
        }
    }
}
