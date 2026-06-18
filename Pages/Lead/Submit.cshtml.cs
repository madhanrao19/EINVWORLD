using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System;
using System.Linq;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using eInvWorld.Helpers;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Services;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using Microsoft.AspNetCore.Authorization;

namespace eInvWorld.Pages.Lead
{
    [AllowAnonymous]
    public class SubmitModel : SecureFormPageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SubmitModel> _logger;
        private readonly DropdownHelper _dropdownHelper; // Inject DropdownHelper
        private readonly EmailService _emailService;

        public SubmitModel(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration config, UserManager<ApplicationUser> userManager, ILogger<SubmitModel> logger, DropdownHelper dropdownHelper, EmailService emailService)
        : base(config, httpClientFactory, logger) // ✅ Required for base class
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _userManager = userManager;
            _logger = logger;
            _dropdownHelper = dropdownHelper;
            _emailService = emailService;

        }

        [BindProperty]
        public PartyInfo Input { get; set; } = null!;

        //[BindProperty]
        //public string AccountFullName { get; set; }

        //[BindProperty]
        //public string AccountEmail { get; set; }

        //[BindProperty]
        //[DataType(DataType.Password)]
        //public string AccountPassword { get; set; }

        public bool Success { get; set; } = false;

        public List<SelectListItem> StateOptions { get; set; } = new();
        public List<SelectListItem> CountryOptions { get; set; } = new();
        public List<SelectListItem> IdTypesOptions { get; set; } = new();
        public List<SelectListItem> MSICOptions { get; set; } = new();


        private void PopulateDropdowns()
        {

            StateOptions = _dropdownHelper.GetStateOptions();
            CountryOptions = _dropdownHelper.GetCountryOptions();
            IdTypesOptions = _dropdownHelper.GetIdTypesOptions();

            MSICOptions = _dropdownHelper.GetMSICOptions()
               .Where(msic => msic != null && msic.Value != null && msic.Text != null) // ✅ Avoid null entries
               .Select(msic => new SelectListItem
               {
                   Value = msic.Value,
                   Text = $"{msic.Value} - {msic.Text}",
                   Selected = Input != null && Input.IndustryClassificationCode == msic.Value
               }).ToList();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Input ??= new PartyInfo();
            PopulateDropdowns();
            return Page();
        }


        public async Task<IActionResult> OnPostAsync()
        {

            // Ensure BizDesciption is not NULL before inserting
            if (Input.BizDescription == null)
            {
                Input.BizDescription = "";
            }

            // 🛡 Shared Honeypot & Turnstile check
            if (IsHoneypotTriggered())
            {
                _logger.LogWarning("🚫 Honeypot triggered on login.");
                ModelState.AddModelError(string.Empty, "Bot detection triggered.");
                return Page();
            }

            if (!await IsTurnstileValidAsync())
            {
                _logger.LogWarning("🚫 Turnstile verification failed on login.");
                ModelState.AddModelError(string.Empty, "Turnstile verification failed.");
                return Page();
            }

            _logger.LogInformation("✅ Turnstile passed, continuing form save");

            bool isCreatingAccount = !string.IsNullOrWhiteSpace(Request.Form["createAccount"]);
            _logger.LogInformation("📦 Account Creation Requested: {CreateAccount}", isCreatingAccount);

            // Fix: initialize mandatory fields before validation
            if (Input != null)
            {
                Input.CreatedBy = User?.Identity?.Name ?? "CustomerInfo";
                Input.CreatedDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
                Input.IsAdminCreated = false;
                Input.IsApproved = false;
            }


            // Account validations
            //if (isCreatingAccount)
            //{
            //    //if (string.IsNullOrWhiteSpace(AccountEmail) || !new EmailAddressAttribute().IsValid(AccountEmail))
            //    //{
            //    //    ModelState.AddModelError("AccountEmail", "Invalid email address.");
            //    //}

            //    //if (string.IsNullOrWhiteSpace(AccountPassword) || AccountPassword.Length < 6)
            //    //{
            //    //    ModelState.AddModelError("AccountPassword", "Password must be at least 6 characters long.");
            //    //}

            //    //if (string.IsNullOrWhiteSpace(AccountFullName))
            //    //{
            //    //    ModelState.AddModelError("AccountFullName", "Full name is required.");
            //    //}
            //}
            //else
            //{
            //    ModelState.Remove(nameof(AccountEmail));
            //    ModelState.Remove(nameof(AccountPassword));
            //    ModelState.Remove(nameof(AccountFullName));
            //}

            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    _logger.LogWarning("❌ Validation Error on {Field}: {Message}", state.Key, error.ErrorMessage);
                }
            }

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return Page();
            }
            // 🛑 Duplicate TIN check
            if (!string.IsNullOrWhiteSpace(Input?.TIN))
            {
                var tinExists = await _context.PartyInfos
                    .AnyAsync(p => p.TIN == Input.TIN);

                if (tinExists)
                {
                    _logger.LogWarning("❌ TIN already exists: {TIN}", Input.TIN);
                    ModelState.AddModelError("Input.TIN", "This TIN is already registered.");
                    PopulateDropdowns();
                    return Page();
                }
            }


            _context.PartyInfos.Add(Input!);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ PartyInfo saved successfully with ID: {Id}", Input!.PartyInfoId);


            // 📧 Send email to Finance team if enabled
            if (_config.GetValue<bool>("EmailConfiguration:Notifications:EnableCustomerInfoEmails"))
            {
                string toEmail = _config["EmailConfiguration:CustomerSubmission:ReceiverEmail"] ?? "";
                string bcc = _config["EmailConfiguration:Default:GlobalBccEmail"] ?? "";
                string subjectTemplate = _config["EmailConfiguration:CustomerSubmission:Subject"] ?? "📩 New Customer Registration Submitted | {{CompanyName}}";
                string companyName = string.IsNullOrWhiteSpace(Input.CompanyName) ? "Unknown Company" : Input.CompanyName;
                string subject = subjectTemplate.Replace("{{CompanyName}}", companyName);
                string body = GenerateCustomerSummaryHtml(Input);

                try
                {
                    var bccList = bcc?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(x => x.Trim())
                                      .ToList();

                    // ✅ Case: Send to Receiver if exists
                    if (!string.IsNullOrWhiteSpace(toEmail))
                    {
                        await _emailService.SendEmail(toEmail, subject, body, bccList);
                        _logger.LogInformation("✅ Customer submission email sent to ReceiverEmail: {ToEmail}", toEmail);
                    }
                    // ✅ Case: No receiver, but BCC is available — send to first BCC
                    else if (bccList != null && bccList.Any())
                    {
                        string fallback = bccList.First();
                        await _emailService.SendEmail(fallback, subject, body, bccList);
                        _logger.LogInformation("✅ Customer submission email fallback sent to first BCC: {Bcc}", fallback);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Customer info email skipped — no valid ReceiverEmail or BCC.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to send customer info email.");
                }
            }


            if (isCreatingAccount)
            {
                if (!string.IsNullOrEmpty(Input.Email) && !string.IsNullOrEmpty(Input.CompanyName))
                {
                    TempData["RegisterEmail"] = Input.Email;
                    TempData["RegisterFullName"] = Input.CompanyName;
                    return Redirect("/register");
                }
            }

            Success = true;
            ModelState.Clear();
            return Page();
        }

        //private async Task<bool> VerifyTurnstile(string token)
        //{
        //    var client = _httpClientFactory.CreateClient();
        //    var response = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify",
        //        new FormUrlEncodedContent(new[]
        //        {
        //            new KeyValuePair<string, string>("secret", _config["Turnstile:SecretKey"] ?? ""),
        //            new KeyValuePair<string, string>("response", token)
        //        }));

        //    if (!response.IsSuccessStatusCode)
        //        return false;

        //    var result = await response.Content.ReadFromJsonAsync<TurnstileVerifyResponse>();
        //    return result != null && result.success;
        //}

        //private class TurnstileVerifyResponse
        //{
        //    public bool success { get; set; }
        //}

        private string GenerateCustomerSummaryHtml(PartyInfo input)
        {
            string template = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "CustomerSubmissionEmailTemplate.html"));

            return template
                .Replace("{{LogoUrl}}", "cid:einvworld-logo")
                .Replace("{{CompanyName}}", input.CompanyName ?? "-")
                .Replace("{{TIN}}", input.TIN ?? "-")
                .Replace("{{BRNOld}}", input.OldRegNo ?? "-")
                .Replace("{{BRNNew}}", input.RegNo ?? "-")
                .Replace("{{SSTNumber}}", input.SST ?? "-")
                .Replace("{{TourismTax}}", input.TTX ?? "-")
                .Replace("{{RegistrationType}}", input.RegTypeCode ?? "-")
                .Replace("{{MSICCode}}", input.IndustryClassificationCode ?? "-")
                .Replace("{{BusinessDescription}}", input.BizDescription ?? "-")

                .Replace("{{Email}}", input.Email ?? "-")
                .Replace("{{Phone}}", input.PhoneNo ?? "-")
                .Replace("{{Address1}}", input.Addr1 ?? "-")
                .Replace("{{Address2}}", input.Addr2 ?? "-")
                .Replace("{{City}}", input.CityName ?? "-")
                .Replace("{{State}}", input.StateCode ?? "-")
                .Replace("{{Postcode}}", input.PostalCode ?? "-")
                .Replace("{{Country}}", input.CountryCode ?? "-")

                .Replace("{{Timestamp}}", DateTime.Now.ToString("f"))
                .Replace("{{Year}}", DateTime.Now.Year.ToString());
        }

    }
}
