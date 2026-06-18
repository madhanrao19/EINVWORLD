using CsvHelper;
using CsvHelper.Configuration;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Auth;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace eInvWorld.Pages.PublicCustomer
{
    [Authorize(Roles = "Admin,Supplier")]
    public class ImportModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ImportModel(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration) : base(context)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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

            // 2. Pre-load reference data for validation
            var validRegTypes = new HashSet<string>(await _context.RegistrationTypes.Select(x => x.Code).ToListAsync());
            var validStateCodes = new HashSet<string>(await _context.StateCodes.Select(x => x.Code).ToListAsync());
            var validCountryCodes = new HashSet<string>(await _context.CountryCodes.Select(x => x.Code).ToListAsync());

            // FIX: Strictly isolate TIN duplication check by Company ID
            IQueryable<eInvWorld.Models.InputModel.PublicCustomer> query = _context.PublicCustomers;
            if (createdByCompanyId.HasValue)
            {
                // For Suppliers: Only check against buyers created by their own company
                query = query.Where(p => p.CreatedByCompanyId == createdByCompanyId.Value);
            }
            else
            {
                // For Admins (Global): Only check against other global buyers, ignore supplier-specific buyers
                query = query.Where(p => p.CreatedByCompanyId == null);
            }

            var existingTINs = new HashSet<string>(await query.Select(p => p.TIN).ToListAsync());

            var generalTins = new[] { "EI00000000010", "EI00000000020", "EI00000000030", "EI00000000040" };

            // LHDN API Setup (Fetch token once for the whole batch)
            string? lhdnToken = null;
            string lhdnBaseUrl = _configuration["LHDNApiConfig:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";
            try
            {
                lhdnToken = await GetLhdnAccessTokenAsync();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Warning: Could not connect to LHDN Identity Server. API Validation will fail. " + ex.Message);
            }

            PreviewRecords = new List<PreviewRecordModel>();
            var validRecordsToKeep = new List<PublicCustomerCsvDto>();

            using (var reader = new StreamReader(UploadFile.OpenReadStream()))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
            }))
            {
                List<PublicCustomerCsvDto> records;
                try
                {
                    records = csv.GetRecords<PublicCustomerCsvDto>().ToList();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error parsing CSV: {ex.Message}");
                    return Page();
                }

                int rowNumber = 0;
                // Keep track of TINs in the current file to prevent duplicates within the CSV itself
                var fileTins = new HashSet<string>();

                foreach (var record in records)
                {
                    rowNumber++;
                    var previewRecord = new PreviewRecordModel
                    {
                        RowNumber = rowNumber,
                        CompanyName = record.CompanyName ?? "",
                        TIN = record.TIN ?? "",
                        RegNo = record.RegNo ?? "",
                        Dto = record,
                        Errors = new List<string>()
                    };

                    // Clean Data
                    record.TIN = FixScientificNotation(record.TIN) ?? "";
                    record.RegNo = FixScientificNotation(record.RegNo) ?? "";
                    record.OldRegNo = FixScientificNotation(record.OldRegNo) ?? "";
                    record.PhoneNo = FixScientificNotation(record.PhoneNo) ?? "";
                    record.FaxNo = FixScientificNotation(record.FaxNo);
                    record.PostalCode = FixScientificNotation(record.PostalCode);
                    record.MSIC = FixScientificNotation(record.MSIC)?.Replace("\"", "").Trim();
                    record.SST = FixScientificNotation(record.SST);
                    record.TTX = FixScientificNotation(record.TTX);
                    record.BankAccountNo = FixScientificNotation(record.BankAccountNo);

                    if (!string.IsNullOrWhiteSpace(record.PhoneNo))
                    {
                        var cleanPhone = record.PhoneNo.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
                        if (!cleanPhone.StartsWith("+") && cleanPhone.Length > 0 && char.IsDigit(cleanPhone[0])) cleanPhone = "+" + cleanPhone;
                        record.PhoneNo = cleanPhone;
                    }

                    if (!string.IsNullOrWhiteSpace(record.FaxNo) && record.FaxNo != "NA")
                    {
                        var cleanFax = record.FaxNo.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
                        if (!cleanFax.StartsWith("+") && cleanFax.Length > 0 && char.IsDigit(cleanFax[0])) cleanFax = "+" + cleanFax;
                        record.FaxNo = cleanFax;
                    }

                    if (string.IsNullOrWhiteSpace(record.SST)) record.SST = "NA";
                    if (string.IsNullOrWhiteSpace(record.TTX)) record.TTX = "NA";

                    // Update preview record strings after cleaning
                    previewRecord.TIN = record.TIN ?? "";
                    previewRecord.RegNo = record.RegNo ?? "";

                    // 1. Basic C# Annotations Validation
                    var validationContext = new ValidationContext(record);
                    var validationResults = new List<ValidationResult>();
                    if (!Validator.TryValidateObject(record, validationContext, validationResults, true))
                    {
                        previewRecord.Errors.AddRange(validationResults.Select(v => v.ErrorMessage ?? ""));
                    }

                    // 2. Database Lookups Validation
                    if (!validRegTypes.Contains(record.RegTypeCode ?? "")) previewRecord.Errors.Add($"Invalid RegTypeCode: '{record.RegTypeCode}'");
                    if (!validStateCodes.Contains(record.StateCode ?? "")) previewRecord.Errors.Add($"Invalid StateCode: '{record.StateCode}'");
                    if (!validCountryCodes.Contains(record.CountryCode ?? "")) previewRecord.Errors.Add($"Invalid CountryCode: '{record.CountryCode}'");

                    // 3. Duplicate Checks
                    if (!generalTins.Contains(record.TIN ?? ""))
                    {
                        if (existingTINs.Contains(record.TIN ?? ""))
                        {
                            previewRecord.Errors.Add($"TIN '{record.TIN}' already exists for your company.");
                        }
                        else if (fileTins.Contains(record.TIN ?? ""))
                        {
                            previewRecord.Errors.Add($"Duplicate TIN '{record.TIN}' found inside this CSV file.");
                        }
                    }

                    // 4. LHDN API Validation (Only if no basic errors so far, to save API calls)
                    if (!previewRecord.Errors.Any() && !generalTins.Contains(record.TIN ?? ""))
                    {
                        if (string.IsNullOrEmpty(lhdnToken))
                        {
                            previewRecord.Errors.Add("Failed to validate with LHDN (Authentication Error).");
                        }
                        else
                        {
                            bool isLhdnValid = await ValidateWithLhdnApi(record.TIN ?? "", record.RegTypeCode ?? "", record.RegNo ?? "", lhdnToken, lhdnBaseUrl);
                            if (!isLhdnValid)
                            {
                                previewRecord.Errors.Add("LHDN Validation Failed. TIN, ID Type, and Reg No mismatch.");
                            }
                        }
                    }

                    // Finalize Status
                    if (!previewRecord.Errors.Any())
                    {
                        previewRecord.IsValid = true;
                        validRecordsToKeep.Add(record);
                        if (!generalTins.Contains(record.TIN ?? "")) fileTins.Add(record.TIN ?? "");
                    }
                    else
                    {
                        previewRecord.IsValid = false;
                    }

                    PreviewRecords.Add(previewRecord);
                }
            }

            // Serialize valid records into a hidden field to pass to the next POST handler
            ValidRecordsJson = JsonSerializer.Serialize(validRecordsToKeep);

            return Page();
        }

        // --- STEP 2: CONFIRM AND SAVE ---
        public async Task<IActionResult> OnPostConfirmAsync()
        {
            if (string.IsNullOrWhiteSpace(ValidRecordsJson))
            {
                TempData["ErrorMessage"] = "No valid records found to import.";
                return RedirectToPage("./List");
            }

            var records = JsonSerializer.Deserialize<List<PublicCustomerCsvDto>>(ValidRecordsJson);

            if (records == null || !records.Any())
            {
                TempData["ErrorMessage"] = "Failed to read confirmed records.";
                return RedirectToPage("./List");
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
            foreach (var record in records)
            {
                var newCustomer = new eInvWorld.Models.InputModel.PublicCustomer
                {
                    CompanyName = record.CompanyName,
                    TIN = record.TIN,
                    RegTypeCode = record.RegTypeCode,
                    RegNo = record.RegNo,
                    Addr1 = record.Address,
                    CityName = record.CityName,
                    StateCode = record.StateCode,
                    CountryCode = record.CountryCode,
                    PhoneNo = record.PhoneNo,
                    SST = record.SST,
                    TTX = record.TTX,
                    IndustryClassificationCode = string.IsNullOrWhiteSpace(record.MSIC) ? "" : record.MSIC,
                    BizDescription = string.IsNullOrWhiteSpace(record.BizDescription) ? "" : record.BizDescription,
                    OldRegNo = string.IsNullOrWhiteSpace(record.OldRegNo) ? null : record.OldRegNo,
                    Email = string.IsNullOrWhiteSpace(record.Email) ? null : record.Email,
                    FaxNo = string.IsNullOrWhiteSpace(record.FaxNo) ? null : record.FaxNo,
                    Addr2 = null,
                    Addr3 = null,
                    PostalCode = string.IsNullOrWhiteSpace(record.PostalCode) ? null : record.PostalCode,
                    BankAccountNo = string.IsNullOrWhiteSpace(record.BankAccountNo) ? null : record.BankAccountNo,
                    BankName = string.IsNullOrWhiteSpace(record.BankName) ? null : record.BankName,
                    Attention = string.IsNullOrWhiteSpace(record.Attention) ? null : record.Attention,
                    PaymentTerms = string.IsNullOrWhiteSpace(record.PaymentTerms) ? null : record.PaymentTerms,
                    Remarks = string.IsNullOrWhiteSpace(record.Remarks) ? null : record.Remarks,

                    

                    // inside your newCustomer initialization:
                    IsActive = true,
                    IsAdminCreated = isAdmin,
                    CreatedBy = User.Identity?.Name ?? "System Import",
                    CreatedByCompanyId = createdByCompanyId,
                    CreatedDate = malaysiaTime,
                    UpdatedDate = malaysiaTime,
                    UpdatedBy = User.Identity?.Name ?? "System Import",
                    LogoPath = "/assets/images/users/multi-user.jpg",
                    IsApproved = User.IsInRole("Admin") || User.IsInRole("Supplier")
                };

                _context.PublicCustomers.Add(newCustomer);
                successCount++;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Successfully imported {successCount} validated buyers!";

            return RedirectToPage("./List");
        }


        // --- LHDN API HELPERS ---
        private async Task<string> GetLhdnAccessTokenAsync()
        {
            string? myCompanyTin = _configuration["TaxpayerValidationSettings:DefaultTIN"];
            string? clientId = _configuration["LHDNApiConfig:ClientId"];
            string? clientSecret = _configuration["LHDNApiConfig:ClientSecret"];
            string scope = _configuration["LHDNApiConfig:Scope"] ?? "InvoicingAPI";
            string baseUrl = _configuration["LHDNApiConfig:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";
            string tokenEndpoint = _configuration["LHDNApiConfig:TokenEndpoint"]?.TrimStart('/') ?? "connect/token";
            string tokenUrl = $"{baseUrl}/{tokenEndpoint}";

            var existingToken = await _context.Set<LHDNToken>()
                .Where(t => t.TIN == myCompanyTin && t.ExpiryTime > DateTime.UtcNow.AddMinutes(5))
                .FirstOrDefaultAsync();

            if (existingToken != null) return existingToken.AccessToken;

            var client = _httpClientFactory.CreateClient();
            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId ?? ""),
                new KeyValuePair<string, string>("client_secret", clientSecret ?? ""),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", scope)
            });

            var response = await client.PostAsync(tokenUrl, requestContent);
            if (!response.IsSuccessStatusCode) throw new Exception($"Failed to retrieve token. Status: {response.StatusCode}");

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token)) throw new Exception("Empty token.");

            var dbToken = await _context.Set<LHDNToken>().FirstOrDefaultAsync(t => t.TIN == myCompanyTin);
            if (dbToken == null)
            {
                dbToken = new LHDNToken
                {
                    TIN = myCompanyTin ?? "",
                    AccessToken = tokenResponse.access_token,
                    ExpiryTime = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in),
                    LastUpdated = DateTime.UtcNow
                };
                _context.Add(dbToken);
            }
            else
            {
                dbToken.AccessToken = tokenResponse.access_token;
                dbToken.ExpiryTime = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);
                dbToken.LastUpdated = DateTime.UtcNow;
                _context.Update(dbToken);
            }

            _context.Add(new LHDNTokenLog
            {
                TIN = myCompanyTin ?? "",
                IssuedAt = DateTime.UtcNow,
                ExpiryTime = dbToken.ExpiryTime,
                ClientIdUsed = clientId ?? "",
                Source = "BulkImportValidation"
            });

            await _context.SaveChangesAsync();
            return tokenResponse.access_token;
        }

        private async Task<bool> ValidateWithLhdnApi(string tin, string idType, string idNo, string accessToken, string baseUrl)
        {
            try
            {
                string requestUrl = $"{baseUrl}/api/v1.0/taxpayer/validate/{tin}?idType={idType}&idValue={idNo}";
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync(requestUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // --- UTILITY ---
        private string? FixScientificNotation(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            if (value.Contains("E", StringComparison.OrdinalIgnoreCase))
            {
                if (decimal.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal result))
                    return result.ToString("0");
            }
            return value;
        }

        // --- DTOs ---
        public class PreviewRecordModel
        {
            public int RowNumber { get; set; }
            public string CompanyName { get; set; } = null!;
            public string TIN { get; set; } = null!;
            public string RegNo { get; set; } = null!;
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
            public PublicCustomerCsvDto Dto { get; set; } = null!;
        }

        public class PublicCustomerCsvDto
        {
            [Required(ErrorMessage = "Company Name is required")]
            [StringLength(300)]
            public string CompanyName { get; set; } = null!;

            [Required(ErrorMessage = "TIN is required")]
            [StringLength(14)]
            public string TIN { get; set; } = null!;

            [Required(ErrorMessage = "RegTypeCode is required")]
            [StringLength(10)]
            public string RegTypeCode { get; set; } = null!;

            [Required(ErrorMessage = "RegNo is required")]
            public string RegNo { get; set; } = null!;

            [StringLength(20)]
            public string? OldRegNo { get; set; }

            [EmailAddress]
            [StringLength(320)]
            public string? Email { get; set; }

            [Required(ErrorMessage = "PhoneNo is required")]
            [StringLength(20)]
            [RegularExpression(@"^\+[0-9]{8,14}$", ErrorMessage = "Phone format invalid (e.g. +6012345678)")]
            public string PhoneNo { get; set; } = null!;

            [StringLength(20)]
            [RegularExpression(@"^\+[0-9]{8,14}$")]
            public string? FaxNo { get; set; }

            [Required(ErrorMessage = "Address is required")]
            public string Address { get; set; } = null!;

            [StringLength(50)]
            public string? PostalCode { get; set; }

            [Required(ErrorMessage = "City Name is required")]
            [StringLength(50)]
            public string CityName { get; set; } = null!;

            [Required(ErrorMessage = "State Code is required")]
            public string StateCode { get; set; } = null!;

            [Required(ErrorMessage = "Country Code is required")]
            public string CountryCode { get; set; } = null!;

            public string? MSIC { get; set; }

            [StringLength(300)]
            public string? BizDescription { get; set; }

            [Required]
            [StringLength(35)]
            public string? SST { get; set; }

            [Required]
            [StringLength(17)]
            public string? TTX { get; set; }

            [StringLength(150)]
            public string? BankAccountNo { get; set; }

            [StringLength(100)]
            public string? BankName { get; set; }

            [StringLength(200)]
            public string? Attention { get; set; }

            [StringLength(300)]
            public string? PaymentTerms { get; set; }

            [StringLength(500)]
            public string? Remarks { get; set; }
        }
    }
}