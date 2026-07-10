using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.InputModel;
using EINVWORLD.Models.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;

namespace eInvWorld.Services
{
    public class EInvoiceNotificationService : IEInvoiceNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EInvoiceNotificationService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly EmailBaseUrls _emailBaseUrls;
        private readonly string _generatedPdfFolder;


        public EInvoiceNotificationService(HttpClient httpClient, IConfiguration configuration,
                ILogger<EInvoiceNotificationService> logger, IServiceScopeFactory serviceScopeFactory,
                IOptions<EmailBaseUrls> emailBaseUrlsOptions,
                IOptions<FilePathConfig> filePathOptions)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration;
            _logger = logger;

            _emailBaseUrls = emailBaseUrlsOptions?.Value ?? throw new ArgumentNullException(nameof(emailBaseUrlsOptions));
            _generatedPdfFolder = filePathOptions?.Value?.GeneratedPdfFolder ?? throw new InvalidOperationException("Missing GeneratedPdfFolder setting");
            _serviceScopeFactory = serviceScopeFactory;

            if (!string.IsNullOrWhiteSpace(_emailBaseUrls.BaseUrl))
            {
                _httpClient.BaseAddress = new Uri(_emailBaseUrls.BaseUrl);
            }
        }

        private string BuildEmailUrl(string relativeOrFullPath)
        {
            if (Uri.TryCreate(relativeOrFullPath, UriKind.Absolute, out var fullUri))
            {
                return fullUri.ToString(); // Already a full URL
            }

            if (_emailBaseUrls == null || string.IsNullOrWhiteSpace(_emailBaseUrls.BaseUrl))
            {
                throw new InvalidOperationException("Email base URL is not configured properly.");
            }

            return $"{_emailBaseUrls.BaseUrl.TrimEnd('/')}/{relativeOrFullPath.TrimStart('/')}";
        }


        private string InvoiceLink(string documentId) =>
            BuildEmailUrl($"{_emailBaseUrls.InvoiceBaseUrl}{documentId}?fromEmail=true");

        private string AccountLink => BuildEmailUrl(_emailBaseUrls.AccountBaseUrl);
        private string ContactLink => BuildEmailUrl(_emailBaseUrls.ContactBaseUrl);


        public async Task SendValidatedNotificationEmail(string recipientName, PartyInfo? buyer, PartyInfo? supplier, string documentId, DateTime issueDate, DateTime validatedTimestamp, PublicCustomer? publicCustomer = null)
        {
            try
            {
                string baseSubject = _configuration["EmailConfiguration:ValidatedEmailSettings:Subject"]
                    ?? throw new InvalidOperationException("Missing configuration: ValidatedEmailSettings:Subject");

                var emailGeneratedTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
                string subject = $"{baseSubject} | {documentId} | {emailGeneratedTime:dd-MM-yyyy hh:mm tt}";

                string adminEmailRaw = _configuration["EmailConfiguration:Default:GlobalBccEmail"]
                    ?? throw new InvalidOperationException("Missing configuration: EmailConfiguration:Default:GlobalBccEmail");

                if (string.IsNullOrWhiteSpace(adminEmailRaw))
                    throw new InvalidOperationException("❌ GlobalBccEmail is empty. Please configure a valid BCC email.");

                // ✅ Use normal .Split because it's guaranteed non-null and non-empty
                string[] adminEmails = adminEmailRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                string invoiceLink = InvoiceLink(documentId);
                string accountLink = AccountLink;
                string contactLink = ContactLink;
                string? pdfFilePath = Path.Combine(_generatedPdfFolder, $"{documentId}.pdf");

                if (!File.Exists(pdfFilePath))
                {
                    _logger.LogWarning("PDF file not found at {PdfFilePath}", pdfFilePath);
                    pdfFilePath = null;
                }

                _logger.LogInformation("Preparing validated invoice email for document {DocumentId}", documentId);

                // Buyer may be a registered party (PartyInfo) OR a public/one-off customer. Fall back to the
                // PublicCustomer so B2C validated invoices still reach the buyer, not only the supplier.
                var buyerEmail = !string.IsNullOrWhiteSpace(buyer?.Email) ? buyer!.Email : publicCustomer?.Email;
                var buyerName = buyer?.CompanyName ?? publicCustomer?.CompanyName ?? "Valued Customer";
                if (IsValidEmail(buyerEmail))
                {
                    var body = GenerateValidatedEmailBody(buyerName, documentId, issueDate, validatedTimestamp, invoiceLink, accountLink, contactLink);
                    SendEmailWithBcc(buyerEmail!, subject, body, pdfFilePath, adminEmails);
                    LogInvoiceHistory(documentId, "EmailSent", "System", $"To Buyer: {buyerEmail}");
                }

                var supplierEmail = supplier?.Email;
                if (IsValidEmail(supplierEmail))
                {
                    var body = GenerateValidatedEmailBody(supplier?.CompanyName ?? "Valued Supplier", documentId, issueDate, validatedTimestamp, invoiceLink, accountLink, contactLink);
                    SendEmailWithBcc(supplierEmail!, subject, body, pdfFilePath, adminEmails);
                    LogInvoiceHistory(documentId, "EmailSent", "System", $"To Supplier: {supplierEmail}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending validated invoice email.");
            }
        }


        public void SendRejectionNotificationEmail(PartyInfo buyer, PartyInfo supplier, string documentId, string rejectionReason, DateTime rejectedTimestamp)
        {
            try
            {
                string baseSubject = _configuration["EmailConfiguration:RequestRejectEmailSettings:Subject"]
                    ?? throw new InvalidOperationException("Missing configuration: RequestRejectEmailSettings:Subject");

                var emailGeneratedTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
                string subject = $"{baseSubject} | {documentId} | {emailGeneratedTime:dd-MM-yyyy hh:mm tt}";

                string adminEmailRaw = _configuration["EmailConfiguration:Default:GlobalBccEmail"]
                    ?? throw new InvalidOperationException("Missing configuration: EmailConfiguration:Default:GlobalBccEmail");

                if (string.IsNullOrWhiteSpace(adminEmailRaw))
                    throw new InvalidOperationException("GlobalBccEmail is empty. Please configure a valid BCC email.");

                string[] adminEmails = adminEmailRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                string invoiceLink = InvoiceLink(documentId);
                string accountLink = AccountLink;
                string contactLink = ContactLink;
                string? pdfFilePath = Path.Combine(_generatedPdfFolder, $"{documentId}.pdf");

                if (!File.Exists(pdfFilePath))
                {
                    _logger.LogWarning("PDF file not found at {PdfFilePath}", pdfFilePath);
                    pdfFilePath = null;
                }

                var buyerEmail = buyer?.Email;
                if (IsValidEmail(buyerEmail))
                {
                    var body = GenerateRejectEmailBody(buyer?.CompanyName ?? "Valued Customer", documentId, rejectionReason, rejectedTimestamp, invoiceLink, accountLink, contactLink);
                    SendEmailWithBcc(buyerEmail!, subject, body, pdfFilePath, adminEmails);
                }

                var supplierEmail = supplier?.Email;
                if (IsValidEmail(supplierEmail))
                {
                    var body = GenerateRejectEmailBody(supplier?.CompanyName ?? "Valued Supplier", documentId, rejectionReason, rejectedTimestamp, invoiceLink, accountLink, contactLink);
                    SendEmailWithBcc(supplierEmail!, subject, body, pdfFilePath, adminEmails);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending rejection notification email.");
            }
        }


        public void SendCancellationNotificationEmail(PartyInfo buyer, PartyInfo supplier, string documentId, string cancellationReason, DateTime cancelledTimestamp)
        {
            try
            {
                string baseSubject = _configuration["EmailConfiguration:RequestCancelEmailSettings:Subject"]
                    ?? throw new InvalidOperationException("Missing configuration: RequestCancelEmailSettings:Subject");

                var emailGeneratedTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
                string subject = $"{baseSubject} | {documentId} | {emailGeneratedTime:dd-MM-yyyy hh:mm tt}";

                string adminEmailRaw = _configuration["EmailConfiguration:Default:GlobalBccEmail"]
                    ?? throw new InvalidOperationException("Missing configuration: EmailConfiguration:Default:GlobalBccEmail");

                if (string.IsNullOrWhiteSpace(adminEmailRaw))
                    throw new InvalidOperationException("GlobalBccEmail is empty. Please configure a valid BCC email.");

                string[] adminEmails = adminEmailRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                string invoiceLink = InvoiceLink(documentId);
                string accountLink = AccountLink;
                string contactLink = ContactLink;

                string? pdfFilePath = Path.Combine(_generatedPdfFolder, $"{documentId}.pdf");
                if (!File.Exists(pdfFilePath))
                {
                    _logger.LogWarning("PDF file not found at {PdfFilePath}", pdfFilePath);
                    pdfFilePath = null;
                }

                var buyerEmail = buyer?.Email;
                if (IsValidEmail(buyerEmail))
                {
                    var body = GenerateCancelEmailBody(buyer?.CompanyName ?? "Valued Customer", documentId, cancellationReason, cancelledTimestamp, invoiceLink, accountLink, contactLink);
                    SendEmailWithBcc(buyerEmail!, subject, body, pdfFilePath, adminEmails);
                }

                var supplierEmail = supplier?.Email;
                if (IsValidEmail(supplierEmail))
                {
                    var body = GenerateCancelEmailBody(supplier?.CompanyName ?? "Valued Supplier", documentId, cancellationReason, cancelledTimestamp, invoiceLink, accountLink, contactLink);
                    SendEmailWithBcc(supplierEmail!, subject, body, pdfFilePath, adminEmails);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending cancellation notification email.");
            }
        }


        private void SendEmailWithBcc(string toEmail, string subject, string body, string? pdfFilePath, IEnumerable<string> bccEmails)
        {
            var smtpSettings = _configuration.GetSection("EmailConfiguration:Default");

            // Guard on IsNullOrWhiteSpace, not just null: an unconfigured SMTP setting on the server is
            // typically a BLANK env var / config key, not a missing one. With a null-only check an empty
            // SmtpUsername slipped through to `new MailAddress(smtpUser, ...)` below and threw a cryptic
            // "The value cannot be an empty string (Parameter 'address')" — masking the real cause
            // (SMTP not configured) and silently dropping every validated/rejected/cancelled email.
            string smtpServer = !string.IsNullOrWhiteSpace(smtpSettings["SmtpServer"])
                ? smtpSettings["SmtpServer"]! : throw new InvalidOperationException("SMTP not configured: EmailConfiguration:Default:SmtpServer is empty. Set it via env var / user-secrets (see SECRETS-SETUP.md).");
            int smtpPort = int.TryParse(smtpSettings["SmtpPort"], out int port) ? port : throw new InvalidOperationException("SMTP not configured: EmailConfiguration:Default:SmtpPort is missing or not a number.");
            string smtpUser = !string.IsNullOrWhiteSpace(smtpSettings["SmtpUsername"])
                ? smtpSettings["SmtpUsername"]! : throw new InvalidOperationException("SMTP not configured: EmailConfiguration:Default:SmtpUsername is empty. Set it via env var / user-secrets (see SECRETS-SETUP.md).");
            string smtpPass = !string.IsNullOrWhiteSpace(smtpSettings["SmtpPassword"])
                ? smtpSettings["SmtpPassword"]! : throw new InvalidOperationException("SMTP not configured: EmailConfiguration:Default:SmtpPassword is empty. Set it via env var / user-secrets (see SECRETS-SETUP.md).");
            string fromName = smtpSettings["FromEmailName"] ?? "eInvWorld";

            using var smtpClient = new SmtpClient(smtpServer)
            {
                Port = smtpPort,
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpUser, fromName),
                Subject = subject,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            if (bccEmails != null)
            {
                foreach (var bcc in bccEmails)
                {
                    if (IsValidEmail(bcc))
                        mailMessage.Bcc.Add(bcc);
                }
            }

            string logoCid = "einvworld-logo";
            body = body.Replace("{{LogoUrl}}", $"cid:{logoCid}");

            AlternateView htmlView = AlternateView.CreateAlternateViewFromString(body, null, "text/html");

            string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "einvworld-logo.png");
            if (File.Exists(logoPath))
            {
                var logoResource = new LinkedResource(logoPath)
                {
                    ContentId = logoCid,
                    TransferEncoding = System.Net.Mime.TransferEncoding.Base64,
                    ContentType = { MediaType = "image/png" }
                };
                htmlView.LinkedResources.Add(logoResource);
            }
            else
            {
                _logger.LogWarning("Logo image not found at path: {LogoPath}", logoPath);
            }

            mailMessage.AlternateViews.Add(htmlView);

            if (!string.IsNullOrEmpty(pdfFilePath) && File.Exists(pdfFilePath))
            {
                mailMessage.Attachments.Add(new Attachment(pdfFilePath));
            }
            else if (!string.IsNullOrEmpty(pdfFilePath))
            {
                _logger.LogWarning("Attachment file not found at {PdfPath}", pdfFilePath);
            }

            smtpClient.Send(mailMessage);
            _logger.LogInformation("✅ Email sent successfully to {ToEmail}", toEmail);
        }


        private bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new MailAddress(email);
                return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }


        private string GenerateValidatedEmailBody(string recipientName, string documentId, DateTime issueDate, DateTime validatedTimestamp, string invoiceLink, string accountLink, string contactLink)
        {
            var placeholders = new Dictionary<string, string>
            {
                { "RecipientName", recipientName },
                { "DocumentId", documentId },
                { "IssueDate", issueDate.ToString("yyyy-MM-dd HH:mm:ss") },
                { "ValidatedTimestamp", validatedTimestamp.ToString("yyyy-MM-dd HH:mm:ss") },
                { "InvoiceLink", invoiceLink ?? "#" },
                { "AccountLink", accountLink ?? "#" },
                { "ContactLink", contactLink ?? "#" },
                { "Year", DateTime.Now.Year.ToString() }
            };

            string template = LoadEmailTemplate("InvoiceValidatedEmailTemplate.html");
            return ReplaceTemplatePlaceholders(template, placeholders);
        }

        private string GenerateRejectEmailBody(string recipientName, string documentId, string reason, DateTime timestamp, string invoiceLink, string accountLink, string contactLink)
        {
            return GenerateEmailBody("RejectEmailTemplate.html", recipientName, documentId, reason, timestamp, invoiceLink, accountLink, contactLink);
        }

        private string GenerateCancelEmailBody(string recipientName, string documentId, string reason, DateTime timestamp, string invoiceLink, string accountLink, string contactLink)
        {
            return GenerateEmailBody("CancelEmailTemplate.html", recipientName, documentId, reason, timestamp, invoiceLink, accountLink, contactLink);
        }

        private string GenerateEmailBody(string templateName, string recipientName, string documentId, string reason, DateTime timestamp, string invoiceLink, string accountLink, string contactLink)
        {
            var model = new EInvoiceNotificationModel
            {
                RecipientName = recipientName,
                DocumentId = documentId,
                RejectionReason = reason,
                RejectedTimestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                InvoiceLink = invoiceLink,
                AccountLink = accountLink,
                ContactLink = contactLink,
                Year = DateTime.Now.Year.ToString()
            };

            string template = LoadEmailTemplate(templateName);
            return ReplaceTemplatePlaceholders(template, model);
        }

        private string ReplaceTemplatePlaceholders(string template, Dictionary<string, string> placeholders)
        {
            foreach (var placeholder in placeholders)
            {
                template = template.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value ?? "");
            }
            return template;
        }

        private string ReplaceTemplatePlaceholders(string template, EInvoiceNotificationModel model)
        {
            return template
                .Replace("{{RecipientName}}", model.RecipientName ?? "")
                .Replace("{{DocumentId}}", model.DocumentId ?? "")
                .Replace("{{RejectionReason}}", model.RejectionReason ?? "")
                .Replace("{{RejectedTimestamp}}", model.RejectedTimestamp ?? "")
                .Replace("{{InvoiceLink}}", model.InvoiceLink ?? "#")
                .Replace("{{AccountLink}}", model.AccountLink ?? "#")
                .Replace("{{CancelLink}}", model.CancelLink ?? "#")
                .Replace("{{ContactLink}}", model.ContactLink ?? "#")
                .Replace("{{Year}}", model.Year ?? "#");
        }

        private string LoadEmailTemplate(string templateName)
        {
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", templateName);
            return File.ReadAllText(templatePath);
        }

        private void LogInvoiceHistory(string invoiceNo, string action, string performedBy, string? remarks = null)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.InvoiceHistories.Add(new InvoiceHistory
            {
                InvoiceNo = invoiceNo,
                Action = action,
                PerformedBy = performedBy,
                Remarks = remarks,
                Timestamp = DateTime.UtcNow
            });

            db.SaveChanges();
        }
    }
}
