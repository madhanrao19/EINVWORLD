// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfirmEmailModel> _logger;

        public ConfirmEmailModel(
            UserManager<ApplicationUser> userManager,
            EmailService emailService,
            IConfiguration configuration,
            ILogger<ConfirmEmailModel> logger)
        {
            _userManager = userManager;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }
        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            var wasConfirmed = user.EmailConfirmed;
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);
            StatusMessage = result.Succeeded ? "Thank you for confirming your email." : "Error confirming your email.";

            // Alert admins once, on the first successful confirmation of a self-registered, not-yet-approved account
            // (re-clicking the link must not re-send it).
            if (result.Succeeded && !wasConfirmed && !user.IsApproved)
            {
                await NotifyAdminsOfPendingRegistrationAsync(user);
            }
            return Page();
        }

        private async Task NotifyAdminsOfPendingRegistrationAsync(ApplicationUser user)
        {
            try
            {
                var receivers = (_configuration["EmailConfiguration:NewUserRegistrationEmailSettings:ReceiverEmail"] ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var adminEmails = (await _userManager.GetUsersInRoleAsync("Admin"))
                    .Where(a => a.IsApproved && a.EmailConfirmed && !string.IsNullOrWhiteSpace(a.Email))
                    .Select(a => a.Email);
                var recipients = receivers.Concat(adminEmails).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (recipients.Count == 0)
                {
                    _logger.LogWarning("New user registration confirmed for {UserId} but no admin recipients are configured.", user.Id);
                    return;
                }

                var baseUrl = (_configuration["EmailConfiguration:EmailBaseUrls:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}/").TrimEnd('/');
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
                var enc = HtmlEncoder.Default;
                var body = await _emailService.LoadEmailTemplate("NewUserRegistration/AdminEmailTemplate.html", new Dictionary<string, string>
                {
                    { "Name", enc.Encode(user.FullName ?? "") },
                    { "Email", enc.Encode(user.Email ?? "") },
                    { "Phone", enc.Encode(user.PhoneNumber ?? "") },
                    { "Position", enc.Encode(user.Position ?? "") },
                    { "ReviewUrl", enc.Encode($"{baseUrl}/Admin/Users/ManageUser") },
                    { "Timestamp", now.ToString("dd/MM/yyyy hh:mm tt") },
                    { "Year", now.Year.ToString() }
                });
                var subject = _configuration["EmailConfiguration:NewUserRegistrationEmailSettings:Subject"]
                              ?? "eInvWorld | New User Registration Pending Approval";

                // First recipient is To; the rest are BCC so admin addresses aren't exposed to each other.
                await _emailService.SendEmail(recipients[0], subject, body, recipients.Skip(1).ToList());
                _logger.LogInformation("New user registration alert sent for {UserId} to {RecipientCount} admin recipient(s).", user.Id, recipients.Count);
            }
            catch (Exception ex)
            {
                // Never fail the user's confirmation because the admin alert couldn't be sent.
                _logger.LogError(ex, "Failed to send new user registration alert for {UserId}.", user.Id);
            }
        }
    }
}
