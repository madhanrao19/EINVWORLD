using eInvWorld.Data;
using eInvWorld.Models.Background;
using Microsoft.AspNetCore.Authorization;
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
    }
}
