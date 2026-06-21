using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Background;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.Background
{
    /// <summary>
    /// Durable replacement for the in-memory background queue. Background sync/import work is now a
    /// row in <c>SyncJobs</c>; this worker polls for Queued rows, atomically claims one, dispatches it
    /// to the matching <see cref="ISyncJobHandler"/>, and on failure retries with exponential backoff
    /// up to MaxAttempts. Because the work is reconstructed from the persisted row, an IIS app-pool
    /// recycle, deploy, or server reboot no longer loses jobs — on startup any job left Running is
    /// recovered back to Queued (single-instance assumption: this is the only worker).
    /// </summary>
    public sealed class DurableSyncJobWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DurableSyncJobWorker> _log;

        private static readonly string InstanceId = $"{Environment.MachineName}:{Environment.ProcessId}";
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan IdleBackoffWhenTableMissing = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(30);

        private bool _tableMissing;

        public DurableSyncJobWorker(IServiceScopeFactory scopeFactory, ILogger<DurableSyncJobWorker> log)
        {
            _scopeFactory = scopeFactory;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("DurableSyncJobWorker started ({Instance})", InstanceId);
            await Task.Yield();

            await RecoverOrphanedJobsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                bool processed = false;
                try
                {
                    processed = await ProcessNextAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "DurableSyncJobWorker loop error");
                }

                if (!processed)
                {
                    var delay = _tableMissing ? IdleBackoffWhenTableMissing : PollInterval;
                    try { await Task.Delay(delay, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }

            _log.LogInformation("DurableSyncJobWorker stopping");
        }

        /// <summary>
        /// Single-instance recovery: on startup any job still marked Running was abandoned by the
        /// previous process. Re-queue it (or fail it if it has exhausted its attempts).
        /// </summary>
        private async Task RecoverOrphanedJobsAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;

                var recovered = await db.Database.ExecuteSqlRawAsync(
                    @"UPDATE SyncJobs
                      SET Status = CASE WHEN AttemptCount >= MaxAttempts THEN {1} ELSE {2} END,
                          NextRunAtUtc = {0},
                          FinishedAtUtc = CASE WHEN AttemptCount >= MaxAttempts THEN {0} ELSE FinishedAtUtc END,
                          Message = CASE WHEN AttemptCount >= MaxAttempts
                                         THEN N'Abandoned after restart; max attempts reached.' ELSE Message END,
                          LockedBy = NULL,
                          LockedUntilUtc = NULL
                      WHERE Status = {3}",
                    now, SyncJobStatus.Failed, SyncJobStatus.Queued, SyncJobStatus.Running);

                if (recovered > 0)
                    _log.LogWarning("Recovered {Count} orphaned sync job(s) left Running after a restart.", recovered);
            }
            catch (Exception ex) when (IsMissingTable(ex))
            {
                MarkTableMissing("recover orphaned jobs");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to recover orphaned sync jobs");
            }
        }

        /// <summary>
        /// Claims the oldest eligible Queued job, runs it, and records the outcome. Returns true if a
        /// job was processed (so the loop polls again immediately), false if the queue was empty.
        /// </summary>
        private async Task<bool> ProcessNextAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;
            SyncJob? job;

            try
            {
                // Atomic claim: lock the chosen row for the duration of a short transaction so a second
                // worker would skip it (UPDLOCK/READPAST). The lock is released immediately after the
                // claim commits — we do NOT hold a DB transaction across the (possibly long) job run.
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                job = (await db.Set<SyncJob>().FromSqlRaw(
                        @"SELECT TOP (1) * FROM SyncJobs WITH (UPDLOCK, READPAST, ROWLOCK)
                          WHERE Status = {0} AND (NextRunAtUtc IS NULL OR NextRunAtUtc <= {1})
                          ORDER BY Id",
                        SyncJobStatus.Queued, now)
                    .ToListAsync(ct)).FirstOrDefault();

                if (job is null)
                {
                    await tx.CommitAsync(ct);
                    return false;
                }

                job.Status = SyncJobStatus.Running;
                job.LockedBy = InstanceId;
                job.LockedUntilUtc = now.Add(LockDuration);
                job.StartedAtUtc ??= now;
                job.AttemptCount += 1;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex) when (IsMissingTable(ex))
            {
                MarkTableMissing("claim next job");
                return false;
            }

            // ── Run the job in its OWN scope so the heavy work (and any DbContext state it leaves
            // behind) can never corrupt the bookkeeping context we use to record the outcome. ──
            try
            {
                string result;
                using (var jobScope = _scopeFactory.CreateScope())
                {
                    var handler = jobScope.ServiceProvider.GetServices<ISyncJobHandler>()
                        .FirstOrDefault(h => string.Equals(h.JobType, job.JobType, StringComparison.OrdinalIgnoreCase));

                    if (handler is null)
                        throw new InvalidOperationException($"No handler registered for job type '{job.JobType}'.");

                    result = await handler.ExecuteAsync(job, ct);
                }

                job.Status = SyncJobStatus.Completed;
                job.FinishedAtUtc = DateTime.UtcNow;
                job.Message = Truncate(result, 2000);
                job.LockedBy = null;
                job.LockedUntilUtc = null;
                await db.SaveChangesAsync(ct);
                _log.LogInformation("Sync job {JobId} ({JobType}, TIN {Tin}) completed.", job.Id, job.JobType, job.Tin);
            }
            catch (Exception ex)
            {
                var now2 = DateTime.UtcNow;
                if (job.AttemptCount < job.MaxAttempts)
                {
                    // Exponential backoff: 2, 4, 8, 16 … capped at 30 minutes.
                    var backoffMinutes = Math.Min(30, Math.Pow(2, job.AttemptCount));
                    job.Status = SyncJobStatus.Queued;
                    job.NextRunAtUtc = now2.AddMinutes(backoffMinutes);
                    job.Message = Truncate($"Attempt {job.AttemptCount} failed, retrying: {ex.Message}", 2000);
                    _log.LogWarning(ex, "Sync job {JobId} ({JobType}) failed on attempt {Attempt}/{Max}; retrying in {Minutes}m.",
                        job.Id, job.JobType, job.AttemptCount, job.MaxAttempts, backoffMinutes);
                }
                else
                {
                    job.Status = SyncJobStatus.Failed;
                    job.FinishedAtUtc = now2;
                    job.Message = Truncate($"Failed after {job.AttemptCount} attempt(s): {ex.Message}", 2000);
                    _log.LogError(ex, "Sync job {JobId} ({JobType}) failed permanently after {Attempt} attempt(s).",
                        job.Id, job.JobType, job.AttemptCount);
                }
                job.LockedBy = null;
                job.LockedUntilUtc = null;
                try { await db.SaveChangesAsync(ct); }
                catch (Exception saveEx) { _log.LogError(saveEx, "Failed to persist outcome for sync job {JobId}", job.Id); }
            }

            return true;
        }

        private void MarkTableMissing(string context)
        {
            if (_tableMissing) return;
            _tableMissing = true;
            _log.LogWarning(
                "SyncJobs table not found — durable background jobs are paused until the pending migrations " +
                "are applied (run Migrations/Apply_AddSyncJobDurability.sql against this database). ({Context})",
                context);
        }

        // SQL Server error 208 = "Invalid object name" (table missing). EF wraps it in DbUpdateException.
        private static bool IsMissingTable(Exception ex) =>
            (ex as SqlException ?? ex.InnerException as SqlException)?.Number == 208;

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);
    }
}
