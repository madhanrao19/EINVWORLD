using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Background;
using eInvWorld.Models.Settings;
using eInvWorld.Models.Webhooks;
using EINVWORLD.Services.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eInvWorld.Services.Webhooks
{
    /// <summary>
    /// Scans for invoices that have reached a terminal LHDN status and enqueues a durable
    /// <see cref="SyncJobType.WebhookDelivery"/> job for each enabled subscription belonging to the
    /// invoice's supplier or customer TIN. Fires exactly once per status transition via the invoice's
    /// <c>WebhookNotifiedStatus</c> marker. Delivery itself (HTTP POST, retry, dead-letter) is handled by
    /// <see cref="WebhookDeliveryJobHandler"/> on the durable queue.
    /// <para>Delivery is at-least-once — a crash between enqueue and marker-commit can re-enqueue — so
    /// receivers must treat the invoice-number + status as an idempotency key.</para>
    /// </summary>
    public interface IWebhookDispatchService
    {
        /// <summary>Scans and enqueues pending webhook deliveries; returns the number of jobs enqueued.</summary>
        Task<int> ScanAndEnqueueAsync(ApplicationDbContext db, CancellationToken ct = default);
    }

    public sealed class WebhookDispatchService : IWebhookDispatchService
    {
        /// <summary>Terminal LHDN statuses that trigger a webhook.</summary>
        private static readonly string[] TerminalStatuses = { "Valid", "Cancelled", "Rejected", "Invalid" };

        private const int BatchSize = 200;

        private readonly ISyncJobTracker _jobs;
        private readonly WebhookSettings _settings;
        private readonly ILogger<WebhookDispatchService> _logger;

        public WebhookDispatchService(
            ISyncJobTracker jobs,
            IOptions<WebhookSettings> settings,
            ILogger<WebhookDispatchService> logger)
        {
            _jobs = jobs;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<int> ScanAndEnqueueAsync(ApplicationDbContext db, CancellationToken ct = default)
        {
            if (!_settings.Enabled)
                return 0;

            // Cheap early-out: nothing to do without at least one enabled subscription.
            var enabled = await db.Set<WebhookSubscription>()
                .AsNoTracking()
                .Where(s => s.IsEnabled)
                .Select(s => new { s.Id, s.Tin })
                .ToListAsync(ct);
            if (enabled.Count == 0)
                return 0;

            var subsByTin = enabled
                .GroupBy(s => s.Tin, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), StringComparer.OrdinalIgnoreCase);

            // Invoices at a terminal status not yet notified for THAT status. Valid additionally requires
            // LongId (fully finalized, mirroring the email finalizer) so we don't fire prematurely.
            var pending = await db.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer)
                .Where(i => i.LHDNStatusId != null
                            && TerminalStatuses.Contains(i.LHDNStatusId)
                            && (i.WebhookNotifiedStatus == null || i.WebhookNotifiedStatus != i.LHDNStatusId)
                            && (i.LHDNStatusId != "Valid" || !string.IsNullOrWhiteSpace(i.LongId)))
                .OrderBy(i => i.LastUpdated)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (pending.Count == 0)
                return 0;

            var enqueued = 0;
            foreach (var invoice in pending)
            {
                ct.ThrowIfCancellationRequested();
                var status = invoice.LHDNStatusId!;

                // Candidate owning TINs: the company that sent the invoice and the one that received it.
                var tins = new[] { invoice.Supplier?.TIN, invoice.Customer?.TIN }
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t!)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var tin in tins)
                {
                    if (!subsByTin.TryGetValue(tin, out var subIds))
                        continue;

                    foreach (var subId in subIds)
                    {
                        var jobId = await _jobs.CreateAsync(
                            tin!, SyncJobType.WebhookDelivery, "System",
                            SyncJobPayload.CreateForWebhook(subId, invoice.InvoiceNo, status, invoice.UUID));
                        if (jobId > 0)
                            enqueued++;
                    }
                }

                // Mark handled for this status so we don't rescan it (fires again on the next transition).
                invoice.WebhookNotifiedStatus = status;
            }

            await db.SaveChangesAsync(ct);

            if (enqueued > 0)
                _logger.LogInformation("Webhook dispatcher enqueued {Count} delivery job(s) for {Invoices} invoice(s).",
                    enqueued, pending.Count);

            return enqueued;
        }
    }
}
