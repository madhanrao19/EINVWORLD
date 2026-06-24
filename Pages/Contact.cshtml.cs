using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace eInvWorld.Pages
{
    public class ContactModel : PageModel
    {
        private readonly EmailService _emailService;
        private readonly EmailConfiguration _emailConfig;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;

        public ContactModel(EmailService emailService, IOptions<EmailConfiguration> emailConfig, IConfiguration configuration, ApplicationDbContext dbContext, IHttpClientFactory httpClientFactory)
        {
            _emailService = emailService;
            _emailConfig = emailConfig.Value;
            _configuration = configuration;
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public ContactFormModel? ContactForm { get; set; }

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            ViewData["TurnstileSiteKey"] = _configuration["Turnstile:SiteKey"];
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var turnstileResponse = Request.Form["cf-turnstile-response"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(turnstileResponse) || !await ValidateTurnstileAsync(turnstileResponse))
            {
                ErrorMessage = "Please complete the Turnstile verification.";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please fill out all required fields.";
                return Page();
            }

            if (ContactForm == null)
            {
                ErrorMessage = "Form data could not be loaded. Please try again.";
                return Page();
            }

            try
            {
                var contactUs = new ContactUs
                {
                    Name = ContactForm.Name,
                    Company = ContactForm.Company,
                    Telephone = ContactForm.Telephone,
                    Email = ContactForm.Email,
                    Message = ContactForm.Message,
                    SubmittedAt = DateTime.Now
                };

                _dbContext.ContactUs.Add(contactUs);
                await _dbContext.SaveChangesAsync();

                var toEmail = _emailConfig.ContactUsEmailSettings.ReceiverEmail;
                var userEmail = ContactForm.Email;
                var bccEmail = _emailConfig.Default.GlobalBccEmail;

                var adminSubject = _emailConfig.ContactUsEmailSettings.AdminSubject;
                var userSubject = _emailConfig.ContactUsEmailSettings.UserSubject;

                var placeholders = new Dictionary<string, string>
                    {
                        { "Name", ContactForm.Name },
                        { "Company", ContactForm.Company },
                        { "Telephone", ContactForm.Telephone },
                        { "Email", ContactForm.Email },
                        { "Message", ContactForm.Message },
                        { "Timestamp", DateTime.Now.ToString("dd/MM/yyyy hh:mm tt") },
                        { "Year", DateTime.Now.Year.ToString() }
                    };

                var adminBody = await _emailService.LoadEmailTemplate("ContactUs/AdminEmailTemplate.html", placeholders);
                var userBody = await _emailService.LoadEmailTemplate("ContactUs/UserEmailTemplate.html", placeholders);

                await _emailService.SendEmail(toEmail, adminSubject, adminBody);

                List<string> bccEmails = new();
                if (!string.IsNullOrWhiteSpace(bccEmail))
                {
                    bccEmails = bccEmail.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(x => x.Trim())
                                        .Where(x => !string.IsNullOrEmpty(x))
                                        .ToList();
                }


                await _emailService.SendEmail(userEmail, userSubject, userBody, bccEmails);





                SuccessMessage = "Your message has been sent successfully! A copy has been sent to your email.";
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error processing request: {ex.Message}";
                return Page();
            }
        }

        private async Task<bool> ValidateTurnstileAsync(string turnstileResponse)
        {
            var secretKey = _configuration["Turnstile:SecretKey"];
            var verifyUrl = _configuration["Turnstile:VerifyUrl"];

            // Use the pooled IHttpClientFactory client, not `new HttpClient()` — a new client per request
            // leaks sockets under load (socket exhaustion). Mirrors SecureFormPageModel.IsTurnstileValidAsync.
            var client = _httpClientFactory.CreateClient();

            var postData = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("secret", secretKey ?? string.Empty),
        new KeyValuePair<string, string>("response", turnstileResponse ?? string.Empty)
    });

            var response = await client.PostAsync(verifyUrl, postData);

            if (!response.IsSuccessStatusCode)
                return false;

            var jsonResponse = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(jsonResponse))
                return false;

            dynamic? result = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);
            return result?.success == true; // lowercase 'success'
        }


    }

    public class ContactFormModel
    {
        [Required]
        public required string Name { get; set; }

        [Required]
        public required string Company { get; set; }

        [Required]
        public required string Telephone { get; set; }

        [Required, EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Message { get; set; }
    }

}
