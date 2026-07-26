using System.Text.RegularExpressions;
using System.Threading.Tasks;
using eInvWorld.Data;
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
    public class InvoiceBrandingModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly UserManager<eInvWorld.Models.ApplicationUser> _userManager;
        private readonly ICompanyAuthorizationService _companyAuth;
        private readonly IAuditService _auditService;

        public InvoiceBrandingModel(ApplicationDbContext context, UserManager<eInvWorld.Models.ApplicationUser> userManager,
            ICompanyAuthorizationService companyAuth, IAuditService auditService) : base(context)
        {
            _context = context;
            _userManager = userManager;
            _companyAuth = companyAuth;
            _auditService = auditService;
        }

        public PartyInfo PartyInfo { get; set; } = default!;
        public bool CanManageBranding { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? From { get; set; }

        [BindProperty]
        public string? InvoiceAccentColorHex { get; set; }

        [BindProperty]
        public string? InvoiceFooterNote { get; set; }

        [BindProperty]
        public bool InvoiceShowBankDetails { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            PartyInfo = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == id) ?? null!;
            if (PartyInfo == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var isMember = await _context.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.PartyInfoId == id);
                if (!isMember) return Forbid();
            }

            CanManageBranding = isAdmin || (userId != null && await _companyAuth.HasPermissionAsync(userId, id.Value, CompanyPermission.ManageBranding));

            InvoiceAccentColorHex = PartyInfo.InvoiceAccentColorHex ?? "#006948";
            InvoiceFooterNote = PartyInfo.InvoiceFooterNote;
            InvoiceShowBankDetails = PartyInfo.InvoiceShowBankDetails;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var partyInfo = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == id);
            if (partyInfo == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                bool allowed = userId != null && await _companyAuth.HasPermissionAsync(userId, id, CompanyPermission.ManageBranding);
                if (!allowed)
                {
                    TempData["ErrorMessage"] = "You do not have permission to manage invoice branding for this company.";
                    return RedirectToPage(new { id });
                }
            }

            if (!string.IsNullOrWhiteSpace(InvoiceAccentColorHex) && !Regex.IsMatch(InvoiceAccentColorHex, "^#[0-9A-Fa-f]{6}$"))
            {
                ModelState.AddModelError(nameof(InvoiceAccentColorHex), "Enter a valid hex color, e.g. #006948.");
            }

            if (!ModelState.IsValid)
            {
                PartyInfo = partyInfo;
                CanManageBranding = true;
                return Page();
            }

            var oldSnapshot = new { partyInfo.InvoiceAccentColorHex, partyInfo.InvoiceFooterNote, partyInfo.InvoiceShowBankDetails };

            partyInfo.InvoiceAccentColorHex = InvoiceAccentColorHex;
            partyInfo.InvoiceFooterNote = InvoiceFooterNote;
            partyInfo.InvoiceShowBankDetails = InvoiceShowBankDetails;

            await _context.SaveChangesAsync();

            await _auditService.WriteAsync("Company.BrandingUpdated", new AuditEntry
            {
                Tin = partyInfo.TIN,
                OldValueJson = System.Text.Json.JsonSerializer.Serialize(oldSnapshot),
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { id, InvoiceAccentColorHex, InvoiceFooterNote, InvoiceShowBankDetails }),
            });

            TempData["SuccessMessage"] = "Invoice branding updated.";
            return RedirectToPage(new { id });
        }
    }
}
