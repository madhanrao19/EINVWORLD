// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using eInvWorld.Helpers;

namespace eInvWorld.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : SecureFormPageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly EmailService _emailService;
        private readonly string _accountBaseUrl;
        private readonly string _registerAccBaseUrl;
        private readonly string _contactBaseUrl;
        private readonly IConfiguration _configuration;



        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            EmailService emailService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory // ✅ Add this
        ) : base(configuration, httpClientFactory, logger) // ✅ Pass to SecureFormPageModel)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailService = emailService;
            _configuration = configuration;

            // Retrieve Account URL from configuration
            _accountBaseUrl = _configuration["EmailConfiguration:EmailBaseUrls:AccountBaseUrl"];
            _registerAccBaseUrl = _configuration["EmailConfiguration:EmailBaseUrls:RegisterAccBaseUrl"];
            _contactBaseUrl = _configuration["EmailConfiguration:EmailBaseUrls:ContactBaseUrl"];

        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            /// [Required]
            //[Required]

            //[Display(Name = "User Name")]
            //public string UserName { get; set; }

            [Required]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }  // ✅ Add Full Name

            [Required]
            [Phone]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; }

            [StringLength(100, ErrorMessage = "Position must be maximum {1} characters long.")]
            [Display(Name = "Position/Job Title")]
            public string Position { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        private string BuildEmailUrl(string relativeOrFullPath)
        {
            var baseUrl = _configuration["EmailConfiguration:EmailBaseUrls:BaseUrl"];
            if (Uri.TryCreate(relativeOrFullPath, UriKind.Absolute, out var fullUri))
                return fullUri.ToString();

            return $"{baseUrl.TrimEnd('/')}/{relativeOrFullPath.TrimStart('/')}";
        }



        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // ✅ Initialize the Input model
            Input = new InputModel();

            // ✅ Safe assignment with null checks
            if (TempData.ContainsKey("RegisterEmail"))
            {
                Input.Email = TempData["RegisterEmail"]?.ToString();
            }
            if (TempData.ContainsKey("RegisterFullName"))
            {
                Input.FullName = TempData["RegisterFullName"]?.ToString();
            }
            if (TempData.ContainsKey("RegisterFullName"))
            {
                Input.FullName = TempData["RegisterFullName"]?.ToString();
            }

        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // 🛡 Shared Honeypot & Turnstile checks (via SecureFormPageModel)
            if (IsHoneypotTriggered())
            {
                _logger.LogWarning("🚫 Honeypot triggered on register.");
                ModelState.AddModelError(string.Empty, "Bot detection triggered.");
                return Page();
            }

            if (!await IsTurnstileValidAsync())
            {
                _logger.LogWarning("🚫 Turnstile verification failed on register.");
                ModelState.AddModelError(string.Empty, "Captcha verification failed.");
                return Page();
            }


            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                // ✅ Save Full Name
                user.FullName = Input.FullName;

                // ✅ Save Phone Number
                user.PhoneNumber = Input.PhoneNumber;

                // ✅ Save Position
                user.Position = Input.Position;

                // ✅ ADD AUDIT FIELDS
                user.UpdatedBy = "Self-Registered";
                user.UpdatedDate = DateTime.Now;

                var result = await _userManager.CreateAsync(user, Input.Password);


                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    //_emailService.SendEmail(Input.Email, "Confirm your email",
                    //    $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
                    //Console.WriteLine($"📩 Confirmation email sent to {Input.Email}");
                    string accountLink = BuildEmailUrl(_accountBaseUrl);
                    string contactLink = BuildEmailUrl(_contactBaseUrl);

                    string emailBody = GenerateRegisterAccEmailBody(Input.FullName, callbackUrl, accountLink, contactLink);

                    await _emailService.SendEmail(Input.Email, "Confirm Your Email", emailBody);

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private string GenerateRegisterAccEmailBody(string recipientName, string confirmLink, string accountLink, string contactLink)
        {

            string template = LoadEmailTemplate("RegisterEmailTemplate.html");

            return template
                .Replace("{{RecipientName}}", recipientName)
                .Replace("{{ConfirmLink}}", confirmLink)
                .Replace("{{LogoUrl}}", "cid:einvworld-logo")
                .Replace("{{AccountLink}}", accountLink ?? "#")
                .Replace("{{ContactLink}}", contactLink ?? "#")
                .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());

        }

        private string LoadEmailTemplate(string templateName)
        {
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", templateName);
            return System.IO.File.ReadAllText(templatePath);
        }

        private void SendEmail(string toEmail, string subject, string body, string pdfFilePath = null)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("EmailConfiguration:Default");

                _logger.LogInformation("Sending email to {ToEmail} via SMTP server {SmtpServer}", toEmail, smtpSettings["SmtpServer"]);

                using var smtpClient = new SmtpClient(smtpSettings["SmtpServer"])
                {
                    Port = int.Parse(smtpSettings["SmtpPort"]),
                    Credentials = new NetworkCredential(smtpSettings["SmtpUsername"], smtpSettings["SmtpPassword"]),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpSettings["SmtpUsername"], smtpSettings["FromEmailName"]),
                    Subject = subject,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                // ✅ Ensure Logo Uses CID
                string logoCid = "einvworld-logo";
                //body = body.Replace("{{LogoUrl}}", $"cid:{logoCid}");

                AlternateView htmlView = AlternateView.CreateAlternateViewFromString(body, null, "text/html");

                // ✅ Embed Logo as Inline Image
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "einvworld-logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    LinkedResource logoResource = new LinkedResource(logoPath)
                    {
                        ContentId = logoCid,
                        TransferEncoding = System.Net.Mime.TransferEncoding.Base64
                    };
                    logoResource.ContentType.MediaType = "image/png";
                    htmlView.LinkedResources.Add(logoResource);
                }
                else
                {
                    _logger.LogWarning("Logo image not found at {ImagePath}", logoPath);
                }

                mailMessage.AlternateViews.Add(htmlView);

                // ✅ Attach PDF if Provided
                if (!string.IsNullOrWhiteSpace(pdfFilePath) && System.IO.File.Exists(pdfFilePath))
                {
                    _logger.LogInformation("Attaching PDF to email: {PdfFilePath}", pdfFilePath);
                    mailMessage.Attachments.Add(new Attachment(pdfFilePath));
                }

                smtpClient.Send(mailMessage);
                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending email to {ToEmail}", toEmail);
                throw;
            }
        }


        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }

        

    }
}
