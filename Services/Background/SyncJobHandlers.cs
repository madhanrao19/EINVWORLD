using System;
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

    /// <summary>
    /// Background retry of an LHDN submission that failed inline (see <see cref="SyncJobType.SubmitDocument"/>).
    /// Reuses <see cref="InvoiceSubmissionHelper.SubmitInvoiceAsync"/>, a self-contained, invoice-number-only
    /// resubmission path (re-reads the invoice + draft JSON fresh from the DB/disk — no dependency on the
    /// original request's in-memory state, so it's safe to replay minutes or hours later). If the invoice is
    /// no longer in Draft status (e.g. the interactive request's own retry, or a prior queued attempt,
    /// already got it submitted), the helper safely no-ops with a "not in Draft status" message instead of
    /// double-submitting.
    /// </summary>
    public sealed class SubmitDocumentJobHandler : ISyncJobHandler
    {
        private readonly InvoiceSubmissionHelper _submission;
        public SubmitDocumentJobHandler(InvoiceSubmissionHelper submission) => _submission = submission;

        public string JobType => SyncJobType.SubmitDocument;

        public async Task<string> ExecuteAsync(SyncJob job, CancellationToken ct)
        {
            var invoiceNo = SyncJobPayload.InvoiceNoOrNull(job.PayloadJson);
            if (string.IsNullOrWhiteSpace(invoiceNo))
                throw new InvalidOperationException("SubmitDocument job is missing its InvoiceNo payload.");

            var user = string.IsNullOrWhiteSpace(job.TriggeredBy) ? "System" : job.TriggeredBy;
            var (success, message) = await _submission.SubmitInvoiceAsync(invoiceNo, user);

            // "Not in Draft status" means it's already submitted (by this same retry chain or the original
            // request) — a success from this job's point of view, not a failure to retry again.
            if (!success && !message.Contains("is not in Draft status", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(message);

            return message;
        }
    }
}
