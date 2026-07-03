using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.Background
{
    /// <summary>
    /// Proactively emails an admin as the LHDN XAdES signing certificate approaches expiry, so it's caught
    /// before it silently starts rejecting submissions. Previously this was only visible by an admin
    /// manually visiting Admin -&gt; System Health. Mirrors <see cref="SyncFailureAlertService"/>'s shape:
    /// OFF unless explicitly enabled with a recipient configured, and throttled so it never spams once a
    /// day is enough warning. Config: CertExpiryAlerts:{Enabled,RecipientEmail,WarnDays,CheckHours,CooldownHours}.
    /// A missing/unreadable certificate is treated as "nothing to alert on" here — that failure mode is
    /// already a hard startup error via ProductionConfigValidator when SigningEnabled=true.
    /// </summary>
    public class CertExpiryAlertService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CertExpiryAlertService> _logger;

        private DateTime _lastAlertUtc = DateTime.MinValue;

        public CertExpiryAlertService(
            IServiceScopeFactory scopeFactory, IConfiguration config, IWebHostEnvironment env,
            ILogger<CertExpiryAlertService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _env = env;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("CertExpiryAlerts:Enabled", false))
            {
                _logger.LogInformation("CertExpiryAlertService disabled (CertExpiryAlerts:Enabled=false).");
                return;
            }
            if (!_config.GetValue("LHDNApiConfig:SigningEnabled", false))
            {
                _logger.LogInformation("CertExpiryAlertService: signing is disabled (LHDNApiConfig:SigningEnabled=false) — nothing to monitor.");
                return;
            }

            var recipient = _config["CertExpiryAlerts:RecipientEmail"];
            if (string.IsNullOrWhiteSpace(recipient))
            {
                _logger.LogWarning("CertExpiryAlerts enabled but RecipientEmail is empty — no alerts will be sent.");
                return;
            }

            var warnDays = Math.Max(1, _config.GetValue("CertExpiryAlerts:WarnDays", 30));
            var checkHours = Math.Max(1, _config.GetValue("CertExpiryAlerts:CheckHours", 24));
            var cooldown = TimeSpan.FromHours(Math.Max(1, _config.GetValue("CertExpiryAlerts:CooldownHours", 24)));

            _logger.LogInformation("✅ CertExpiryAlertService started (warn within {WarnDays}d, every {Hours}h, cooldown {Cooldown}h).",
                warnDays, checkHours, cooldown.TotalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await CheckOnceAsync(recipient!, warnDays, cooldown); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "CertExpiryAlertService check failed."); }

                try { await Task.Delay(TimeSpan.FromHours(checkHours), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task CheckOnceAsync(string recipient, int warnDays, TimeSpan cooldown)
        {
            var certPath = _config["LHDNApiConfig:CertPath"];
            if (string.IsNullOrWhiteSpace(certPath)) return; // ProductionConfigValidator already hard-fails this case at startup

            var full = Path.IsPathRooted(certPath) ? certPath : Path.Combine(_env.ContentRootPath, certPath);
            if (!File.Exists(full))
            {
                _logger.LogWarning("CertExpiryAlertService: signing cert file not found at {Path}.", full);
                return;
            }

            using var cert = X509CertificateLoader.LoadPkcs12FromFile(full, _config["LHDNApiConfig:CertPass"]);
            var daysLeft = (cert.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays;
            if (daysLeft >= warnDays) return; // healthy — nothing to alert on

            var now = DateTime.UtcNow;
            if ((now - _lastAlertUtc) < cooldown) return; // already warned recently

            var state = daysLeft < 0 ? "EXPIRED" : "expiring soon";
            var subject = daysLeft < 0
                ? "🔴 EINVWORLD: LHDN signing certificate has EXPIRED"
                : $"⚠️ EINVWORLD: LHDN signing certificate expires in {daysLeft:F0} day(s)";
            var body =
                $"<p>The LHDN XAdES signing certificate is <b>{state}</b>.</p>" +
                $"<p>Subject: {System.Net.WebUtility.HtmlEncode(cert.Subject)}<br/>" +
                $"Expires: {cert.NotAfter:yyyy-MM-dd} ({daysLeft:F0} day(s) from now)</p>" +
                "<p>Renew the certificate and update <b>LHDNApiConfig:CertPath</b>/<b>CertPass</b> before it lapses — " +
                "an expired certificate will cause every signed submission to be rejected. " +
                "See the certificate-rotation runbook.</p>";

            using var scope = _scopeFactory.CreateScope();
            var email = scope.ServiceProvider.GetRequiredService<EmailService>();
            var sent = await email.SendEmail(recipient, subject, body);

            if (sent)
            {
                _lastAlertUtc = now;
                _logger.LogWarning("Sent cert-expiry alert to {Recipient} ({State}, {Days} day(s)).", recipient, state, daysLeft);
            }
            else
            {
                _logger.LogError("Failed to send cert-expiry alert email to {Recipient}.", recipient);
            }
        }
    }
}
