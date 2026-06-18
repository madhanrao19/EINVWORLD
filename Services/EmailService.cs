using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using eInvWorld.Models;
using System.Text.RegularExpressions;


namespace eInvWorld.Services
{
    public class EmailService
    {
        private readonly EmailConfiguration _emailConfig;
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration; 


        public EmailService(EmailConfiguration emailConfig, ILogger<EmailService> logger, IConfiguration configuration)
        {
            _emailConfig = emailConfig;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<string> LoadEmailTemplate(string filePath, Dictionary<string, string> placeholders)
        {
            var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates");
            var fullPath = Path.Combine(rootPath, filePath);

            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogError("❌ Email Template Not Found: {FilePath}", fullPath);
                return "<p><b>Error:</b> Email template not found.</p>";
            }

            var template = await System.IO.File.ReadAllTextAsync(fullPath);
            return ReplaceTemplatePlaceholders(template, placeholders);
        }

        private string ReplaceTemplatePlaceholders(string template, Dictionary<string, string> placeholders)
        {
            foreach (var placeholder in placeholders)
            {
                template = template.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value ?? "N/A");
            }
            return template;
        }

        //public void SendEmail(string toEmail, string subject, string body, string bccEmail = null)
        //{
        //    try
        //    {
        //        using var smtpClient = new SmtpClient(_emailConfig.Default.SmtpServer)
        //        {
        //            Port = _emailConfig.Default.SmtpPort,
        //            Credentials = new NetworkCredential(_emailConfig.Default.SmtpUsername, _emailConfig.Default.SmtpPassword),
        //            EnableSsl = true
        //        };

        //        var mailMessage = new MailMessage
        //        {
        //            From = new MailAddress(_emailConfig.Default.SmtpUsername, _emailConfig.Default.FromEmailName),
        //            Subject = subject,
        //            IsBodyHtml = true
        //        };

        //        mailMessage.To.Add(toEmail);
        //        if (!string.IsNullOrWhiteSpace(bccEmail))
        //        {
        //            mailMessage.Bcc.Add(bccEmail);
        //        }

        //        // Define a CID for the logo
        //        string logoCid = "einvworld-logo";

        //        // Replace the placeholder in the email body
        //        body = body.Replace("{{LogoUrl}}", $"cid:{logoCid}");

        //        // Create an alternate HTML view
        //        AlternateView htmlView = AlternateView.CreateAlternateViewFromString(body, null, "text/html");

        //        // Path to the logo image
        //        string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "einvworld-logo.png");

        //        if (File.Exists(logoPath))
        //        {
        //            LinkedResource logoResource = new LinkedResource(logoPath)
        //            {
        //                ContentId = logoCid,
        //                TransferEncoding = System.Net.Mime.TransferEncoding.Base64
        //            };
        //            logoResource.ContentType.MediaType = "image/png";
        //            htmlView.LinkedResources.Add(logoResource);
        //        }
        //        else
        //        {
        //            _logger.LogWarning("⚠️ Logo image not found at {ImagePath}", logoPath);
        //        }

        //        mailMessage.AlternateViews.Add(htmlView);

        //        _logger.LogInformation("📩 Sending email to {ToEmail} with subject {Subject}", toEmail, subject);
        //        smtpClient.Send(mailMessage);
        //        _logger.LogInformation("✅ Email sent successfully to {ToEmail}", toEmail);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "❌ Error sending email to {ToEmail}", toEmail);
        //    }
        //}



        public async Task<bool> SendEmail(string toEmail, string subject, string body, List<string>? bccEmails = null)
        {
            int maxRetries = 3;
            int delayBetweenRetriesMs = 2000; // 2 seconds between retries

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var smtpClient = new SmtpClient(_emailConfig.Default.SmtpServer)
                    {
                        Port = _emailConfig.Default.SmtpPort,
                        Credentials = new NetworkCredential(_emailConfig.Default.SmtpUsername, _emailConfig.Default.SmtpPassword),
                        EnableSsl = true
                    };

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_emailConfig.Default.SmtpUsername, _emailConfig.Default.FromEmailName),
                        Subject = subject,
                        IsBodyHtml = true
                    };

                    if (!string.IsNullOrWhiteSpace(toEmail))
                    {
                        mailMessage.To.Add(toEmail);
                    }
                    else if (bccEmails != null && bccEmails.Any())
                    {
                        // Use first BCC as fallback To (some SMTP servers require it)
                        mailMessage.To.Add(bccEmails.First());
                        _logger.LogWarning("📬 No 'To' address provided. Using first BCC as fallback: {FallbackTo}", bccEmails.First());
                    }
                    else
                    {
                        _logger.LogWarning("🚫 Email skipped: no 'To' or BCC recipients specified.");
                        return false;
                    }


                    // Logo inline
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

                    mailMessage.AlternateViews.Add(htmlView);

                    _logger.LogInformation("📩 Attempt {Attempt} - Sending email to {ToEmail}", attempt, toEmail);
                    await smtpClient.SendMailAsync(mailMessage);
                    _logger.LogInformation("✅ Email sent successfully on attempt {Attempt}", attempt);
                    return true; // Success
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Attempt {Attempt} failed to send email to {ToEmail}", attempt, toEmail);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError("🚫 All retries failed for {ToEmail}. Giving up.", toEmail);
                        return false;
                    }

                    // Wait before next retry
                    await Task.Delay(delayBetweenRetriesMs);
                }
            }

            return false; // Should never reach here
        }

        // ✨ Utility to validate email format
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
                return emailRegex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }
    }

}
