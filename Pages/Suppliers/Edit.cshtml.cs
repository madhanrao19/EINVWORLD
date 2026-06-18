using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using eInvWorld.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using eInvWorld.Models;

namespace eInvWorld.Pages.Suppliers
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly DropdownHelper _dropdownHelper;
        private readonly ILogger<EditModel> _logger; // Inject Logger
        private readonly FilePathConfig _filePathConfig;

        public EditModel(ApplicationDbContext context, IWebHostEnvironment env, DropdownHelper dropdownHelper, ILogger<EditModel> logger, IOptions<FilePathConfig> filePathConfig)
        {
            _context = context;
            _env = env;
            _dropdownHelper = dropdownHelper;
            _logger = logger;
            _filePathConfig = filePathConfig.Value;
        }

        [BindProperty(SupportsGet = true)]
        public string? From { get; set; } // example: "lead"


        [BindProperty]
        public PartyInfo PartyInfo { get; set; } = default!;

        [BindProperty]
        public IFormFile? LogoFile { get; set; }

        public List<SelectListItem> StateOptions { get; set; } = new();
        public List<SelectListItem> CountryOptions { get; set; } = new();
        public List<SelectListItem> IdTypesOptions { get; set; } = new();
        public List<SelectListItem> MSICOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Edit requested with null ID.");
                return NotFound();
            }

            var partyInfo = await _context.PartyInfos.FirstOrDefaultAsync(m => m.PartyInfoId == id);
            if (partyInfo == null)
            {
                _logger.LogWarning($"PartyInfo with ID {id} not found.");
                return NotFound();
            }

            PartyInfo = partyInfo;

            // Populate dropdowns using the helper
            LoadDropdownOptions();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid during Edit operation.");
                LoadDropdownOptions();
                return Page();
            }

            var existingParty = await _context.PartyInfos.FindAsync(PartyInfo.PartyInfoId);
            if (existingParty == null)
            {
                _logger.LogWarning($"PartyInfo with ID {PartyInfo.PartyInfoId} not found during update.");
                return NotFound();
            }

            // ✅ New: Check for duplicate TIN (Excluding General TINs and the current record)
            var generalTins = new[] { "EI00000000010", "EI00000000020", "EI00000000030", "EI00000000040" };
            if (!generalTins.Contains(PartyInfo.TIN))
            {
                var tinExists = await _context.PartyInfos
                    .AnyAsync(p => p.TIN == PartyInfo.TIN && p.PartyInfoId != PartyInfo.PartyInfoId);

                if (tinExists)
                {
                    ModelState.AddModelError("PartyInfo.TIN", "A record with this TIN already exists.");
                    LoadDropdownOptions();
                    return Page();
                }
            }

            // Preserve audit fields
            PartyInfo.CreatedBy = existingParty.CreatedBy ?? User.Identity?.Name ?? "System";
            PartyInfo.CreatedDate = existingParty.CreatedDate;
            PartyInfo.UpdatedBy = User.Identity?.Name ?? "System";
            PartyInfo.UpdatedDate = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(PartyInfo.BizDescription))
            {
                PartyInfo.BizDescription = existingParty.BizDescription;
            }

            // Handle logo update
            if (LogoFile != null && LogoFile.Length > 0)
            {
                var uploadsFolder = _filePathConfig.CompanyLogosFolder;
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(LogoFile.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await LogoFile.CopyToAsync(stream);
                }
                existingParty.LogoPath = $"/api/companies/logos/{fileName}";
            }

            // Update fields manually
            existingParty.CompanyName = PartyInfo.CompanyName;
            existingParty.RegTypeCode = PartyInfo.RegTypeCode;
            existingParty.TIN = PartyInfo.TIN;
            existingParty.RegNo = PartyInfo.RegNo;
            existingParty.SST = PartyInfo.SST;
            existingParty.TTX = PartyInfo.TTX;
            existingParty.IndustryClassificationCode = PartyInfo.IndustryClassificationCode;
            existingParty.BizDescription = PartyInfo.BizDescription;
            existingParty.Email = PartyInfo.Email;
            existingParty.Addr1 = PartyInfo.Addr1;
            existingParty.Addr2 = null;/*PartyInfo.Addr2;*/
            existingParty.Addr3 = null;/*PartyInfo.Addr3;*/
            existingParty.PostalCode = PartyInfo.PostalCode;
            existingParty.CityName = PartyInfo.CityName;
            existingParty.StateCode = PartyInfo.StateCode;
            existingParty.CountryCode = PartyInfo.CountryCode;
            existingParty.PhoneNo = PartyInfo.PhoneNo;
            existingParty.FaxNo = PartyInfo.FaxNo;
            existingParty.Remarks = PartyInfo.Remarks;
            existingParty.UpdatedBy = PartyInfo.UpdatedBy;
            existingParty.UpdatedDate = PartyInfo.UpdatedDate;
            existingParty.OldRegNo = PartyInfo.OldRegNo;
            existingParty.BankAccountNo = PartyInfo.BankAccountNo;
            existingParty.BankName = PartyInfo.BankName;
            existingParty.Attention = PartyInfo.Attention;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully updated PartyInfo with ID {PartyInfo.PartyInfoId}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while updating PartyInfo with ID {PartyInfo.PartyInfoId}.");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again later.");
                LoadDropdownOptions();
                return Page();
            }

            return RedirectToPage(From == "lead" ? "/Lead/List" : "./Index");
        }


        private bool PartyInfoExists(int id)
        {
            return _context.PartyInfos.Any(e => e.PartyInfoId == id);
        }

        private void LoadDropdownOptions()
        {
            StateOptions = _dropdownHelper.GetStateOptions();
            CountryOptions = _dropdownHelper.GetCountryOptions();
            IdTypesOptions = _dropdownHelper.GetIdTypesOptions();

            // ✅ Ensure MSICOptions contains descriptions
            MSICOptions = _dropdownHelper.GetMSICOptions()
                .Select(msic => new SelectListItem
                {
                    Value = msic.Value,
                    Text = $"{msic.Value} - {msic.Text}",  // Ensure description is included
                    Selected = PartyInfo.IndustryClassificationCode == msic.Value
                }).ToList();
        }


    }
}
