using eInvWorld.Data;
using eInvWorld.Models.Background;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class SyncJobsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public SyncJobsModel(ApplicationDbContext db) => _db = db;

        public List<SyncJob> Jobs { get; private set; } = new();
        public int RunningCount { get; private set; }
        public int QueuedCount { get; private set; }

        public async Task OnGetAsync()
        {
            Jobs = await _db.Set<SyncJob>()
                .AsNoTracking()
                .OrderByDescending(j => j.Id)
                .Take(100)
                .ToListAsync();

            RunningCount = Jobs.Count(j => j.Status == SyncJobStatus.Running);
            QueuedCount = Jobs.Count(j => j.Status == SyncJobStatus.Queued);
        }

        // Re-queue a Failed job for a fresh run (the DurableSyncJobWorker picks it up on its next poll).
        public async Task<IActionResult> OnPostRetryAsync(int id)
        {
            var job = await _db.Set<SyncJob>().FirstOrDefaultAsync(j => j.Id == id);
            if (job is null)
            {
                TempData["Message"] = "Job not found.";
            }
            else if (job.Status != SyncJobStatus.Failed)
            {
                TempData["Message"] = $"Only Failed jobs can be retried (job #{id} is {job.Status}).";
            }
            else
            {
                job.Status = SyncJobStatus.Queued;
                job.AttemptCount = 0;
                job.NextRunAtUtc = null;
                job.StartedAtUtc = null;
                job.FinishedAtUtc = null;
                job.LockedBy = null;
                job.LockedUntilUtc = null;
                job.Message = "Manually retried.";
                await _db.SaveChangesAsync();
                TempData["Message"] = $"✅ Job #{id} re-queued.";
            }
            return RedirectToPage();
        }

        // Cancel a job that has not started yet. A Running job can't be safely cancelled mid-flight.
        public async Task<IActionResult> OnPostCancelAsync(int id)
        {
            var job = await _db.Set<SyncJob>().FirstOrDefaultAsync(j => j.Id == id);
            if (job is null)
            {
                TempData["Message"] = "Job not found.";
            }
            else if (job.Status != SyncJobStatus.Queued)
            {
                TempData["Message"] = $"Only Queued jobs can be cancelled (job #{id} is {job.Status}).";
            }
            else
            {
                job.Status = SyncJobStatus.Cancelled;
                job.FinishedAtUtc = DateTime.UtcNow;
                job.Message = "Cancelled by admin.";
                await _db.SaveChangesAsync();
                TempData["Message"] = $"🛑 Job #{id} cancelled.";
            }
            return RedirectToPage();
        }
    }
}
