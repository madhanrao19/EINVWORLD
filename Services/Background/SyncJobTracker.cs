using eInvWorld.Data;
using eInvWorld.Models.Background;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Services.Background
{
    public interface ISyncJobTracker
    {
        /// <summary>Creates a job row in the Queued state and returns its id. The row is the durable
        /// work item the background worker picks up; <paramref name="payloadJson"/> carries any
        /// parameters the handler needs (e.g. lookback days).</summary>
        Task<int> CreateAsync(string tin, string jobType, string? triggeredBy, string? payloadJson = null);
        Task MarkRunningAsync(int jobId);
        Task MarkCompletedAsync(int jobId, string? message, int importedCount = 0, int errorCount = 0);
        Task MarkFailedAsync(int jobId, string? message);
    }

    /// <summary>
    /// Persists the lifecycle of background sync/import jobs to the SyncJobs table.
    /// Scoped — background work items resolve it from their own DI scope. Each method is defensive:
    /// tracking failures are swallowed (logged) so they can never break the actual sync work.
    /// </summary>
    public sealed class SyncJobTracker : ISyncJobTracker
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SyncJobTracker> _log;

        // Process-wide latch: once we discover the SyncJobs table is missing (migration not applied),
        // skip all tracking DB calls instead of throwing/catching on every sync, and warn just once.
        private static volatile bool _tableMissing;

        public SyncJobTracker(ApplicationDbContext db, ILogger<SyncJobTracker> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<int> CreateAsync(string tin, string jobType, string? triggeredBy, string? payloadJson = null)
        {
            if (_tableMissing) return 0; // tracking disabled until the SyncJobs table exists
            try
            {
                var job = new SyncJob
                {
                    Tin = tin,
                    JobType = jobType,
                    Status = SyncJobStatus.Queued,
                    QueuedAtUtc = DateTime.UtcNow,
                    TriggeredBy = triggeredBy,
                    PayloadJson = payloadJson
                };
                _db.Set<SyncJob>().Add(job);
                await _db.SaveChangesAsync();
                return job.Id;
            }
            catch (Exception ex)
            {
                HandleTrackingException(ex, $"create SyncJob row for TIN {tin}");
                return 0;
            }
        }

        public Task MarkRunningAsync(int jobId) =>
            UpdateAsync(jobId, job =>
            {
                job.Status = SyncJobStatus.Running;
                job.StartedAtUtc = DateTime.UtcNow;
            });

        public Task MarkCompletedAsync(int jobId, string? message, int importedCount = 0, int errorCount = 0) =>
            UpdateAsync(jobId, job =>
            {
                job.Status = SyncJobStatus.Completed;
                job.FinishedAtUtc = DateTime.UtcNow;
                job.Message = Truncate(message, 2000);
                job.ImportedCount = importedCount;
                job.ErrorCount = errorCount;
            });

        public Task MarkFailedAsync(int jobId, string? message) =>
            UpdateAsync(jobId, job =>
            {
                job.Status = SyncJobStatus.Failed;
                job.FinishedAtUtc = DateTime.UtcNow;
                job.Message = Truncate(message, 2000);
            });

        private async Task UpdateAsync(int jobId, Action<SyncJob> mutate)
        {
            if (jobId <= 0 || _tableMissing) return; // creation failed/disabled — nothing to update
            try
            {
                var job = await _db.Set<SyncJob>().FirstOrDefaultAsync(j => j.Id == jobId);
                if (job == null) return;
                mutate(job);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                HandleTrackingException(ex, $"update SyncJob {jobId}");
            }
        }

        /// <summary>
        /// Logs a tracking failure. The common "SyncJobs table doesn't exist yet" case (SQL error 208,
        /// migrations not applied) is downgraded to a single one-time warning and disables further
        /// tracking attempts — sync work itself is unaffected. Any other failure is logged as an error.
        /// </summary>
        private void HandleTrackingException(Exception ex, string context)
        {
            if (IsMissingTable(ex))
            {
                if (!_tableMissing)
                {
                    _tableMissing = true;
                    _log.LogWarning(
                        "SyncJobs table not found — job tracking is disabled until the pending migrations " +
                        "are applied (run Migrations/Apply_AddSyncJobTable.sql against this database). " +
                        "Sync/import work itself is unaffected. ({Context})", context);
                }
                return;
            }

            _log.LogError(ex, "Sync job tracking failed: {Context}", context);
        }

        // SQL Server error 208 = "Invalid object name" (table missing). EF wraps it in DbUpdateException.
        private static bool IsMissingTable(Exception ex) =>
            (ex as SqlException ?? ex.InnerException as SqlException)?.Number == 208;

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);
    }
}
