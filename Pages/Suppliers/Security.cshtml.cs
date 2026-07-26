using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Suppliers
{
    [Authorize(Roles = "Admin,Supplier")]
    public class SecurityModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICompanyAuthorizationService _companyAuth;

        public SecurityModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ICompanyAuthorizationService companyAuth) : base(context)
        {
            _context = context;
            _userManager = userManager;
            _companyAuth = companyAuth;
        }

        public PartyInfo PartyInfo { get; set; } = default!;
        public bool CanViewTeamSecurity { get; set; }

        public bool CurrentUserTwoFactorEnabled { get; set; }

        public List<(string FullName, string Email, bool TwoFactorEnabled)> TeamSecurityStatus { get; set; } = new();
        public List<AuditLog> RecentActivity { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? From { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            PartyInfo = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == id) ?? null!;
            if (PartyInfo == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var userId = currentUser?.Id;
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var isMember = await _context.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.PartyInfoId == id);
                if (!isMember) return Forbid();
            }

            if (currentUser != null)
            {
                CurrentUserTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(currentUser);
            }

            CanViewTeamSecurity = isAdmin || (userId != null && await _companyAuth.HasPermissionAsync(userId, id.Value, CompanyPermission.ViewAudit));

            if (CanViewTeamSecurity)
            {
                var members = await _context.UserCompanies
                    .Include(uc => uc.User)
                    .Where(uc => uc.PartyInfoId == id)
                    .ToListAsync();

                foreach (var m in members)
                {
                    bool enabled = await _userManager.GetTwoFactorEnabledAsync(m.User);
                    TeamSecurityStatus.Add((m.User.FullName, m.User.Email ?? "", enabled));
                }

                RecentActivity = await _context.Set<AuditLog>()
                    .Where(a => a.Tin == PartyInfo.TIN
                        && (a.Action.StartsWith("Company.") || a.Action == "LoginSucceeded" || a.Action == "LoginFailed"))
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .Take(25)
                    .ToListAsync();
            }

            return Page();
        }
    }
}
