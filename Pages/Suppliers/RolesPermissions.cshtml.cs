using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Suppliers
{
    [Authorize(Roles = "Admin,Supplier")]
    public class RolesPermissionsModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICompanyAuthorizationService _companyAuth;
        private readonly IAuditService _auditService;

        public RolesPermissionsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ICompanyAuthorizationService companyAuth, IAuditService auditService) : base(context)
        {
            _context = context;
            _userManager = userManager;
            _companyAuth = companyAuth;
            _auditService = auditService;
        }

        public PartyInfo PartyInfo { get; set; } = default!;
        public List<UserCompany> Memberships { get; set; } = new();
        public Dictionary<string, string> IdentityRoleByUserId { get; set; } = new();
        public List<CompanyRole> AvailableRoles { get; set; } = new();
        public bool CanManageUsers { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? From { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            PartyInfo = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == id) ?? null!;
            if (PartyInfo == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            // Same tenant guard used by Details/Edit — a non-admin must be a member of this company.
            if (!isAdmin)
            {
                var isMember = await _context.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.PartyInfoId == id);
                if (!isMember) return Forbid();
            }

            CanManageUsers = isAdmin || (userId != null && await _companyAuth.HasPermissionAsync(userId, id.Value, CompanyPermission.ManageUsers));

            // System-wide roles (Owner/Admin/Editor/Viewer) plus any custom role this company created.
            AvailableRoles = await _context.CompanyRoles
                .Where(r => r.PartyInfoId == null || r.PartyInfoId == id)
                .OrderBy(r => r.CompanyRoleId)
                .ToListAsync();

            Memberships = await _context.UserCompanies
                .Include(uc => uc.User)
                .Include(uc => uc.CompanyRole)
                .Where(uc => uc.PartyInfoId == id)
                .ToListAsync();

            foreach (var m in Memberships)
            {
                var roles = await _userManager.GetRolesAsync(m.User);
                IdentityRoleByUserId[m.UserId] = roles.FirstOrDefault() ?? "User";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAssignRoleAsync(int partyInfoId, int userCompanyId, int? companyRoleId)
        {
            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                bool allowed = userId != null && await _companyAuth.HasPermissionAsync(userId, partyInfoId, CompanyPermission.ManageUsers);
                if (!allowed)
                {
                    TempData["ErrorMessage"] = "You do not have permission to manage roles for this company.";
                    return RedirectToPage(new { id = partyInfoId });
                }
            }

            var membership = await _context.UserCompanies.FirstOrDefaultAsync(uc => uc.Id == userCompanyId && uc.PartyInfoId == partyInfoId);
            if (membership == null)
            {
                TempData["ErrorMessage"] = "Membership not found.";
                return RedirectToPage(new { id = partyInfoId });
            }

            if (companyRoleId.HasValue)
            {
                bool roleExists = await _context.CompanyRoles.AnyAsync(r => r.CompanyRoleId == companyRoleId.Value);
                if (!roleExists)
                {
                    TempData["ErrorMessage"] = "Selected role does not exist.";
                    return RedirectToPage(new { id = partyInfoId });
                }
            }

            var oldRoleId = membership.CompanyRoleId;
            membership.CompanyRoleId = companyRoleId;
            await _context.SaveChangesAsync();

            var companyTin = await _context.PartyInfos.Where(p => p.PartyInfoId == partyInfoId).Select(p => p.TIN).FirstOrDefaultAsync();
            await _auditService.WriteAsync("Company.RoleAssigned", new AuditEntry
            {
                Tin = companyTin,
                OldValueJson = System.Text.Json.JsonSerializer.Serialize(new { CompanyRoleId = oldRoleId }),
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { PartyInfoId = partyInfoId, TargetUserId = membership.UserId, CompanyRoleId = companyRoleId }),
            });

            TempData["SuccessMessage"] = "Role updated.";
            return RedirectToPage(new { id = partyInfoId });
        }

        /// <summary>Creates a custom role scoped to this company only — never touches the shared
        /// system-wide roles (Owner/Admin/Editor/Viewer) other companies also use.</summary>
        public async Task<IActionResult> OnPostCreateRoleAsync(int partyInfoId, string name,
            bool canManageUsers, bool canEditProfile, bool canManageBranding, bool canViewAudit)
        {
            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                bool allowed = userId != null && await _companyAuth.HasPermissionAsync(userId, partyInfoId, CompanyPermission.ManageUsers);
                if (!allowed)
                {
                    TempData["ErrorMessage"] = "You do not have permission to manage roles for this company.";
                    return RedirectToPage(new { id = partyInfoId });
                }
            }

            name = (name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                TempData["ErrorMessage"] = "Enter a role name.";
                return RedirectToPage(new { id = partyInfoId });
            }

            bool nameTaken = await _context.CompanyRoles
                .AnyAsync(r => (r.PartyInfoId == null || r.PartyInfoId == partyInfoId) && r.Name == name);
            if (nameTaken)
            {
                TempData["ErrorMessage"] = "A role with this name already exists for this company.";
                return RedirectToPage(new { id = partyInfoId });
            }

            _context.CompanyRoles.Add(new CompanyRole
            {
                Name = name,
                CanManageUsers = canManageUsers,
                CanEditProfile = canEditProfile,
                CanManageBranding = canManageBranding,
                CanViewAudit = canViewAudit,
                IsSystemDefined = false,
                PartyInfoId = partyInfoId,
            });
            await _context.SaveChangesAsync();

            var companyTin = await _context.PartyInfos.Where(p => p.PartyInfoId == partyInfoId).Select(p => p.TIN).FirstOrDefaultAsync();
            await _auditService.WriteAsync("Company.RoleCreated", new AuditEntry
            {
                Tin = companyTin,
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { PartyInfoId = partyInfoId, RoleName = name }),
            });

            TempData["SuccessMessage"] = $"Role \"{name}\" created.";
            return RedirectToPage(new { id = partyInfoId });
        }

        /// <summary>Deletes a custom role this company created. System-wide roles and roles belonging
        /// to another company can never be removed here. Members holding the role fall back to
        /// "no role" (legacy access flags), same as unassigning it.</summary>
        public async Task<IActionResult> OnPostDeleteRoleAsync(int partyInfoId, int companyRoleId)
        {
            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                bool allowed = userId != null && await _companyAuth.HasPermissionAsync(userId, partyInfoId, CompanyPermission.ManageUsers);
                if (!allowed)
                {
                    TempData["ErrorMessage"] = "You do not have permission to manage roles for this company.";
                    return RedirectToPage(new { id = partyInfoId });
                }
            }

            var role = await _context.CompanyRoles.FirstOrDefaultAsync(r => r.CompanyRoleId == companyRoleId);
            if (role == null || role.IsSystemDefined || role.PartyInfoId != partyInfoId)
            {
                TempData["ErrorMessage"] = "This role cannot be deleted.";
                return RedirectToPage(new { id = partyInfoId });
            }

            // Unassign from any member currently holding it — same effect as picking "-- No role --".
            var holders = await _context.UserCompanies.Where(uc => uc.CompanyRoleId == companyRoleId).ToListAsync();
            foreach (var m in holders) m.CompanyRoleId = null;

            _context.CompanyRoles.Remove(role);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Role \"{role.Name}\" deleted.";
            return RedirectToPage(new { id = partyInfoId });
        }
    }
}
