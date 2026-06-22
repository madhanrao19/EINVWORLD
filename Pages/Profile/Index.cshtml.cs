using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace eInvWorld.Pages.Profile
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(UserManager<ApplicationUser> userManager, IConfiguration configuration, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context, ILogger<IndexModel> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
        }

        public ApplicationUser UserProfile { get; set; } = null!;
        public string UserTIN { get; set; } = null!;
        public IList<PartyInfo> UserCompanies { get; set; } = new List<PartyInfo>();
        public PartyInfo? PrimaryCompany { get; set; }

        public IList<string> Roles { get; set; } = new List<string>();

        [BindProperty]
        public InputModel Input { get; set; } = null!;

        public string LoginMode { get; set; } = null!; // Will be either "Taxpayer" or "Intermediary"
        public bool Is2faEnabled { get; set; }
        public bool HasAuthenticator { get; set; }
        public int RecoveryCodesLeft { get; set; }
        public bool IsMachineRemembered { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Full name is required")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters")]
            public string FullName { get; set; } = null!;

            [Phone(ErrorMessage = "Please enter a valid phone number")]
            public string PhoneNumber { get; set; } = null!;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            // Fetch user's associated companies with full details
            var userCompanyData = await _context.UserCompanies
                .Where(u => u.UserId == user.Id)
                .Include(u => u.PartyInfo)
                .ThenInclude(p => p.State)
                .Include(u => u.PartyInfo)
                .ThenInclude(p => p.Country)
                .Include(u => u.PartyInfo)
                .ThenInclude(p => p.RegType)
                .ToListAsync();

            UserCompanies = userCompanyData.Select(uc => uc.PartyInfo).ToList();
            PrimaryCompany = userCompanyData.FirstOrDefault(uc => uc.IsPrimaryCompany)?.PartyInfo 
                          ?? UserCompanies.FirstOrDefault();

            var userTIN = PrimaryCompany?.TIN;

            UserProfile = user;
            UserTIN = userTIN ?? "";

            Roles = await _userManager.GetRolesAsync(user);

            //var userTIN = HttpContext.Session.GetString("UserTIN");
            var systemTIN = _configuration["LHDNApiConfig:OnBehalfOf"];

            if (string.IsNullOrWhiteSpace(userTIN) || string.IsNullOrWhiteSpace(systemTIN))
            {
                LoginMode = "Unknown";
            }
            else
            {
                LoginMode = userTIN != systemTIN ? "Intermediary (on behalf)" : "Taxpayer";
            }


            Input = new InputModel
            {
                FullName = user.FullName ?? "",
                PhoneNumber = user.PhoneNumber ?? ""
            };
            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null;
            IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user);
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) 
            {
                TempData["ErrorMessage"] = "User not found.";
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var user2 = await _userManager.GetUserAsync(User);
                UserProfile = user2!;
                Roles = await _userManager.GetRolesAsync(user2!);

                // Reload company data for display
                var userCompanyData = await _context.UserCompanies
                    .Where(u => u.UserId == user2!.Id)
                    .Include(u => u.PartyInfo)
                    .ThenInclude(p => p.State)
                    .Include(u => u.PartyInfo)
                    .ThenInclude(p => p.Country)
                    .Include(u => u.PartyInfo)
                    .ThenInclude(p => p.RegType)
                    .ToListAsync();

                UserCompanies = userCompanyData.Select(uc => uc.PartyInfo).ToList();
                PrimaryCompany = userCompanyData.FirstOrDefault(uc => uc.IsPrimaryCompany)?.PartyInfo
                              ?? UserCompanies.FirstOrDefault()!;

                var userTIN = PrimaryCompany?.TIN;
                UserTIN = userTIN ?? "";
                
                var systemTIN = _configuration["LHDNApiConfig:OnBehalfOf"];
                LoginMode = userTIN != systemTIN ? "Intermediary (on behalf)" : "Taxpayer";
                
                return Page();
            }

            try
            {
                user.FullName = Input.FullName;
                user.PhoneNumber = Input.PhoneNumber;

                // ADD AUDIT FIELDS
                user.UpdatedBy = user.Email ?? "User"; // Record their own email
                user.UpdatedDate = DateTime.Now;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Profile updated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update profile: " + string.Join(", ", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for the current user.");
                TempData["ErrorMessage"] = "An error occurred while updating your profile.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync(string OldPassword, string NewPassword, string ConfirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) 
            {
                TempData["ErrorMessage"] = "User not found.";
                return NotFound();
            }

            if (string.IsNullOrEmpty(OldPassword) || string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmPassword))
            {
                TempData["ErrorMessage"] = "All password fields are required.";
                return RedirectToPage();
            }

            if (NewPassword != ConfirmPassword)
            {
                TempData["ErrorMessage"] = "New password and confirmation password do not match.";
                return RedirectToPage();
            }

            if (NewPassword.Length < 6)
            {
                TempData["ErrorMessage"] = "Password must be at least 6 characters long.";
                return RedirectToPage();
            }

            try
            {
                var result = await _userManager.ChangePasswordAsync(user, OldPassword, NewPassword);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Password changed successfully!";
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    TempData["ErrorMessage"] = "Failed to change password: " + errors;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for the current user.");
                TempData["ErrorMessage"] = "An error occurred while changing your password.";
            }

            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostForgetTwoFactorClientAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _signInManager.ForgetTwoFactorClientAsync();
                await _signInManager.SignOutAsync(); // Optional: force re-login or just refresh
                return RedirectToPage();
            }
            return Page();
        }
    }

}
