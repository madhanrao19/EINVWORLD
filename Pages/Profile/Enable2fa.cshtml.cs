using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using eInvWorld.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Pages.Profile
{
    [Authorize]
    public class Enable2faModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager; // Added for refreshing sign-in
        private readonly ILogger<Enable2faModel> _logger;
        private readonly UrlEncoder _urlEncoder;

        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

        public Enable2faModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<Enable2faModel> logger,
            UrlEncoder urlEncoder)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _urlEncoder = urlEncoder;
        }

        public bool Is2faEnabled { get; set; }
        public int RecoveryCodesLeft { get; set; }
        public string SharedKey { get; set; } = null!;
        public string AuthenticatorUri { get; set; } = null!;

        [TempData]
        public string[] RecoveryCodes { get; set; } = null!;

        [BindProperty]
        public InputModel Input { get; set; } = null!;

        public class InputModel
        {
            [Required(ErrorMessage = "Please enter the code.")]
            [StringLength(7, MinimumLength = 6)]
            [DataType(DataType.Text)]
            [Display(Name = "Verification Code")]
            public string Code { get; set; } = null!;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);

            if (!Is2faEnabled)
            {
                await LoadSharedKeyAndQrCodeUriAsync(user);
            }

            return Page();
        }

        // Handler: Enable 2FA (Verify Code)
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            if (!ModelState.IsValid)
            {
                await LoadSharedKeyAndQrCodeUriAsync(user);
                return Page();
            }

            var verificationCode = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

            if (!isValid)
            {
                ModelState.AddModelError("Input.Code", "Verification code is invalid.");
                await LoadSharedKeyAndQrCodeUriAsync(user);
                return Page();
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            _logger.LogInformation("User with ID '{UserId}' has enabled 2FA.", user.Id);

            if (await _userManager.CountRecoveryCodesAsync(user) == 0)
            {
                var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                RecoveryCodes = (recoveryCodes ?? Enumerable.Empty<string>()).Where(x => x != null).Select(x => x!).ToArray();
            }

            TempData["SuccessMessage"] = "Two-factor authentication enabled.";
            return RedirectToPage(); // Reload page to show "Enabled" view
        }

        // Handler: Disable 2FA
        public async Task<IActionResult> OnPostDisableAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Unexpected error occurred disabling 2FA.";
                return RedirectToPage();
            }

            _logger.LogInformation("User with ID '{UserId}' has disabled 2FA.", user.Id);
            TempData["SuccessMessage"] = "2FA has been disabled.";

            // CHANGED: Redirect to the main Profile page
            return RedirectToPage("/Profile/Index");
        }

        // Handler: Generate New Recovery Codes
        public async Task<IActionResult> OnPostGenerateCodesAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            RecoveryCodes = (recoveryCodes ?? Enumerable.Empty<string>()).Where(x => x != null).Select(x => x!).ToArray();

            _logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", user.Id);
            TempData["SuccessMessage"] = "You have generated new recovery codes.";
            return RedirectToPage();
        }

        private async Task LoadSharedKeyAndQrCodeUriAsync(ApplicationUser user)
        {
            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }
            SharedKey = FormatKey(unformattedKey);
            var email = await _userManager.GetEmailAsync(user);
            AuthenticatorUri = GenerateQrCodeUri(email, unformattedKey);
        }

        private string FormatKey(string? unformattedKey)
        {
            if (string.IsNullOrEmpty(unformattedKey)) return string.Empty;
            var result = new StringBuilder();
            int currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length) result.Append(unformattedKey.AsSpan(currentPosition));
            return result.ToString().ToLowerInvariant();
        }

        private string GenerateQrCodeUri(string? email, string? unformattedKey)
        {
            return string.Format(CultureInfo.InvariantCulture, AuthenticatorUriFormat,
                _urlEncoder.Encode("eInvWorld"), _urlEncoder.Encode(email ?? ""), unformattedKey);
        }
    }
}