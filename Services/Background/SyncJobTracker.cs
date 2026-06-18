using eInvWorld.Data;
using eInvWorld.Models.Background;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Services.Background
{
    public interface ISyncJobTracker
    {
        /// <summary>Creates a job row in the Queued state and returns its id.</summary>
        Task<int> CreateAsync(string tin, string jobType, string? triggeredBy);
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

        public SyncJobTracker(ApplicationDbContext db, ILogger<SyncJobTracker> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<int> CreateAsync(string tin, string jobType, string? triggeredBy)
        {
            try
            {
                var job = new SyncJob
                {
                    Tin = tin,
                    JobType = jobType,
                    Status = SyncJobStatus.Queued,
                    QueuedAtUtc = DateTime.UtcNow,
                    TriggeredBy = triggeredBy
                };
                _db.Set<SyncJob>().Add(job);
                await _db.SaveChangesAsync();
                return job.Id;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to create SyncJob row for TIN {Tin}", tin);
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
            if (jobId <= 0) return; // creation failed earlier — nothing to update
            try
            {
                var job = await _db.Set<SyncJob>().FirstOrDefaultAsync(j => j.Id == jobId);
                if (job == null) return;
                mutate(job);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to update SyncJob {JobId}", jobId);
            }
        }

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);
    }
}
