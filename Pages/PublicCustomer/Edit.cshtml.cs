using System.Security.Claims;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.InputModel;
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
        private readonly IWebHostEnvironment _environment;
        private readonly DropdownHelper _dropdownHelper;

        public EditModel(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            DropdownHelper dropdownHelper)
        {
            _context = context;
            _environment = environment;
            _dropdownHelper = dropdownHelper;
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

                if (userCompany == null ||
                    existing.CreatedByCompanyId != userCompany.PartyInfoId)
                {
                    return Redirect("/Identity/Account/AccessDenied");
                }
            }

            PublicCustomer.CreatedBy = existing.CreatedBy;
            PublicCustomer.CreatedDate = existing.CreatedDate;
            PublicCustomer.CreatedByCompanyId = existing.CreatedByCompanyId;
            ModelState.Remove("PublicCustomer.CreatedBy");
            ModelState.Remove("PublicCustomer.CreatedDate");
            if (!ModelState.IsValid)
            {
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
            existing.BankName = PublicCustomer.BankName;
            existing.BankAccountNo = PublicCustomer.BankAccountNo;
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
            existing.BankName = PublicCustomer.BankName;
            existing.BankAccountNo = PublicCustomer.BankAccountNo;
            existing.Remarks = PublicCustomer.Remarks;
            existing.OldRegNo = PublicCustomer.OldRegNo;
            existing.Attention = PublicCustomer.Attention;
            existing.PaymentTerms = PublicCustomer.PaymentTerms;
            existing.IndustryClassificationCode = PublicCustomer.IndustryClassificationCode;
            existing.BizDescription = PublicCustomer.BizDescription ?? "";

            if (LogoUpload != null && LogoUpload.Length > 0)
            {
                var uploadFolder = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "publiccustomer");

                Directory.CreateDirectory(uploadFolder);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(LogoUpload.FileName)}";
                var filePath = Path.Combine(uploadFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await LogoUpload.CopyToAsync(stream);

                existing.LogoPath = $"/uploads/publiccustomer/{fileName}";
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
