// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace eInvWorld.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : SecureFormPageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly ILogger<ForgotPasswordModel> _logger;
        private readonly IConfiguration _configuration;


        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, EmailService emailService, IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<ForgotPasswordModel> logger,
        IConfiguration configuration)
        : base(config, httpClientFactory, logger) // ✅ Required for base class
        {
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        private string GenerateForgotPasswordEmailBody(string resetLink)
        {
            string template = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "ForgotPasswordEmailTemplate.html"));

            string baseUrl = _configuration["EmailConfiguration:EmailBaseUrls:BaseUrl"];
            string accountUrl = _configuration["EmailConfiguration:EmailBaseUrls:AccountBaseUrl"];
            string contactUrl = _configuration["EmailConfiguration:EmailBaseUrls:ContactBaseUrl"];

            return template
                .Replace("{{ResetLink}}", resetLink)
                .Replace("{{LogoUrl}}", "cid:einvworld-logo")
                .Replace("{{AccountLink}}", $"{baseUrl.TrimEnd('/')}/{accountUrl.TrimStart('/')}")
                .Replace("{{ContactLink}}", $"{baseUrl.TrimEnd('/')}/{contactUrl.TrimStart('/')}")
                .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());
        }



        public async Task<IActionResult> OnPostAsync()
        {
            // 🛡 Shared Honeypot & Turnstile check
            if (IsHoneypotTriggered())
            {
                _logger.LogWarning("🚫 Honeypot triggered on login.");
                ModelState.AddModelError(string.Empty, "Bot detection triggered.");
                return Page();
            }

            if (!await IsTurnstileValidAsync())
            {
                _logger.LogWarning("🚫 Turnstile verification failed on login.");
                ModelState.AddModelError(string.Empty, "Turnstile verification failed.");
                return Page();
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                // 🟢 CHANGE: Add "email = Input.Email" to the values object
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code, email = Input.Email },
                    protocol: Request.Scheme);

                string emailBody = GenerateForgotPasswordEmailBody(callbackUrl);
                await _emailService.SendEmail(Input.Email, "Reset Your Password", emailBody);

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}
