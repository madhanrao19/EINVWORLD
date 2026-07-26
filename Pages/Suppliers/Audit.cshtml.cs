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
    public class AuditModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICompanyAuthorizationService _companyAuth;

        public AuditModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ICompanyAuthorizationService companyAuth) : base(context)
        {
            _context = context;
            _userManager = userManager;
            _companyAuth = companyAuth;
        }

        public PartyInfo PartyInfo { get; set; } = default!;
        public bool CanViewAudit { get; set; }
        public bool IsAdmin { get; set; }
        public List<AuditLog> Entries { get; set; } = new();
        public List<string> AvailableActions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? From { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ActionFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        private const int PageSize = 25;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            PartyInfo = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == id) ?? null!;
            if (PartyInfo == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            IsAdmin = User.IsInRole("Admin");

            if (!IsAdmin)
            {
                var isMember = await _context.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.PartyInfoId == id);
                if (!isMember) return Forbid();
            }

            CanViewAudit = IsAdmin || (userId != null && await _companyAuth.HasPermissionAsync(userId, id.Value, CompanyPermission.ViewAudit));
            if (!CanViewAudit) return Page();

            var query = _context.Set<AuditLog>().Where(a => a.Tin == PartyInfo.TIN);

            AvailableActions = await query.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();

            if (!string.IsNullOrEmpty(ActionFilter))
            {
                query = query.Where(a => a.Action == ActionFilter);
            }

            TotalRecords = await query.CountAsync();
            TotalPages = (int)System.Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            Entries = await query
                .OrderByDescending(a => a.CreatedAtUtc)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return Page();
        }
    }
}
