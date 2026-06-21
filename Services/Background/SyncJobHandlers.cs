using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models.Background;
using EINVWORLD.Helpers;

namespace EINVWORLD.Services.Background
{
    /// <summary>
    /// "Run Invoice Sync Now": refresh statuses from LHDN then run the finalizer.
    /// (Was the OnPostRunAsync closure in Pages/Admin/InvoiceSync.)
    /// </summary>
    public sealed class StatusSyncJobHandler : ISyncJobHandler
    {
        private readonly InvoiceSyncHelper _sync;
        public StatusSyncJobHandler(InvoiceSyncHelper sync) => _sync = sync;

        public string JobType => SyncJobType.StatusSync;

        public async Task<string> ExecuteAsync(SyncJob job, CancellationToken ct)
        {
            var user = string.IsNullOrWhiteSpace(job.TriggeredBy) ? "System" : job.TriggeredBy;
            var updateResult = await _sync.RunInvoiceUpdateAsync(user);
            var finalizeResult = await _sync.RunFinalizerAsync(user);
            return $"{updateResult} | {finalizeResult}";
        }
    }

    /// <summary>
    /// "Import All Invoices from LHDN": full import for one company TIN over the configured window.
    /// (Was the OnPostFullImportAllAsync closure in Pages/Admin/InvoiceSync.)
    /// </summary>
    public sealed class FullImportJobHandler : ISyncJobHandler
    {
        private readonly InvoiceSyncHelper _sync;
        public FullImportJobHandler(InvoiceSyncHelper sync) => _sync = sync;

        public string JobType => SyncJobType.FullImport;

        public Task<string> ExecuteAsync(SyncJob job, CancellationToken ct)
        {
            var user = string.IsNullOrWhiteSpace(job.TriggeredBy) ? "System" : job.TriggeredBy;
            var lookback = SyncJobPayload.LookbackOrDefault(job.PayloadJson, 60);
            return _sync.RunFullImportFromLhdnAsync(job.Tin, user, lookback);
        }
    }

    /// <summary>
    /// Supplier-initiated "Refresh from LHDN": same as a full import but a short default lookback.
    /// (Was the refresh closure in Pages/Invoices/InvoiceLists.)
    /// </summary>
    public sealed class SupplierRefreshJobHandler : ISyncJobHandler
    {
        private readonly InvoiceSyncHelper _sync;
        public SupplierRefreshJobHandler(InvoiceSyncHelper sync) => _sync = sync;

        public string JobType => SyncJobType.SupplierRefresh;

        public Task<string> ExecuteAsync(SyncJob job, CancellationToken ct)
        {
            var user = string.IsNullOrWhiteSpace(job.TriggeredBy) ? "System" : job.TriggeredBy;
            var lookback = SyncJobPayload.LookbackOrDefault(job.PayloadJson, 7);
            return _sync.RunFullImportFromLhdnAsync(job.Tin, user, lookback);
        }
    }
}
