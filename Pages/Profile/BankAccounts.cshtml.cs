using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using EINVWORLD.Services;
using EINVWORLD.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Profile
{
    /// <summary>
    /// A company's bank accounts (reached from My Profile; Admins from Company details/edit with ?companyId=).
    /// Any member may view; changing requires the company "Edit profile" permission (Owner/Admin/Editor) or a
    /// system Admin. Account numbers are only ever shown masked.
    /// </summary>
    [Authorize(Roles = "Admin,Supplier")]
    public class BankAccountsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ISupplierBankAccountService _accounts;
        private readonly ICompanyAuthorizationService _companyAuth;

        public BankAccountsModel(ApplicationDbContext context, ISupplierBankAccountService accounts, ICompanyAuthorizationService companyAuth)
        {
            _context = context;
            _accounts = accounts;
            _companyAuth = companyAuth;
        }

        [BindProperty(SupportsGet = true)]
        public int? CompanyId { get; set; }

        public PartyInfo? Company { get; private set; }
        public IReadOnlyList<SupplierBankAccount> Accounts { get; private set; } = new List<SupplierBankAccount>();
        public bool CanEdit { get; private set; }

        public class AccountInput
        {
            [Required(ErrorMessage = "Bank name is required.")]
            [StringLength(100, ErrorMessage = "Bank name cannot exceed 100 characters.")]
            public string BankName { get; set; } = string.Empty;

            [StringLength(150, ErrorMessage = "Bank account number cannot exceed 150 characters.")]
            [RegularExpression(@"^[0-9A-Za-z\- ]*$", ErrorMessage = "Use digits, letters, spaces or dashes only.")]
            public string? BankAccountNo { get; set; }

            public bool MakeDefault { get; set; }
        }

        [BindProperty]
        public AccountInput Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            if (!await ResolveCompanyAsync(ct)) return Forbid();
            if (Company == null) return NotFound();
            Accounts = await _accounts.ListAsync(Company.PartyInfoId, ct);
            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync(CancellationToken ct)
        {
            var guard = await GuardWriteAsync(ct);
            if (guard != null) return guard;

            if (string.IsNullOrWhiteSpace(Input.BankAccountNo))
                ModelState.AddModelError("Input.BankAccountNo", "Bank account number is required.");

            var existing = await _accounts.ListAsync(Company!.PartyInfoId, ct);
            if (!string.IsNullOrWhiteSpace(Input.BankAccountNo) && existing.Any(a => a.BankAccountNo == Input.BankAccountNo.Trim()))
                ModelState.AddModelError("Input.BankAccountNo", "This account number is already saved.");

            if (!ModelState.IsValid)
            {
                Accounts = existing;
                return Page();
            }

            await _accounts.AddAsync(Company.PartyInfoId, Input.BankName, Input.BankAccountNo!, Input.MakeDefault, User.Identity?.Name, ct);
            TempData["SuccessMessage"] = "Bank account added.";
            return RedirectToPage(new { CompanyId });
        }

        public async Task<IActionResult> OnPostUpdateAsync(int accountId, CancellationToken ct)
        {
            var guard = await GuardWriteAsync(ct);
            if (guard != null) return guard;

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToPage(new { CompanyId });
            }

            var ok = await _accounts.UpdateAsync(Company!.PartyInfoId, accountId, Input.BankName, Input.BankAccountNo, User.Identity?.Name, ct);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Bank account updated." : "Bank account not found.";
            return RedirectToPage(new { CompanyId });
        }

        public async Task<IActionResult> OnPostSetDefaultAsync(int accountId, CancellationToken ct)
        {
            var guard = await GuardWriteAsync(ct);
            if (guard != null) return guard;

            var ok = await _accounts.SetDefaultAsync(Company!.PartyInfoId, accountId, User.Identity?.Name, ct);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Default bank account changed." : "Bank account not found.";
            return RedirectToPage(new { CompanyId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int accountId, CancellationToken ct)
        {
            var guard = await GuardWriteAsync(ct);
            if (guard != null) return guard;

            var ok = await _accounts.DeleteAsync(Company!.PartyInfoId, accountId, User.Identity?.Name, ct);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok ? "Bank account deleted." : "Bank account not found.";
            return RedirectToPage(new { CompanyId });
        }

        // Validation errors only matter for Add/Update; the other handlers bind no input.
        private async Task<IActionResult?> GuardWriteAsync(CancellationToken ct)
        {
            if (!await ResolveCompanyAsync(ct)) return Forbid();
            if (Company == null) return NotFound();
            if (!CanEdit) return Forbid();
            return null;
        }

        /// <summary>
        /// Picks the company (Admin: ?companyId= required; supplier: ?companyId= of a company they belong to, else
        /// their primary company) and works out CanEdit. False = not allowed to see this company at all.
        /// </summary>
        private async Task<bool> ResolveCompanyAsync(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return false;

            if (User.IsInRole("Admin"))
            {
                if (CompanyId == null) return true; // Company stays null -> 404
                Company = await _context.PartyInfos.AsNoTracking().FirstOrDefaultAsync(p => p.PartyInfoId == CompanyId, ct);
                CanEdit = true;
                return true;
            }

            var membership = await _context.UserCompanies.AsNoTracking()
                .Where(uc => uc.UserId == userId && (CompanyId == null ? true : uc.PartyInfoId == CompanyId))
                .OrderByDescending(uc => uc.IsPrimaryCompany)
                .FirstOrDefaultAsync(ct);
            if (membership == null) return CompanyId == null; // no company at all -> 404; someone else's -> 403

            Company = await _context.PartyInfos.AsNoTracking().FirstOrDefaultAsync(p => p.PartyInfoId == membership.PartyInfoId, ct);
            CanEdit = await _companyAuth.HasPermissionAsync(userId, membership.PartyInfoId, CompanyPermission.EditProfile, ct);
            return true;
        }
    }
}
