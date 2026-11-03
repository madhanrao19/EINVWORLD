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
    public class ContactModel : eInvWorld.Helpers.SecureFormPageModel
    {
        private readonly EmailService _emailService;
        private readonly EmailConfiguration _emailConfig;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ContactModel> _logger;

        public ContactModel(EmailService emailService, IOptions<EmailConfiguration> emailConfig, IConfiguration configuration, ApplicationDbContext dbContext, IHttpClientFactory httpClientFactory, ILogger<ContactModel> logger)
            : base(configuration, httpClientFactory, logger)
        {
            _emailService = emailService;
            _emailConfig = emailConfig.Value;
            _configuration = configuration;
            _dbContext = dbContext;
            _logger = logger;
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
            ViewData["TurnstileSiteKey"] = _configuration["Turnstile:SiteKey"];
            if (IsHoneypotTriggered())
            {
                _logger.LogWarning("Honeypot triggered on contact form.");
                ErrorMessage = "Bot detection triggered.";
                return Page();
            }

            if (!await IsTurnstileValidAsync())
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
                var adminSubject = _emailConfig.ContactUsEmailSettings.AdminSubject;

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
                await _emailService.SendEmail(toEmail, adminSubject, adminBody);
                // No copy to the typed address: anyone could type someone else's and use this form as a spam relay.

                SuccessMessage = "Your message has been sent successfully! We'll get back to you soon.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Contact form submission failed.");
                ErrorMessage = "Sorry, we couldn't send your message. Please try again later.";
                return Page();
            }
        }
    }

    public class ContactFormModel
    {
        [Required, StringLength(100)]
        public required string Name { get; set; }

        [Required, StringLength(150)]
        public required string Company { get; set; }

        [Required, StringLength(30)]
        public required string Telephone { get; set; }

        [Required, EmailAddress, StringLength(256)]
        public required string Email { get; set; }

        [Required, StringLength(4000)]
        public required string Message { get; set; }
    }

}
