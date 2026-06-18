using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Auth;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eInvWorld.Pages.PublicCustomer
{
    [Authorize(Roles = "Admin,Supplier")]
    public class CreateModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly DropdownHelper _dropdownHelper;
        private readonly FilePathConfig _filePathConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            DropdownHelper dropdownHelper,
            IOptions<FilePathConfig> filePathConfig,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<CreateModel> logger) : base(context)
        {
            _context = context;
            _environment = environment;
            _dropdownHelper = dropdownHelper;
            _filePathConfig = filePathConfig.Value;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string? From { get; set; }

        public IActionResult OnGet(string? from)
        {
            if (from == "admin" && !User.IsInRole("Admin"))
            {
                return Redirect("/Identity/Account/AccessDenied");
            }

            From = from;

            PublicCustomer = new eInvWorld.Models.InputModel.PublicCustomer
            {
                IsAdminCreated = User.IsInRole("Admin"),
                IsApproved = true
            };

            PopulateDropdowns();
            return Page();
        }

        [BindProperty]
        public eInvWorld.Models.InputModel.PublicCustomer PublicCustomer { get; set; } = new();

        [BindProperty]
        public IFormFile? LogoUpload { get; set; }

        public List<SelectListItem> StateOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CountryOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> IdTypesOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> MSICOptions { get; set; } = new();

        public bool IsAdmin => User.IsInRole("Admin");

        public async Task<IActionResult> OnPostAsync(string? from)
        {
            if (from == "admin" && !User.IsInRole("Admin"))
            {
                return Redirect("/Identity/Account/AccessDenied");
            }

            PopulateDropdowns();

            ModelState.Remove("PublicCustomer.CreatedBy");
            ModelState.Remove("PublicCustomer.InviteCode");
            ModelState.Remove("PublicCustomer.CreatedDate");
            ModelState.Remove("PublicCustomer.CreatedByCompanyId");

            if (PublicCustomer.BizDescription == null)
            {
                PublicCustomer.BizDescription = "";
            }

            PublicCustomer.IsAdminCreated = IsAdmin;
            PublicCustomer.IsApproved = true;

            PublicCustomer.CreatedBy = string.IsNullOrWhiteSpace(PublicCustomer.CreatedBy)
                ? (User.Identity?.Name ?? "System")
                : PublicCustomer.CreatedBy;

            var malaysiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));

            PublicCustomer.CreatedDate = malaysiaTime;

            var registrationType = await _context.RegistrationTypes
                .FirstOrDefaultAsync(r => r.Code == PublicCustomer.RegTypeCode);

            if (registrationType == null)
            {
                ModelState.AddModelError("PublicCustomer.RegTypeCode", "Invalid Registration Type selected.");
                return Page();
            }

            // ✅ STEP 1: Determine the Company ID FIRST
            if (!IsAdmin)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                if (userCompany != null)
                {
                    PublicCustomer.CreatedByCompanyId = userCompany.PartyInfoId;
                }
                else
                {
                    ModelState.AddModelError("", "Error: You are not assigned to any supplier company.");
                    return Page();
                }
            }

            // ✅ STEP 2: Validate TIN uniqueness ONLY for the current company
            var generalTins = new[] { "EI00000000010", "EI00000000020", "EI00000000030", "EI00000000040" };
            if (!generalTins.Contains(PublicCustomer.TIN))
            {
                // Check if THIS specific company already created a buyer with THIS TIN
                var existingTIN = await _context.PublicCustomers
                    .AnyAsync(p => p.TIN == PublicCustomer.TIN && p.CreatedByCompanyId == PublicCustomer.CreatedByCompanyId);

                if (existingTIN)
                {
                    TempData["ErrorMessage"] = $"Cannot create: Your company already has a buyer with the TIN '{PublicCustomer.TIN}'.";
                    ModelState.AddModelError("PublicCustomer.TIN", "A record with this TIN already exists for your company.");
                    return Page();
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errors)
                {
                    _logger.LogWarning("Model validation error: {Error}", error.ErrorMessage);
                }
                return Page();
            }

            string logoRelativePath = "/assets/images/users/multi-user.jpg";
            if (LogoUpload != null && LogoUpload.Length > 0)
            {
                var uploadsFolder = _filePathConfig.CompanyLogosFolder;
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(LogoUpload.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await LogoUpload.CopyToAsync(fileStream);
                }

                logoRelativePath = $"/api/companies/logos/{uniqueFileName}";
            }

            var publicCustomer = new eInvWorld.Models.InputModel.PublicCustomer
            {
                IndustryClassificationCode = PublicCustomer.IndustryClassificationCode,
                BizDescription = PublicCustomer.BizDescription,
                CompanyName = PublicCustomer.CompanyName,
                TIN = PublicCustomer.TIN,
                RegTypeCode = registrationType.Code,
                RegNo = PublicCustomer.RegNo,
                SST = PublicCustomer.SST,
                TTX = PublicCustomer.TTX,
                Email = PublicCustomer.Email,
                Addr1 = PublicCustomer.Addr1,
                Addr2 = null,
                Addr3 = null,
                PostalCode = PublicCustomer.PostalCode,
                CityName = PublicCustomer.CityName,
                StateCode = PublicCustomer.StateCode,
                CountryCode = PublicCustomer.CountryCode,
                PhoneNo = PublicCustomer.PhoneNo,
                FaxNo = PublicCustomer.FaxNo,
                Remarks = PublicCustomer.Remarks,
                IsActive = true,
                CreatedBy = User.Identity?.Name ?? "System",
                CreatedDate = malaysiaTime,
                UpdatedDate = malaysiaTime,
                UpdatedBy = User.Identity?.Name ?? "System",
                IsAdminCreated = IsAdmin,
                InviteCode = null,
                LogoPath = logoRelativePath,
                IsApproved = PublicCustomer.IsApproved,
                OldRegNo = PublicCustomer.OldRegNo,
                BankAccountNo = PublicCustomer.BankAccountNo,
                BankName = PublicCustomer.BankName,
                Attention = PublicCustomer.Attention,
                PaymentTerms = PublicCustomer.PaymentTerms,
                CreatedByCompanyId = PublicCustomer.CreatedByCompanyId
            };

            _context.PublicCustomers.Add(publicCustomer);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Buyer successfully created!";

            return RedirectToPage("./List");
        }

        public async Task<JsonResult> OnGetValidateTINAsync(string tin, string idType, string idNo)
        {
            var generalTins = new[] { "EI00000000010", "EI00000000020", "EI00000000030", "EI00000000040" };
            if (generalTins.Contains(tin))
            {
                return new JsonResult(new { success = true, message = "General TIN skipped validation." });
            }

            if (string.IsNullOrWhiteSpace(tin) || string.IsNullOrWhiteSpace(idType) || string.IsNullOrWhiteSpace(idNo))
            {
                return new JsonResult(new { success = false, message = "TIN, ID Type, and Registration Number are required for validation." });
            }

            bool isValid = await ValidateWithLhdnApi(tin, idType, idNo);

            if (isValid)
            {
                return new JsonResult(new { success = true, message = "Validated successfully with LHDN." });
            }

            return new JsonResult(new { success = false, message = "LHDN Validation Failed. Please check TIN, ID Type, and Registration Number." });
        }
        private async Task<bool> ValidateWithLhdnApi(string tin, string idType, string idNo)
        {
            try
            {
                // ✅ Read BaseUrl from appsettings.json
                string baseUrl = _configuration["LHDNApiConfig:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";

                // Construct the validate URL
                string requestUrl = $"{baseUrl}/api/v1.0/taxpayer/validate/{tin}?idType={idType}&idValue={idNo}";

                var client = _httpClientFactory.CreateClient();

                string accessToken = await GetLhdnAccessTokenAsync();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync(requestUrl);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LHDN API error during public customer creation");
                return false;
            }
        }

        private async Task<string> GetLhdnAccessTokenAsync()
        {
            // ✅ 1. Read credentials directly from appsettings.json
            string? myCompanyTin = _configuration["TaxpayerValidationSettings:DefaultTIN"];
            string? clientId = _configuration["LHDNApiConfig:ClientId"];
            string? clientSecret = _configuration["LHDNApiConfig:ClientSecret"];
            string scope = _configuration["LHDNApiConfig:Scope"] ?? "InvoicingAPI";

            // Construct the token URL based on your base URL and token endpoint
            string baseUrl = _configuration["LHDNApiConfig:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";
            string tokenEndpoint = _configuration["LHDNApiConfig:TokenEndpoint"]?.TrimStart('/') ?? "connect/token";
            string tokenUrl = $"{baseUrl}/{tokenEndpoint}";

            // ✅ 2. Check the database for an existing, unexpired token (5-minute buffer)
            var existingToken = await _context.Set<LHDNToken>()
                .Where(t => t.TIN == myCompanyTin && t.ExpiryTime > DateTime.UtcNow.AddMinutes(5))
                .FirstOrDefaultAsync();

            if (existingToken != null)
            {
                return existingToken.AccessToken;
            }

            // ✅ 3. Call LHDN Identity Server for a new one
            var client = _httpClientFactory.CreateClient();
            var requestContent = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("client_id", clientId ?? ""),
        new KeyValuePair<string, string>("client_secret", clientSecret ?? ""),
        new KeyValuePair<string, string>("grant_type", "client_credentials"),
        new KeyValuePair<string, string>("scope", scope)
    });

            var response = await client.PostAsync(tokenUrl, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retrieve LHDN Access Token. Status: {response.StatusCode}");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
            {
                throw new Exception("LHDN Identity server returned empty token.");
            }

            // ✅ 4. Save/Update the token in the database
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

            // ✅ 5. Add to Audit Log
            _context.Add(new LHDNTokenLog
            {
                TIN = myCompanyTin ?? "",
                IssuedAt = DateTime.UtcNow,
                ExpiryTime = dbToken.ExpiryTime,
                ClientIdUsed = clientId ?? "",
                Source = "ValidateBuyerTIN"
            });

            await _context.SaveChangesAsync();

            return tokenResponse.access_token;
        }

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
                    Selected = PublicCustomer != null && PublicCustomer.IndustryClassificationCode == msic.Value
                }).ToList();
        }
    }
}