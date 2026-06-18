// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using eInvWorld.Models;
using eInvWorld.Controllers;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Services;

namespace eInvWorld.Areas.Identity.Pages.Account
{
    public class LoginModel : SecureFormPageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly ITokenService _tokenService; // Add ITokenService as a dependency
        private readonly IOptions<TaxpayerValidationSettings> _taxpayerValidationSettings;
        private readonly ILHDNApiService _lhdnApiService; // Inject the actual service
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;


        public LoginModel(
             SignInManager<ApplicationUser> signInManager,
             ILogger<LoginModel> logger,
             ITokenService tokenService,
             IOptions<TaxpayerValidationSettings> taxpayerValidationSettings,
             ILHDNApiService lhdnApiService,
             ApplicationDbContext context,
             IConfiguration config,
             IHttpClientFactory httpClientFactory
        ) : base(config, httpClientFactory, logger) // ✅ Inject shared
        {
            _signInManager = signInManager;
            _logger = logger;
            _tokenService = tokenService;
            _taxpayerValidationSettings = taxpayerValidationSettings;
            _lhdnApiService = lhdnApiService;
            _context = context;
            _config = config;
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
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

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
            /// [Required]
            [Required]
            public string UserName { get; set; }

            //[Required]
            //[EmailAddress]
            //public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public string access_token { get; set; }


        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/Dashboard/Dashboard");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // ✅ Detect if the request is from an email
            bool isFromEmail = Request.Query.ContainsKey("fromEmail") && Request.Query["fromEmail"] == "true";
            string documentId = Request.Query.ContainsKey("documentId") ? Request.Query["documentId"].ToString() : null;


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
                // 1. Fetch User (Declaration #1 - Keep this one)
                var user = await _signInManager.UserManager.FindByNameAsync(Input.UserName);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid username/password.");
                    return Page();
                }

                // Check if the user is approved
                if (!user.IsApproved)
                {
                    ModelState.AddModelError(string.Empty, "Your account is not approved. Please contact the administrator.");
                    return Page();
                }

                // Check if the user is active
                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account is inactive. Please contact the administrator.");
                    return Page();
                }

                // ✅ Prepare activity log metadata before login attempt
                string logData = $"Username: {Input.UserName}, RememberMe: {Input.RememberMe}";

                // ✅ Authenticate the user FIRST
                var result = await _signInManager.PasswordSignInAsync(Input.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                // -----------------------------------------------------------
                // 1️⃣ CHECK FOR 2FA FIRST (Priority Check)
                // -----------------------------------------------------------
                if (result.RequiresTwoFactor)
                {
                    await UserActivityLogger.LogAsync(
                        _context, HttpContext,
                        action: "Login2FARequired",
                        module: "Authentication",
                        data: logData
                    );
                    // This redirects specifically to the 2FA input page
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }

                // -----------------------------------------------------------
                // 2️⃣ CHECK FOR LOCKOUT
                // -----------------------------------------------------------
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    await UserActivityLogger.LogAsync(
                        _context, HttpContext,
                        action: "LoginLockedOut",
                        module: "Authentication",
                        data: logData
                    );
                    return RedirectToPage("./Lockout");
                }

                // -----------------------------------------------------------
                // 3️⃣ CHECK FOR SUCCESS
                // -----------------------------------------------------------
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} logged in successfully.", Input.UserName);

                    // 🟢 NEW VALIDATION: Check if user has roles
                    var roles = await _signInManager.UserManager.GetRolesAsync(user);
                    if (roles == null || !roles.Any())
                    {
                        _logger.LogWarning("User {UserName} attempted login but has no roles assigned.", Input.UserName);

                        // Immediately sign them out to invalidate the session
                        await _signInManager.SignOutAsync();

                        ModelState.AddModelError(string.Empty, "User Roles is missing. Please contact support.");
                        return Page();
                    }

                    // ✅ Retrieve user's assigned TIN after successful login
                    var userTin = await _context.UserCompanies
                        .Where(uc => uc.UserId == user.Id)
                        .Include(uc => uc.PartyInfo)
                        .Select(uc => uc.PartyInfo.TIN)
                        .FirstOrDefaultAsync();

                    bool isAdmin = user.UserName.Equals("admin@einvworld.com", StringComparison.OrdinalIgnoreCase);

                    if (isAdmin)
                    {
                        _logger.LogInformation("Admin login: skipping TIN check.");
                        HttpContext.Session.SetString("UserTIN", "ADMIN");
                        HttpContext.Session.SetString("AccessToken", "ADMIN-TOKEN");
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(userTin))
                        {
                            _logger.LogError("No TIN found for user {UserName}.", Input.UserName);
                            // Sign out if TIN is missing to prevent partial login
                            await _signInManager.SignOutAsync();
                            ModelState.AddModelError(string.Empty, "Your company TIN is missing. Please contact support.");
                            return Page();
                        }
                        _logger.LogInformation("User {UserName} is assigned to TIN: {TIN}", Input.UserName, userTin);
                        HttpContext.Session.SetString("UserTIN", userTin);

                        try
                        {
                            var accessToken = await _tokenService.GetAccessToken();
                            HttpContext.Session.SetString("AccessToken", accessToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Failed to retrieve access token for user {UserName}.", Input.UserName);
                            ModelState.AddModelError(string.Empty, $"Login succeeded but failed to retrieve system token: {ex.Message}");
                            return Page();
                        }
                    }

                    // ✅ Log login activity
                    await UserActivityLogger.LogAsync(
                        _context,
                        HttpContext,
                        action: "Login",
                        module: "Authentication",
                        data: isAdmin ? "User: admin@einvworld.com" : $"TIN: {userTin}"
                    );

                    // ✅ If login comes from email, redirect to InvoiceDetails
                    if (isFromEmail && !string.IsNullOrEmpty(documentId))
                    {
                        string invoiceRedirectUrl = $"/Invoices/InvoiceDetails?fromEmail=true&documentId={documentId}";
                        _logger.LogInformation("🔄 Redirecting {UserName} to: {RedirectUrl}", Input.UserName, invoiceRedirectUrl);
                        return LocalRedirect(invoiceRedirectUrl);
                    }

                    return LocalRedirect(returnUrl);
                }

                // -----------------------------------------------------------
                // 4️⃣ HANDLE FAILURE (If we reached here, login failed)
                // -----------------------------------------------------------
                await UserActivityLogger.LogAsync(
                    _context, HttpContext,
                    action: "LoginFailed",
                    module: "Authentication",
                    data: logData
                );
                _logger.LogWarning("Invalid login attempt for user {UserName}.", Input.UserName);

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
