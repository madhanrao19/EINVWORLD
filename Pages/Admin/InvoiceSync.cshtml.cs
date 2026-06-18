using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using eInvWorld.Services;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Background;
using eInvWorld.Data; // Make sure namespace matches where InvoiceSyncHelper is
using eInvWorld.Models.Background;

namespace eInvWorld.Pages.Admin
{
    [Authorize(Roles = "Admin")] // 🔐 Protect this page
    public class InvoiceSyncModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ISyncJobTracker _jobTracker;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InvoiceSyncModel> _logger;

        public InvoiceSyncModel(
            ApplicationDbContext dbContext,
            IBackgroundTaskQueue taskQueue,
            IServiceScopeFactory scopeFactory,
            ISyncJobTracker jobTracker,
            IConfiguration configuration,
            ILogger<InvoiceSyncModel> logger)
        {
            _dbContext = dbContext;
            _taskQueue = taskQueue;
            _scopeFactory = scopeFactory;
            _jobTracker = jobTracker;
            _configuration = configuration;
            _logger = logger;
        }

        // "Run Invoice Sync Now" — enqueue the status sync + finalizer to run in the background
        // (paced by LhdnRateLimitHandler) so the request returns immediately and never times out.
        public async Task<IActionResult> OnPostRunAsync()
        {
            string userName = User?.Identity?.Name ?? "System";

            var jobId = await _jobTracker.CreateAsync("admin-sync", SyncJobType.StatusSync, userName);

            await _taskQueue.EnqueueAsync("admin-sync", async token =>
            {
                using var scope = _scopeFactory.CreateScope();
                var tracker = scope.ServiceProvider.GetRequiredService<ISyncJobTracker>();
                await tracker.MarkRunningAsync(jobId);
                try
                {
                    var syncHelper = scope.ServiceProvider.GetRequiredService<InvoiceSyncHelper>();
                    var updateResult = await syncHelper.RunInvoiceUpdateAsync(userName);
                    var finalizeResult = await syncHelper.RunFinalizerAsync(userName);
                    await tracker.MarkCompletedAsync(jobId, $"{updateResult} | {finalizeResult}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[StatusSync] background job {JobId} failed", jobId);
                    await tracker.MarkFailedAsync(jobId, ex.Message);
                }
            });

            TempData["Message"] = "✅ Invoice sync started in the background. Watch progress on the Sync Jobs page.";
            return RedirectToPage();
        }

        // "Import All Invoices from LHDN" — enqueue one paced background job per company TIN.
        public async Task<IActionResult> OnPostFullImportAllAsync()
        {
            // Get all TINs linked to the current user (could be system or intermediary)
            var allUserTins = User.GetUserCompanies(_dbContext).Select(x => x.TIN).ToList();

            // Filter out General TINs as they cannot be used for LHDN token requests
            var userTins = allUserTins.Where(tin => !GeneralTINHelper.IsGeneralTIN(tin)).ToList();

            if (!userTins.Any())
            {
                var generalTinCount = allUserTins.Count - userTins.Count;
                TempData["Message"] = generalTinCount > 0
                    ? $"❌ No valid companies for LHDN import. Found {generalTinCount} General TIN(s) which cannot be used for import."
                    : "❌ No companies linked to your account.";
                return RedirectToPage();
            }

            string userName = User?.Identity?.Name ?? "System";

            // (a) Admin full import uses the configured LHDN retention window (default 60 days).
            int lookbackDays = _configuration.GetValue<int?>("LHDNApiConfig:SyncRetentionDays") ?? 60;

            foreach (var tin in userTins)
            {
                var capturedTin = tin; // avoid closure capture of the loop variable
                var jobId = await _jobTracker.CreateAsync(capturedTin, SyncJobType.FullImport, userName);

                await _taskQueue.EnqueueAsync(capturedTin, async token =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var tracker = scope.ServiceProvider.GetRequiredService<ISyncJobTracker>();
                    await tracker.MarkRunningAsync(jobId);
                    try
                    {
                        var syncHelper = scope.ServiceProvider.GetRequiredService<InvoiceSyncHelper>();
                        var result = await syncHelper.RunFullImportFromLhdnAsync(capturedTin, userName, lookbackDays);
                        _logger.LogInformation("[ImportAll] TIN {Tin}: {Result}", capturedTin, result);
                        await tracker.MarkCompletedAsync(jobId, result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ImportAll] TIN {Tin} job {JobId} failed", capturedTin, jobId);
                        await tracker.MarkFailedAsync(jobId, ex.Message);
                    }
                });
            }

            var skippedGeneralTins = allUserTins.Where(GeneralTINHelper.IsGeneralTIN).ToList();
            var msg = $"✅ LHDN full import queued for {userTins.Count} company(ies) (last {lookbackDays} days); it runs in the background (rate-limited). Watch progress on the Sync Jobs page.";
            if (skippedGeneralTins.Any())
                msg += $" ⚠️ Skipped {skippedGeneralTins.Count} General TIN(s): {string.Join(", ", skippedGeneralTins)}.";
            TempData["Message"] = msg;
            return RedirectToPage();
        }
    }
}
