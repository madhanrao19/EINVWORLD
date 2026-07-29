using eInvWorld.Data;
using eInvWorld.Models;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Admin
{
    /// <summary>
    /// Two things live on this page: (1) the global Identity role catalog (create/delete the roles
    /// that "Change Role" in Manage Users lets you assign), and (2) which app modules the Supplier and
    /// Buyer roles can access. Admin always has full access to every module and can't be deleted here.
    /// A module with no row for a role defaults to allowed — the module grid only ever adds
    /// restrictions on top of the existing per-page [Authorize(Roles=...)] gates; it never grants
    /// access those gates wouldn't already allow.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class RoleManagementModel : PageModel
    {
        public static readonly string[] ManagedRoles = { "Supplier", "Buyer" };

        /// <summary>Core roles the rest of the app's authorization is built on — never deletable.</summary>
        private static readonly string[] ProtectedRoleNames = { "Admin", "Supplier", "Buyer" };

        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoleManagementModel(ApplicationDbContext context, RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public RoleModules.Module[] Modules => RoleModules.All;
        public List<IdentityRole> AllRoles { get; private set; } = new();

        /// <summary>Keyed by "Role|ModuleKey" → currently allowed.</summary>
        public Dictionary<string, bool> Grid { get; private set; } = new();

        public async Task OnGetAsync()
        {
            AllRoles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();

            var rows = await _context.RoleModulePermissions.ToListAsync();
            foreach (var role in ManagedRoles)
            {
                foreach (var module in RoleModules.All)
                {
                    var row = rows.FirstOrDefault(r => r.RoleName == role && r.ModuleKey == module.Key);
                    Grid[$"{role}|{module.Key}"] = row?.IsAllowed ?? true;
                }
            }
        }

        /// <summary>Adds a new assignable role — becomes available immediately in Manage Users'
        /// "Change Role" list, and in the module grid above once the page is reloaded.</summary>
        public async Task<IActionResult> OnPostCreateIdentityRoleAsync(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                TempData["ErrorMessage"] = "Enter a role name.";
                return RedirectToPage();
            }

            if (await _roleManager.RoleExistsAsync(name))
            {
                TempData["ErrorMessage"] = $"Role \"{name}\" already exists.";
                return RedirectToPage();
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(name));
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                ? $"Role \"{name}\" created."
                : string.Join("; ", result.Errors.Select(e => e.Description));

            return RedirectToPage();
        }

        /// <summary>Deletes a role. Refuses to delete Admin/Supplier/Buyer (the app's authorization
        /// depends on them existing) or a role currently assigned to any user.</summary>
        public async Task<IActionResult> OnPostDeleteIdentityRoleAsync(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToPage();
            }

            if (ProtectedRoleNames.Contains(role.Name))
            {
                TempData["ErrorMessage"] = $"\"{role.Name}\" is a core role and can't be deleted.";
                return RedirectToPage();
            }

            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            if (usersInRole.Count > 0)
            {
                TempData["ErrorMessage"] = $"\"{role.Name}\" is assigned to {usersInRole.Count} user(s) — reassign them first.";
                return RedirectToPage();
            }

            await _roleManager.DeleteAsync(role);
            TempData["SuccessMessage"] = $"Role \"{role.Name}\" deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAsync(List<string>? allowed)
        {
            var allowedSet = new HashSet<string>(allowed ?? new List<string>());
            var existing = await _context.RoleModulePermissions.ToListAsync();

            foreach (var role in ManagedRoles)
            {
                foreach (var module in RoleModules.All)
                {
                    bool isAllowed = allowedSet.Contains($"{role}|{module.Key}");
                    var row = existing.FirstOrDefault(r => r.RoleName == role && r.ModuleKey == module.Key);
                    if (row == null)
                    {
                        _context.RoleModulePermissions.Add(new RoleModulePermission
                        {
                            RoleName = role,
                            ModuleKey = module.Key,
                            IsAllowed = isAllowed,
                        });
                    }
                    else
                    {
                        row.IsAllowed = isAllowed;
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Role module access updated.";
            return RedirectToPage();
        }
    }
}
