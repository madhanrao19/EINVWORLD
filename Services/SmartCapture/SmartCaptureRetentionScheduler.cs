using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Background;
using EINVWORLD.Services.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.SmartCapture
{
    /// <summary>
    /// Enqueues one SmartCaptureRetention sweep job per day onto the existing SyncJobs queue (so retention
    /// actually runs on a schedule rather than being a setting that does nothing — the whole point of
    /// treating retention as mandatory, not a follow-up). Idempotent: skips enqueueing if a
    /// SmartCaptureRetention job is already Queued or Running, so a slow sweep can't pile up duplicates.
    /// </summary>
    public sealed class SmartCaptureRetentionScheduler : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SmartCaptureOptions _options;
        private readonly ILogger<SmartCaptureRetentionScheduler> _logger;

        public SmartCaptureRetentionScheduler(IServiceScopeFactory scopeFactory, SmartCaptureOptions options, ILogger<SmartCaptureRetentionScheduler> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled) return; // no documents to retain if Smart Capture itself is off

            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnqueueIfDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SmartCaptureRetentionScheduler tick failed");
                }

                try { await Task.Delay(CheckInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task EnqueueIfDueAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pending = await context.SyncJobs.AnyAsync(j =>
                j.JobType == SyncJobType.SmartCaptureRetention &&
                (j.Status == SyncJobStatus.Queued || j.Status == SyncJobStatus.Running), ct);
            if (pending) return;

            var lastCompleted = await context.SyncJobs
                .Where(j => j.JobType == SyncJobType.SmartCaptureRetention && j.Status == SyncJobStatus.Completed)
                .OrderByDescending(j => j.FinishedAtUtc)
                .Select(j => j.FinishedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (lastCompleted is not null && DateTime.UtcNow - lastCompleted.Value < TimeSpan.FromDays(1))
                return;

            context.SyncJobs.Add(new SyncJob
            {
                Tin = "smart-capture-retention",
                JobType = SyncJobType.SmartCaptureRetention,
                Status = SyncJobStatus.Queued,
                QueuedAtUtc = DateTime.UtcNow,
                MaxAttempts = 3,
                TriggeredBy = "System (scheduler)",
            });
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Enqueued daily SmartCaptureRetention sweep.");
        }
    }
}
