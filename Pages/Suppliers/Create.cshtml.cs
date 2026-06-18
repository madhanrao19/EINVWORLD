using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using eInvWorld.Data;
using eInvWorld.Areas.Identity.Pages.Account;
using eInvWorld.Models.InputModel;
using eInvWorld.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using eInvWorld.Models;
using System.Security.Claims;
// Added Namespaces required for HTTP Client and LHDN Auth Models
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using eInvWorld.Models.Auth;
using eInvWorld.Models.Audit;

namespace eInvWorld.Pages.Suppliers
{
    [Authorize(Roles = "Admin,Supplier")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
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
            ILogger<CreateModel> logger)
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
            // ✅ SECURITY CHECK: If User is Supplier, they MUST have from="lead"
            if (User.IsInRole("Supplier") && from != "lead")
            {
                return Redirect("/Identity/Account/AccessDenied"); // Or return Forbid();
            }

            From = from;

            PartyInfo = new PartyInfo
            {
                IsAdminCreated = User.IsInRole("Admin"),
                IsApproved = DetermineApprovalFlag(from)
            };

            PopulateDropdowns();
            return Page();
        }

        [BindProperty]
        public PartyInfo PartyInfo { get; set; } = default!;

        [BindProperty]
        public IFormFile? LogoFile { get; set; }

        public List<SelectListItem> StateOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CountryOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> IdTypesOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> MSICOptions { get; set; } = new();

        public bool IsAdmin => User.IsInRole("Admin");

        public async Task<IActionResult> OnPostAsync(string? from)
        {
            // ✅ SECURITY CHECK: Prevent Suppliers from posting to standard create
            if (User.IsInRole("Supplier") && from != "lead")
            {
                return Redirect("/AccessDenied");
            }

            // Repopulate dropdowns on POST
            PopulateDropdowns();

            // Ensure BizDescription is not NULL before inserting
            if (PartyInfo.BizDescription == null)
            {
                PartyInfo.BizDescription = "";
            }

            PartyInfo.IsAdminCreated = IsAdmin;
            PartyInfo.IsApproved = DetermineApprovalFlag(from);

            // Ensure CreatedBy is not NULL before validation
            PartyInfo.CreatedBy = string.IsNullOrWhiteSpace(PartyInfo.CreatedBy)
                ? (User.Identity?.Name ?? "System")
                : PartyInfo.CreatedBy;

            PartyInfo.CreatedDate = DateTime.Now;

            var registrationType = await _context.RegistrationTypes
                .FirstOrDefaultAsync(r => r.Code == PartyInfo.RegTypeCode);

            if (registrationType == null)
            {
                _logger.LogWarning("Invalid RegTypeCode: {RegTypeCode}", PartyInfo.RegTypeCode);
                ModelState.AddModelError("PartyInfo.RegTypeCode", "Invalid Registration Type selected.");
                return Page();
            }

            // ✅ Check for existing TIN (Excluding General TINs)
            var generalTins = new[] { "EI00000000010", "EI00000000020", "EI00000000030", "EI00000000040" };
            if (!generalTins.Contains(PartyInfo.TIN))
            {
                var existingTIN = await _context.PartyInfos
                    .AnyAsync(p => p.TIN == PartyInfo.TIN);

                if (existingTIN)
                {
                    TempData["ErrorMessage"] = $"Cannot create: A company with the TIN '{PartyInfo.TIN}' already exists in the system.";
                    ModelState.AddModelError("PartyInfo.TIN", "A record with this TIN already exists.");
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

            // Handle Logo Upload
            string logoRelativePath = "/assets/images/users/multi-user.jpg";
            if (LogoFile != null && LogoFile.Length > 0)
            {
                var uploadsFolder = _filePathConfig.CompanyLogosFolder;
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(LogoFile.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await LogoFile.CopyToAsync(fileStream);
                }

                logoRelativePath = $"/api/companies/logos/{uniqueFileName}";
            }

            var partyInfo = new PartyInfo
            {
                IndustryClassificationCode = PartyInfo.IndustryClassificationCode,
                BizDescription = PartyInfo.BizDescription,
                CompanyName = PartyInfo.CompanyName,
                TIN = PartyInfo.TIN,
                RegTypeCode = registrationType.Code,
                RegNo = PartyInfo.RegNo,
                SST = PartyInfo.SST,
                TTX = PartyInfo.TTX,
                Email = PartyInfo.Email,
                Addr1 = PartyInfo.Addr1,
                Addr2 = null,
                Addr3 = null,
                PostalCode = PartyInfo.PostalCode,
                CityName = PartyInfo.CityName,
                StateCode = PartyInfo.StateCode,
                CountryCode = PartyInfo.CountryCode,
                PhoneNo = PartyInfo.PhoneNo,
                FaxNo = PartyInfo.FaxNo,
                Remarks = PartyInfo.Remarks,
                IsActive = PartyInfo.IsActive,
                CreatedBy = User.Identity?.Name ?? "System",
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
                UpdatedBy = User.Identity?.Name ?? "System",
                IsAdminCreated = true,
                InviteCode = null,
                LogoPath = logoRelativePath,
                IsApproved = PartyInfo.IsApproved,
                OldRegNo = PartyInfo.OldRegNo,
                BankAccountNo = PartyInfo.BankAccountNo,
                BankName = PartyInfo.BankName,
                Attention = PartyInfo.Attention,
                PaymentTerms = PartyInfo.PaymentTerms
            };

            _context.PartyInfos.Add(partyInfo);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Company successfully created!";

            if (IsAdmin)
            {
                return from == "lead" ? RedirectToPage("/Lead/List") : RedirectToPage("./Index");
            }

            // ✅ Redirect Suppliers to the Lead List
            if (User.IsInRole("Supplier"))
                return RedirectToPage("/Lead/List");

            return RedirectToPage("./Index");
        }

        // --- NEW: API Validation Endpoints & Logic Additions ---

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
                string baseUrl = _configuration["LHDNApiConfig:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";
                string requestUrl = $"{baseUrl}/api/v1.0/taxpayer/validate/{tin}?idType={idType}&idValue={idNo}";

                var client = _httpClientFactory.CreateClient();

                string accessToken = await GetLhdnAccessTokenAsync();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync(requestUrl);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LHDN API error during supplier creation");
                return false;
            }
        }

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

            if (existingToken != null)
            {
                return existingToken.AccessToken;
            }

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
                Source = "ValidateCompanyTIN"
            });

            await _context.SaveChangesAsync();

            return tokenResponse.access_token;
        }

        private bool DetermineApprovalFlag(string? from)
        {
            if (User.IsInRole("Supplier")) return false;
            if (IsAdmin)
                return from == "lead" ? false : true;
            return false;
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
                    Selected = PartyInfo != null && PartyInfo.IndustryClassificationCode == msic.Value
                }).ToList();
        }
    }
}