using CsvHelper;
using CsvHelper.Configuration;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace eInvWorld.Pages.Items
{
    [Authorize(Roles = "Admin,Supplier")]
    public class ImportModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;

        public ImportModel(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        [BindProperty]
        public IFormFile? UploadFile { get; set; }

        [BindProperty]
        public string? ValidRecordsJson { get; set; }

        public List<PreviewRecordModel> PreviewRecords { get; set; } = new();

        public IActionResult OnGet()
        {
            return Page();
        }

        // --- STEP 1: UPLOAD AND VALIDATE ---
        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (UploadFile == null || UploadFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a CSV file.");
                return Page();
            }

            if (!UploadFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Only .csv files are allowed.");
                return Page();
            }

            // 1. Identify Context (Who is importing?)
            int? createdByCompanyId = null;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                if (userCompany == null)
                {
                    ModelState.AddModelError("", "No active company profile found for your account.");
                    return Page();
                }
                createdByCompanyId = userCompany.PartyInfoId;
            }

            // 2. Pre-load valid Classification Codes from the database
            var validClassificationCodes = new HashSet<string>(await _context.ClassificationCodes.Select(x => x.Code).ToListAsync());

            // Optional: Get existing Item Codes for this company to prevent duplicates
            IQueryable<ItemDescription> query = _context.ItemDescriptions;
            if (createdByCompanyId.HasValue)
            {
                query = query.Where(p => p.CreatedByCompanyId == createdByCompanyId.Value);
            }
            else
            {
                query = query.Where(p => p.CreatedByCompanyId == null);
            }
            var existingItemCodes = new HashSet<string>(await query.Select(p => p.ItemCode).ToListAsync());

            PreviewRecords = new List<PreviewRecordModel>();
            var validRecordsToKeep = new List<ItemCsvDto>();

            using (var reader = new StreamReader(UploadFile.OpenReadStream()))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
            }))
            {
                List<ItemCsvDto> records;
                try
                {
                    records = csv.GetRecords<ItemCsvDto>().ToList();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error parsing CSV: {ex.Message}");
                    return Page();
                }

                int rowNumber = 0;
                var fileItemCodes = new HashSet<string>();

                foreach (var record in records)
                {
                    rowNumber++;
                    var previewRecord = new PreviewRecordModel
                    {
                        RowNumber = rowNumber,
                        ClassificationCode = record.ClassificationCode,
                        ItemCode = record.ItemCode,
                        Description = record.Description,
                        Errors = new List<string>()
                    };

                    // 1. Basic C# Annotations Validation
                    var validationContext = new ValidationContext(record);
                    var validationResults = new List<ValidationResult>();
                    if (!Validator.TryValidateObject(record, validationContext, validationResults, true))
                    {
                        previewRecord.Errors.AddRange(validationResults.Select(v => v.ErrorMessage ?? ""));
                    }

                    // 2. Database Lookup Validation (Classification Code)
                    if (!string.IsNullOrWhiteSpace(record.ClassificationCode) && !validClassificationCodes.Contains(record.ClassificationCode))
                    {
                        previewRecord.Errors.Add($"Invalid Classification Code: '{record.ClassificationCode}'. It does not exist in the system.");
                    }

                    // 3. Duplicate Checks
                    if (!string.IsNullOrWhiteSpace(record.ItemCode))
                    {
                        if (existingItemCodes.Contains(record.ItemCode))
                        {
                            previewRecord.Errors.Add($"Item Code '{record.ItemCode}' already exists for your company.");
                        }
                        else if (fileItemCodes.Contains(record.ItemCode))
                        {
                            previewRecord.Errors.Add($"Duplicate Item Code '{record.ItemCode}' found inside this CSV file.");
                        }
                    }

                    // Finalize Status
                    if (!previewRecord.Errors.Any())
                    {
                        previewRecord.IsValid = true;
                        validRecordsToKeep.Add(record);
                        fileItemCodes.Add(record.ItemCode);
                    }
                    else
                    {
                        previewRecord.IsValid = false;
                    }

                    PreviewRecords.Add(previewRecord);
                }
            }

            ValidRecordsJson = JsonSerializer.Serialize(validRecordsToKeep);

            return Page();
        }

        // --- STEP 2: CONFIRM AND SAVE ---
        public async Task<IActionResult> OnPostConfirmAsync()
        {
            if (string.IsNullOrWhiteSpace(ValidRecordsJson))
            {
                TempData["ErrorMessage"] = "No valid records found to import.";
                return RedirectToPage("./Index");
            }

            var records = JsonSerializer.Deserialize<List<ItemCsvDto>>(ValidRecordsJson);

            if (records == null || !records.Any())
            {
                TempData["ErrorMessage"] = "Failed to read confirmed records.";
                return RedirectToPage("./Index");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            int? createdByCompanyId = null;

            if (!isAdmin)
            {
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();
                createdByCompanyId = userCompany?.PartyInfoId;
            }

            int successCount = 0;

            
            var malaysiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
            var importUser = User.Identity?.Name ?? "System Import";

            foreach (var record in records)
            {
                var newItem = new ItemDescription
                {
                    ClassificationCode = record.ClassificationCode,
                    ItemCode = record.ItemCode,
                    Description = record.Description,
                    IsActive = true,
                    CreatedByCompanyId = createdByCompanyId,
                    UpdatedBy = importUser,          
                    UpdatedDate = malaysiaTime       
                };

                _context.ItemDescriptions.Add(newItem);
                successCount++;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Successfully imported {successCount} items!";

            return RedirectToPage("./Index");
        }

        // --- DTOs ---
        public class PreviewRecordModel
        {
            public int RowNumber { get; set; }
            public string ClassificationCode { get; set; } = null!;
            public string ItemCode { get; set; } = null!;
            public string Description { get; set; } = null!;
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
        }

        public class ItemCsvDto
        {
            [Required(ErrorMessage = "Classification Code is required")]
            [StringLength(50)]
            public string ClassificationCode { get; set; } = null!;

            [Required(ErrorMessage = "Item Code is required")]
            [StringLength(100)]
            public string ItemCode { get; set; } = null!;

            [Required(ErrorMessage = "Description is required")]
            [StringLength(500)]
            public string Description { get; set; } = null!;
        }
    }
}