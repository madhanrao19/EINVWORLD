using System.Security.Claims;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.PublicCustomer
{
    [Authorize(Roles = "Admin,Supplier")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly DropdownHelper _dropdownHelper;
        private readonly FilePathConfig _filePathConfig;

        public EditModel(
            ApplicationDbContext context,
            DropdownHelper dropdownHelper,
            IOptions<FilePathConfig> filePathConfig)
        {
            _context = context;
            _dropdownHelper = dropdownHelper;
            _filePathConfig = filePathConfig.Value;
        }

        [BindProperty]
        public eInvWorld.Models.InputModel.PublicCustomer PublicCustomer { get; set; } = default!;

        [BindProperty]
        public IFormFile? LogoUpload { get; set; }

        public List<SelectListItem> StateOptions { get; set; } = new();
        public List<SelectListItem> CountryOptions { get; set; } = new();
        public List<SelectListItem> IdTypesOptions { get; set; } = new();
        public List<SelectListItem> MSICOptions { get; set; } = new();

        private bool IsAdmin => User.IsInRole("Admin");

        // ==============================
        // GET
        // ==============================
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
                return NotFound();

            var entity = await _context.PublicCustomers
                .FirstOrDefaultAsync(p => p.PublicCustomerId == id);

            if (entity == null)
                return NotFound();

            // Admin can edit anything
            if (User.IsInRole("Admin"))
            {
                PublicCustomer = entity;
                PopulateDropdowns();
                return Page();
            }

            // Supplier ownership check
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var userCompany = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .OrderByDescending(uc => uc.IsPrimaryCompany)
                .FirstOrDefaultAsync();

            if (userCompany == null ||
                entity.CreatedByCompanyId != userCompany.PartyInfoId)
            {
                return Redirect("/Identity/Account/AccessDenied");
            }
            if (!userCompany.HasCompanyAccess || userCompany.IsViewOnly)
            {
                return Redirect("/Identity/Account/AccessDenied");
            }
            PublicCustomer = entity;
            PopulateDropdowns();
            return Page();
        }


        // ==============================
        // POST
        // ==============================
        public async Task<IActionResult> OnPostAsync()
        {
            var existing = await _context.PublicCustomers
                .FirstOrDefaultAsync(p => p.PublicCustomerId == PublicCustomer.PublicCustomerId);

            if (existing == null)
                return NotFound();

            // ===== Ownership check =====
            if (!User.IsInRole("Admin"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                // Same view-only rule as GET; this page is a plain PageModel, so SupplierBasePage doesn't cover it.
                if (userCompany == null ||
                    existing.CreatedByCompanyId != userCompany.PartyInfoId ||
                    !userCompany.HasCompanyAccess || userCompany.IsViewOnly)
                {
                    return Redirect("/Identity/Account/AccessDenied");
                }
            }

            PublicCustomer.CreatedBy = existing.CreatedBy;
            PublicCustomer.CreatedDate = existing.CreatedDate;
            PublicCustomer.CreatedByCompanyId = existing.CreatedByCompanyId;
            ModelState.Remove("PublicCustomer.CreatedBy");
            ModelState.Remove("PublicCustomer.CreatedDate");

            // A Malaysian buyer's state must still be a real StateCodes entry (the dropdown enforces
            // this client-side, but the FK constraint no longer does at the DB level — see
            // Models/InputModel/PublicCustomer.cs). A foreign buyer's state is free text, already
            // covered by [Required]/[StringLength(100)] on the model.
            if (string.Equals(PublicCustomer.CountryCode, "MYS", StringComparison.OrdinalIgnoreCase))
            {
                var isValidState = await _context.StateCodes
                    .AnyAsync(s => s.Code == PublicCustomer.StateCode && s.IsActive);
                if (!isValidState)
                {
                    ModelState.AddModelError("PublicCustomer.StateCode", "Invalid state selected.");
                }
            }

            // Same per-company TIN uniqueness rule as Create, excluding this buyer itself.
            if (!GeneralTINHelper.IsGeneralTIN(PublicCustomer.TIN)
                && await _context.PublicCustomers.AnyAsync(p => p.TIN == PublicCustomer.TIN
                    && p.CreatedByCompanyId == existing.CreatedByCompanyId
                    && p.PublicCustomerId != existing.PublicCustomerId))
            {
                ModelState.AddModelError("PublicCustomer.TIN", "A record with this TIN already exists for your company.");
            }

            // Same logo check as Create: content-sniffed PNG/JPG only, stored outside wwwroot.
            string? logoExt = null;
            if (LogoUpload != null && LogoUpload.Length > 0)
            {
                using (var probe = LogoUpload.OpenReadStream()) logoExt = LogoImage.DetectExtension(probe);
                if (logoExt == null)
                    ModelState.AddModelError(nameof(LogoUpload), "The logo must be a PNG or JPG image.");
            }

            if (!ModelState.IsValid)
            {
                PublicCustomer.LogoPath = existing.LogoPath;
                PopulateDropdowns();
                return Page();
            }

            // ===== Update fields =====
            existing.CompanyName = PublicCustomer.CompanyName;
            existing.TIN = PublicCustomer.TIN;
            existing.RegTypeCode = PublicCustomer.RegTypeCode;
            existing.RegNo = PublicCustomer.RegNo;
            existing.OldRegNo = PublicCustomer.OldRegNo;
            existing.BizDescription = PublicCustomer.BizDescription;
            existing.FaxNo = PublicCustomer.FaxNo;
            existing.Attention = PublicCustomer.Attention;
            existing.PaymentTerms = PublicCustomer.PaymentTerms;
            existing.SST = PublicCustomer.SST;
            existing.TTX = PublicCustomer.TTX;
            existing.Email = PublicCustomer.Email;
            existing.PhoneNo = PublicCustomer.PhoneNo;
            existing.FaxNo = PublicCustomer.FaxNo;
            existing.Addr1 = PublicCustomer.Addr1;
            existing.Addr2 = null;/*PublicCustomer.Addr2;*/
            existing.Addr3 = null;/*PublicCustomer.Addr3;*/
            existing.PostalCode = PublicCustomer.PostalCode;
            existing.CityName = PublicCustomer.CityName;
            existing.StateCode = PublicCustomer.StateCode;
            existing.CountryCode = PublicCustomer.CountryCode;
            existing.Remarks = PublicCustomer.Remarks;
            existing.OldRegNo = PublicCustomer.OldRegNo;
            existing.Attention = PublicCustomer.Attention;
            existing.PaymentTerms = PublicCustomer.PaymentTerms;
            existing.IndustryClassificationCode = PublicCustomer.IndustryClassificationCode;
            existing.BizDescription = PublicCustomer.BizDescription ?? "";

            if (logoExt != null)
            {
                var uploadsFolder = _filePathConfig.CompanyLogosFolder;
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}{logoExt}";
                using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
                {
                    await LogoUpload!.CopyToAsync(stream);
                }

                existing.LogoPath = $"/api/companies/logos/{fileName}";
            }

            var malaysiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));

            existing.UpdatedBy = User.Identity?.Name ?? "System";
            existing.UpdatedDate = malaysiaTime;

            await _context.SaveChangesAsync();

            return RedirectToPage("./Details", new { id = existing.PublicCustomerId });
        }


        // ==============================
        // Dropdown population
        // ==============================
        private void PopulateDropdowns()
        {
            StateOptions = _dropdownHelper.GetStateOptions();
            CountryOptions = _dropdownHelper.GetCountryOptions();
            IdTypesOptions = _dropdownHelper.GetIdTypesOptions();

            MSICOptions = _dropdownHelper.GetMSICOptions()
                .Where(msic => msic != null && msic.Value != null && msic.Text != null)
                .Select(msic => new SelectListItem
                {
                    Value = msic.Value,
                    Text = $"{msic.Value} - {msic.Text}",
                    Selected = PublicCustomer != null &&
                               PublicCustomer.IndustryClassificationCode == msic.Value
                }).ToList();
        }
    }
}
