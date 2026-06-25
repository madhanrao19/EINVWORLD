using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Background;
using eInvWorld.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.Background
{
    /// <summary>
    /// Proactively emails an admin when background sync jobs pile up in the Failed (dead-letter) state, so a
    /// silent degradation (e.g. LHDN down, token revoked) is noticed without anyone watching the Sync Jobs
    /// page. OFF unless explicitly enabled with a recipient configured, and throttled so it never spams:
    /// it alerts when the failed count crosses the threshold and re-alerts only when the backlog grows or a
    /// cooldown elapses. Config: SyncFailureAlerts:{Enabled,RecipientEmail,Threshold,CheckMinutes,CooldownHours}.
    /// </summary>
    public class SyncFailureAlertService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<SyncFailureAlertService> _logger;

        private int _lastAlertedCount;
        private DateTime _lastAlertUtc = DateTime.MinValue;

        public SyncFailureAlertService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<SyncFailureAlertService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("SyncFailureAlerts:Enabled", false))
            {
                _logger.LogInformation("SyncFailureAlertService disabled (SyncFailureAlerts:Enabled=false).");
                return;
            }

            var recipient = _config["SyncFailureAlerts:RecipientEmail"];
            if (string.IsNullOrWhiteSpace(recipient))
            {
                _logger.LogWarning("SyncFailureAlerts enabled but RecipientEmail is empty — no alerts will be sent.");
                return;
            }

            var checkMinutes = _config.GetValue("SyncFailureAlerts:CheckMinutes", 15);
            if (checkMinutes <= 0) checkMinutes = 15;
            var threshold = Math.Max(1, _config.GetValue("SyncFailureAlerts:Threshold", 1));
            var cooldown = TimeSpan.FromHours(Math.Max(1, _config.GetValue("SyncFailureAlerts:CooldownHours", 6)));

            _logger.LogInformation("✅ SyncFailureAlertService started (threshold {Threshold}, every {Min}m, cooldown {Hours}h).",
                threshold, checkMinutes, cooldown.TotalHours);

            // Small initial delay so a startup blip doesn't trigger an immediate alert.
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await CheckOnceAsync(recipient!, threshold, cooldown, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "SyncFailureAlertService check failed."); }

                try { await Task.Delay(TimeSpan.FromMinutes(checkMinutes), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task CheckOnceAsync(string recipient, int threshold, TimeSpan cooldown, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var failedCount = await db.Set<SyncJob>().AsNoTracking().CountAsync(j => j.Status == SyncJobStatus.Failed, ct);

            if (failedCount < threshold)
            {
                _lastAlertedCount = 0; // backlog cleared — a fresh pile-up will alert again
                return;
            }

            var now = DateTime.UtcNow;
            var grew = failedCount > _lastAlertedCount;
            var cooledDown = (now - _lastAlertUtc) >= cooldown;
            if (!grew && !cooledDown) return; // already alerted at this level and still within cooldown

            var recent = await db.Set<SyncJob>().AsNoTracking()
                .Where(j => j.Status == SyncJobStatus.Failed)
                .OrderByDescending(j => j.Id)
                .Take(10)
                .Select(j => new { j.Id, j.JobType, j.Tin, j.Message })
                .ToListAsync(ct);

            var rows = string.Join("", recent.Select(j =>
                $"<tr><td>{j.Id}</td><td>{System.Net.WebUtility.HtmlEncode(j.JobType)}</td>" +
                $"<td>{System.Net.WebUtility.HtmlEncode(j.Tin)}</td>" +
                $"<td>{System.Net.WebUtility.HtmlEncode(j.Message ?? "")}</td></tr>"));

            var body =
                $"<p><b>{failedCount}</b> background sync job(s) are in the <b>Failed</b> state on EINVWORLD.</p>" +
                "<p>Review and retry them on <b>Admin → Sync Jobs → failed</b>.</p>" +
                "<table border='1' cellpadding='4' cellspacing='0'>" +
                "<tr><th>#</th><th>Type</th><th>TIN</th><th>Message</th></tr>" + rows + "</table>";

            var email = scope.ServiceProvider.GetRequiredService<EmailService>();
            var sent = await email.SendEmail(recipient, $"⚠️ EINVWORLD: {failedCount} failed sync job(s)", body);

            if (sent)
            {
                _lastAlertedCount = failedCount;
                _lastAlertUtc = now;
                _logger.LogWarning("Sent sync-failure alert to {Recipient} ({Count} failed).", recipient, failedCount);
            }
            else
            {
                _logger.LogError("Failed to send sync-failure alert email to {Recipient}.", recipient);
            }
        }
    }
}
