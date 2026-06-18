using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using eInvWorld.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using eInvWorld.Data;
using eInvWorld.Models.Audit;
using System.ComponentModel.DataAnnotations;
using System;

namespace eInvWorld.Pages.Admin.Users
{
    [Authorize(Roles = "Admin")]
    public class AddEditUserModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AddEditUserModel> _logger;

        public AddEditUserModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<ApplicationUser> userStore,
            ApplicationDbContext db,
            ILogger<AddEditUserModel> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _userStore = userStore;
            _emailStore = (IUserEmailStore<ApplicationUser>)userStore;
            _db = db;
            _logger = logger;
        }

        [BindProperty]
        public ApplicationUser AppUser { get; set; } = null!;

        [BindProperty]
        [Required(ErrorMessage = "Role selection is required")]
        public string SelectedRole { get; set; } = null!;

        [BindProperty]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Password and confirmation password do not match")]
        public string? ConfirmPassword { get; set; }

        public IList<IdentityRole> AllRoles { get; set; } = new List<IdentityRole>();

        [BindProperty(SupportsGet = true)]
        public bool EnablePasswordChange { get; set; } = false;

        public async Task<IActionResult> OnGetAsync(string? id)
        {
            AllRoles = await _roleManager.Roles.ToListAsync();

            if (string.IsNullOrWhiteSpace(id))
            {
                AppUser = new ApplicationUser { Id = string.Empty };
                return Page();
            }

            var foundUser = await _userManager.FindByIdAsync(id);
            if (foundUser == null)
                return NotFound();

            AppUser = foundUser;


            SelectedRole = (await _userManager.GetRolesAsync(AppUser)).FirstOrDefault() ?? string.Empty;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            AllRoles = await _roleManager.Roles.ToListAsync();

            if (Request.Form["TriggerPasswordChange"] == "true")
            {
                EnablePasswordChange = true;
                ModelState.Clear();
                return Page();
            }

            // Custom validation for password fields
            ValidatePasswordFields();

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(ms => ms.Value?.Errors.Any() == true)
                    .Select(ms => new { Field = ms.Key, Errors = ms.Value!.Errors.Select(e => e.ErrorMessage).ToList() })

                    .ToList();

                _logger.LogWarning("❌ Model validation failed. Errors: {@ValidationErrors}", errors);
                return Page();
            }

            ApplicationUser? user = null;
            var appUserId = AppUser.Id;

            bool isNew = string.IsNullOrWhiteSpace(appUserId) || !_userManager.Users.Any(u => u.Id == appUserId);

            if (isNew)
            {
                user = new ApplicationUser
                {
                    FullName = AppUser.FullName,
                    IsApproved = true, // Auto-approve new users created by admin
                    IsActive = true,
                    EmailConfirmed = true,
                    UserType = SelectedRole,
                    // ✅ ADD AUDIT FIELDS
                    UpdatedBy = User.Identity?.Name ?? "Admin",
                    UpdatedDate = DateTime.Now
                };

                // Password validation is now handled in ValidatePasswordFields method

                await _userStore.SetUserNameAsync(user, AppUser.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, AppUser.Email, CancellationToken.None);

                var createResult = await _userManager.CreateAsync(user, Password ?? "");
                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                        ModelState.AddModelError("", error.Description);
                    return Page();
                }

                Log.Information("✅ Admin '{Admin}' created user '{User}' with role '{Role}'",
                    HttpContext.User.Identity?.Name ?? "Unknown", user.Email, SelectedRole);

                _db.UserActivityLogs.Add(new UserActivityLog
                {
                    UserId = _userManager.GetUserId(HttpContext.User) ?? "unknown",
                    UserName = _userManager.GetUserName(HttpContext.User) ?? "unknown",
                    Action = "CreateUser",
                    Module = "UserManagement",
                    Data = $"{{ \"Email\": \"{user.Email}\", \"Role\": \"{SelectedRole}\" }}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    Timestamp = DateTime.Now
                });
            }
            else
            {
                user = await _userManager.FindByIdAsync(AppUser.Id);
                if (user == null)
                    return NotFound();

                user.UserName = AppUser.UserName;
                user.Email = AppUser.Email;
                user.FullName = AppUser.FullName;
                user.IsApproved = AppUser.IsApproved;
                user.UserType = SelectedRole;
                user.IsActive = AppUser.IsActive;

                // ✅ ADD AUDIT FIELDS
                user.UpdatedBy = User.Identity?.Name ?? "Admin";
                user.UpdatedDate = DateTime.Now;

                // Handle password change for existing users
                if (EnablePasswordChange && !string.IsNullOrWhiteSpace(Password))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var result = await _userManager.ResetPasswordAsync(user, token, Password);
                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                            ModelState.AddModelError("", $"Password update failed: {error.Description}");
                        return Page();
                    }
                }

                await _userManager.UpdateAsync(user);

                Log.Information("✏️ Admin '{Admin}' updated user '{User}' (ID: {Id})",
                    HttpContext.User.Identity?.Name ?? "Unknown", user.Email, user.Id);

                _db.UserActivityLogs.Add(new UserActivityLog
                {
                    UserId = _userManager.GetUserId(HttpContext.User) ?? "unknown",
                    UserName = _userManager.GetUserName(HttpContext.User) ?? "unknown",
                    Action = "EditUser",
                    Module = "UserManagement",
                    Data = $"{{ \"Email\": \"{user.Email}\", \"Role\": \"{SelectedRole}\" }}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    Timestamp = DateTime.Now
                });

            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!string.IsNullOrEmpty(SelectedRole))
            {
                var assignResult = await _userManager.AddToRoleAsync(user, SelectedRole);
                if (!assignResult.Succeeded)
                {
                    foreach (var error in assignResult.Errors)
                        ModelState.AddModelError("", error.Description);
                    return Page();
                }
            }

            await _db.SaveChangesAsync();
            return RedirectToPage("/Admin/Users/ManageUser");
        }

        private void ValidatePasswordFields()
        {
            bool isNewUser = string.IsNullOrWhiteSpace(AppUser?.Id) || !_userManager.Users.Any(u => u.Id == AppUser.Id);

            if (isNewUser)
            {
                // For new users, password is required
                if (string.IsNullOrWhiteSpace(Password))
                    ModelState.AddModelError(nameof(Password), "Password is required for new users.");
                if (string.IsNullOrWhiteSpace(ConfirmPassword))
                    ModelState.AddModelError(nameof(ConfirmPassword), "Password confirmation is required for new users.");
            }
            else if (EnablePasswordChange)
            {
                // For existing users, password is only required if they want to change it
                if (string.IsNullOrWhiteSpace(Password))
                    ModelState.AddModelError(nameof(Password), "New password is required when changing password.");
                if (string.IsNullOrWhiteSpace(ConfirmPassword))
                    ModelState.AddModelError(nameof(ConfirmPassword), "Password confirmation is required when changing password.");
            }

            // Validate password match if both are provided
            if (!string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(ConfirmPassword) && Password != ConfirmPassword)
            {
                ModelState.AddModelError(nameof(ConfirmPassword), "Password and confirmation password do not match.");
            }
        }
    }
}