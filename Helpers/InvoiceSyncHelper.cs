using eInvWorld.Data;
using eInvWorld.Helpers;
using EINVWORLD.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using eInvWorld.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;


namespace EINVWORLD.Helpers

{
    public class InvoiceSyncHelper
    {
        private readonly ApplicationDbContext _context;
        private readonly ILHDNApiService _lhdnService;
        private readonly IPdfGeneratorService _pdfService;
        private readonly IEInvoiceNotificationService _notificationService;
        private readonly ITokenService _tokenService;
        private readonly InvoiceStatusSyncHelper _statusSyncHelper;
        private readonly ILogger<InvoiceSyncHelper> _logger;
        private readonly FilePathConfig _filePathConfig;
        private readonly InvoiceFullSyncHelper _fullSyncHelper;
        private readonly IInvoiceFinalizer _invoiceFinalizer;



        public InvoiceSyncHelper(
            ApplicationDbContext context,
            ILHDNApiService lhdnService,
            IPdfGeneratorService pdfService,
            IEInvoiceNotificationService notificationService,
            ITokenService tokenService,
            InvoiceStatusSyncHelper statusSyncHelper,
            InvoiceFullSyncHelper fullSyncHelper,
            ILogger<InvoiceSyncHelper> logger,
            IOptions<FilePathConfig> filePathConfig,
            IInvoiceFinalizer invoiceFinalizer)
        {
            _context = context;
            _lhdnService = lhdnService;
            _pdfService = pdfService;
            _notificationService = notificationService;
            _tokenService = tokenService;
            _statusSyncHelper = statusSyncHelper;
            _fullSyncHelper = fullSyncHelper;
            _logger = logger;
            _filePathConfig = filePathConfig.Value;
            _invoiceFinalizer = invoiceFinalizer;
        }

        // Manual trigger for LHDN sync (status update)
        public async Task<string> RunInvoiceUpdateAsync(string? triggeredBy = "System")
        {
            int processed = 0, skipped = 0, errored = 0;
            _logger.LogInformation("[SYNC] Starting LHDN invoice status sync job...");

            // 1. Define the 72-hour cutoff time
            var cutoffTime = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow).AddHours(-72);

            // 2. Add the cutoff filter to the query
            var invoices = _context.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer)
                .Where(i => i.InternalStatusId != "Draft")
                // ONLY rely on CreatedDate to prevent the infinite loop trap
                .Where(i => i.CreatedDate >= cutoffTime)
                .ToList();

            foreach (var invoice in invoices)
            {
                // Skip draft or invoices with no SubmissionID or UUID
                if (string.IsNullOrWhiteSpace(invoice.SubmissionID) && string.IsNullOrWhiteSpace(invoice.UUID))
                {
                    _logger.LogInformation("[SYNC] Skipping invoice {InvoiceNo}: No SubmissionID/UUID (Status: {Status})", invoice.InvoiceNo, invoice.LHDNStatusId);
                    skipped++;
                    continue;
                }

                string context = $"InvoiceNo: {invoice.InvoiceNo}, SubmissionID: {invoice.SubmissionID}, UUID: {invoice.UUID}, LHDNStatus: {invoice.LHDNStatusId}, InternalStatus: {invoice.InternalStatusId}";

                try
                {
                    // Decide TIN based on document type (self-billed → Customer, else Supplier)
                    string? tin = TinHelper.ResolveSubmitterTin(invoice);
                    if (string.IsNullOrWhiteSpace(tin))
                    {
                        _logger.LogWarning("[SYNC] {Context} - Missing TIN, skipping.", context);
                        skipped++;
                        continue;
                    }

                    string accessToken = await _tokenService.GetAccessTokenForTIN(tin);
                    _logger.LogDebug("[SYNC] {Context} - Using TIN: {TIN}", context, LogSanitizer.MaskTin(tin));

                    // If UUID is missing but SubmissionID exists, poll SubmissionStatus to get UUID
                    if (!string.IsNullOrWhiteSpace(invoice.SubmissionID) && string.IsNullOrWhiteSpace(invoice.UUID))
                    {
                        _logger.LogInformation("[SYNC] {Context} - Polling SubmissionStatus to get UUID...", context);

                        try
                        {
                            var submissionSummary = await _lhdnService.PollSubmissionStatusAsync(invoice.SubmissionID, accessToken);
                            if (!string.IsNullOrWhiteSpace(submissionSummary?.uuid))
                            {
                                invoice.UUID = submissionSummary.uuid;
                                _context.InvoiceHeaders.Update(invoice);
                                await _context.SaveChangesAsync();
                                processed++;
                                _logger.LogInformation("[SYNC] {Context} - UUID obtained: {UUID}", context, submissionSummary.uuid);
                            }
                            else
                            {
                                _logger.LogWarning("[SYNC] {Context} - SubmissionStatus polling did not return UUID.", context);
                                skipped++;
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[SYNC] {Context} - SubmissionStatus polling error.", context);
                            errored++;
                            continue;
                        }
                    }

                    // Always poll document details by UUID if available
                    if (!string.IsNullOrWhiteSpace(invoice.UUID))
                    {
                        if (InvoiceSyncRules.IsPermanentStatus(invoice.LHDNStatusId))
                        {
                            _logger.LogInformation("[SYNC] {Context} - Status is Permanent. Skipping DocumentDetails poll.", context);
                            skipped++;
                            continue;
                        }

                        if (!TryBeginDetailsPoll(invoice, triggeredBy))
                        {
                            _logger.LogDebug("⏳ Refresh skipped for {InvoiceNo} (cooldown, {TriggeredBy}).", invoice.InvoiceNo, triggeredBy);
                            continue;
                        }

                        _logger.LogDebug("[SYNC] {Context} - Polling DocumentDetails...", context);

                        try
                        {
                            var summary = await _lhdnService.GetDocumentDetailsAsync(invoice.UUID, accessToken);

                            if (summary != null && summary.status == "Valid" && string.IsNullOrWhiteSpace(summary.longId))
                            {
                                _logger.LogInformation("[SYNC] {Context} - Status is Valid but LongId missing. Waiting 15 seconds for a quick retry...", context);
                                await Task.Delay(15000); // Pauses the system for exactly 15 seconds
                                summary = await _lhdnService.GetDocumentDetailsAsync(invoice.UUID, accessToken); // Asks LHDN one more time
                            }

                            if (summary != null)
                            {
                                bool updated = await _statusSyncHelper.SyncInvoiceFromDocumentSummaryAsync(invoice.InvoiceNo, summary, triggeredBy ?? "System", saveImmediately: true);

                                if (!updated && invoice.LHDNStatusId == "Valid")
                                {
                                    invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                                    _context.InvoiceHeaders.Update(invoice);
                                    await _context.SaveChangesAsync();
                                }

                                processed++;
                                _logger.LogDebug("[SYNC] {Context} - DocumentDetails polled. DB updated: {Updated}", context, updated);
                            }
                            else
                            {
                                _logger.LogWarning("[SYNC] {Context} - DocumentDetails returned null.", context);
                                skipped++;
                            }
                        }
                        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.Message.Contains("404"))
                        {
                            // Benign: document not on LHDN yet (or stale UUID). Clean Warning, not an ERROR+stacktrace.
                            _logger.LogWarning("[SYNC] {Context} - document not found on LHDN (404); skipping.", context);
                            skipped++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[SYNC] {Context} - DocumentDetails polling error.", context);
                            errored++;

                            if (ex is HttpRequestException httpEx && (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests || httpEx.Message.Contains("429")))
                            {
                                _logger.LogWarning("🛑 Breaking manual sync loop due to LHDN 429 Rate Limit.");
                                break;
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SYNC] {Context} - Unexpected error in sync loop.", context);
                    errored++;
                }
                // Pacing is handled centrally by LhdnRateLimitHandler; no manual delay needed here.
            }

            _logger.LogInformation("[SYNC] LHDN invoice status sync job completed. Processed: {Processed}, Skipped: {Skipped}, Errors: {Errored}", processed, skipped, errored);

            return $"🧾 Invoices processed: <strong>{processed}</strong>, Skipped: <strong>{skipped}</strong>, Errors: <strong>{errored}</strong>";
        }




        // Manual trigger for PDF/email finalization. Per-invoice work (PDF, email, duplicate-send
        // guard) lives in the shared IInvoiceFinalizer; this only selects the recent candidates.
        public async Task<string> RunFinalizerAsync(string? triggeredBy = "System")
        {
            int pdfsGenerated = 0, emailsSent = 0;

            // 72-hour cutoff on CreatedDate so a manual run doesn't sweep the whole table.
            var cutoffTime = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow).AddHours(-72);

            var invoiceNos = await _context.InvoiceHeaders
                .AsNoTracking()
                .Where(i => i.LHDNStatusId == "Valid" && !string.IsNullOrWhiteSpace(i.LongId) && i.DateTimeValidated != null
                            && (!i.IsPdfGenerated || !i.IsValidationEmailSent))
                .Where(i => i.CreatedDate >= cutoffTime)
                .Select(i => i.InvoiceNo)
                .ToListAsync();

            foreach (var invoiceNo in invoiceNos)
            {
                var result = await _invoiceFinalizer.FinalizeInvoiceAsync(invoiceNo, triggeredBy ?? "System");
                if (result.PdfGenerated) pdfsGenerated++;
                if (result.EmailSent) emailsSent++;
            }

            return $"📄 PDFs generated: <strong>{pdfsGenerated}</strong><br/>📧 Emails sent: <strong>{emailsSent}</strong>";
        }


        public async Task<bool> SyncLhdnInvoiceStatusAsync(
            InvoiceHeader invoice, // the invoice to update
            string updatedBy = "System",
            CancellationToken cancellationToken = default)
        {
            bool updated = false;

            // 1. Get correct token for TIN (self-billed → Customer, else Supplier)
            string? tin = TinHelper.ResolveSubmitterTin(invoice);
            if (string.IsNullOrEmpty(tin))
            {
                _logger.LogWarning($"[SYNC] Invoice {invoice.InvoiceNo} missing TIN. Skipping.");
                return false;
            }

            _logger.LogInformation("[SYNC] SyncLhdnInvoiceStatusAsync START for InvoiceNo: {InvoiceNo}, TIN: {TIN}", invoice.InvoiceNo, LogSanitizer.MaskTin(tin));
            _logger.LogDebug("[SYNC] SyncLhdnInvoiceStatusAsync - Using TIN: {TIN}", LogSanitizer.MaskTin(tin));
            _logger.LogDebug("[SYNC] SyncLhdnInvoiceStatusAsync - Invoice UUID: {UUID}, SubmissionID: {SubmissionID}", invoice.UUID, invoice.SubmissionID);
            
            // Get access token for the TIN
            var accessToken = await _tokenService.GetAccessTokenForTIN(tin);

            // 2. If we have SubmissionID but NOT UUID, poll submission endpoint to get UUID
            if (!string.IsNullOrEmpty(invoice.SubmissionID) && string.IsNullOrEmpty(invoice.UUID))
            {
                try
                {
                    _logger.LogInformation("[SYNC] Polling SubmissionStatus for InvoiceNo: {InvoiceNo} (SubmissionID: {SubmissionID})", invoice.InvoiceNo, invoice.SubmissionID);

                    var submissionSummary = await _lhdnService.PollSubmissionStatusAsync(invoice.SubmissionID, accessToken);
                    if (submissionSummary?.uuid != null)
                    {
                        invoice.UUID = submissionSummary.uuid;
                        _context.InvoiceHeaders.Update(invoice);
                        await _context.SaveChangesAsync();
                        updated = true;
                        _logger.LogInformation("[SYNC] Updated UUID for InvoiceNo: {InvoiceNo} to {UUID}", invoice.InvoiceNo, submissionSummary.uuid);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[LHDN Sync] Submission polling error for {invoice.InvoiceNo}");
                }
            }

            // 3. If we have UUID, always poll details and map all fields
            if (!string.IsNullOrEmpty(invoice.UUID))
            {
                if (InvoiceSyncRules.IsPermanentStatus(invoice.LHDNStatusId))
                {
                    _logger.LogInformation("[SYNC] InvoiceNo: {InvoiceNo} status is permanent ({Status}). Skipping API call.", invoice.InvoiceNo, invoice.LHDNStatusId);
                    return updated; // Exit early
                }

                if (!TryBeginDetailsPoll(invoice, updatedBy))
                {
                    _logger.LogDebug("⏳ Refresh skipped for {InvoiceNo} (cooldown, {TriggeredBy}).", invoice.InvoiceNo, updatedBy);
                    return updated;
                }

                try
                {
                    _logger.LogInformation("[SYNC] Polling DocumentDetails for InvoiceNo: {InvoiceNo}, UUID: {UUID}", invoice.InvoiceNo, invoice.UUID);

                    var summary = await _lhdnService.GetDocumentDetailsAsync(invoice.UUID, accessToken);

                    if (summary != null && summary.status == "Valid" && string.IsNullOrWhiteSpace(summary.longId))
                    {
                        _logger.LogInformation("⏳ Invoice {InvoiceNo} is Valid but LongId missing. Waiting 15 seconds for a quick retry...", invoice.InvoiceNo);
                        await Task.Delay(15000, cancellationToken); // cancellable: don't hold an abandoned request open
                        summary = await _lhdnService.GetDocumentDetailsAsync(invoice.UUID, accessToken); // Ask one more time
                    }

                    if (summary != null)
                    {
                        // Update all possible fields (use your improved mapping method!)
                        bool mapped = await _statusSyncHelper.SyncInvoiceFromDocumentSummaryAsync(invoice.InvoiceNo, summary, updatedBy, saveImmediately: true);
                        updated = updated || mapped;
                        _logger.LogInformation("[SYNC] SyncInvoiceFromDocumentSummaryAsync for InvoiceNo: {InvoiceNo} => Updated: {Updated}", invoice.InvoiceNo, mapped);
                    }
                    else
                    {
                        _logger.LogWarning("[SYNC] GetDocumentDetailsAsync returned null for InvoiceNo: {InvoiceNo}, UUID: {UUID}", invoice.InvoiceNo, invoice.UUID);
                    }
                }
                catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.Message.Contains("404"))
                {
                    // Benign: the document isn't on LHDN (not yet submitted, or a stale/placeholder UUID).
                    // Log clean at Warning — an ERROR + stack trace here fired every sync cycle and buried the log.
                    _logger.LogWarning("[LHDN Sync] Document not found on LHDN (404) for {InvoiceNo}; skipping this cycle.", invoice.InvoiceNo);
                }
                catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests || ex.Message.Contains("429"))
                {
                    // Rate limit persisted through all retries. Log clean (an ERROR + stack trace per invoice
                    // was burying the log) and rethrow so callers looping over invoices abort the pass instead
                    // of hammering an API that just rate-limited us. The next cycle retries naturally.
                    _logger.LogWarning("[LHDN Sync] LHDN rate limit (429) persisted while polling {InvoiceNo}; aborting this sync pass.", invoice.InvoiceNo);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[LHDN Sync] Details polling error for {invoice.InvoiceNo}");
                }
            }
            _logger.LogInformation("[SYNC] SyncLhdnInvoiceStatusAsync END for InvoiceNo: {InvoiceNo}, Updated: {Updated}", invoice.InvoiceNo, updated);

            return updated;
        }

        /// <summary>
        /// Cooldown gate for a DocumentDetails poll. Returns <c>false</c> (skip) when the per-caller
        /// cooldown for a Valid document has not elapsed; otherwise records the poll attempt — BEFORE
        /// the actual LHDN call, so a failed (e.g. 429) attempt also starts the cooldown — and returns
        /// <c>true</c>. <c>LastUpdated</c> only advances when LHDN data changed, so the in-memory
        /// last-attempt time is folded in; without it, long-unchanged invoices are re-polled on every
        /// pass and trip the LHDN 429 limit. Only "BackgroundService"/"UISession" triggers have a
        /// cooldown (see <see cref="InvoiceSyncRules.ShouldSkipValidRefresh"/>); manual/job triggers
        /// always poll, but their attempts still feed the other triggers' cooldowns.
        /// </summary>
        private static bool TryBeginDetailsPoll(InvoiceHeader invoice, string? triggeredBy)
        {
            var lastChecked = InvoicePollAttemptTracker.GetEffectiveLastChecked(
                invoice.InvoiceNo, invoice.LastUpdated ?? invoice.CreatedDate);
            var nowMyt = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);

            if (InvoiceSyncRules.ShouldSkipValidRefresh(
                    invoice.LHDNStatusId, !string.IsNullOrWhiteSpace(invoice.LongId), lastChecked, nowMyt, triggeredBy))
            {
                return false;
            }

            InvoicePollAttemptTracker.RecordAttempt(invoice.InvoiceNo, nowMyt);
            return true;
        }

        // Statuses LHDN will never move an invoice out of once reached.
        private static readonly HashSet<string> TerminalLhdnStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Rejected", "Cancelled", "Invalid"
        };

        /// <summary>
        /// True once an invoice can no longer change status: fully terminal (Rejected/Cancelled/
        /// Invalid), or Valid with its QR (<see cref="DocumentSummary.longId"/>) already captured and
        /// more than 72 hours past validation — LHDN only allows cancel (supplier) / reject (buyer)
        /// within 72 hours of validation, so a Valid invoice past that window can never change again.
        /// </summary>
        internal static bool IsStable(DocumentSummary summary)
        {
            if (TerminalLhdnStatuses.Contains(summary.status ?? string.Empty)) return true;

            return string.Equals(summary.status, "Valid", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(summary.longId)
                && summary.dateTimeValidated.HasValue
                && summary.dateTimeValidated.Value < DateTime.UtcNow.AddHours(-72);
        }

        /// <summary>
        /// True when the local copy already matches what LHDN's search result says: same status, and for a Valid
        /// invoice the QR (LongId) is already stored. Inside the 72h window a cancel or reject changes the status in the
        /// search result itself, so the expensive document fetch is only needed when the status differs.
        /// </summary>
        internal static bool IsUnchangedLocally(DocumentSummary summary, string? localStatus, bool localHasLongId) =>
            !string.IsNullOrWhiteSpace(summary.status)
            && string.Equals(summary.status, localStatus, StringComparison.OrdinalIgnoreCase)
            && (!string.Equals(summary.status, "Valid", StringComparison.OrdinalIgnoreCase) || localHasLongId);

        /// <summary>Default cap on raw-document fetches per import run (see <c>InvoiceStatusUpdaterSettings.ImportMaxDocumentFetchesPerRun</c>).</summary>
        public const int DefaultMaxDocumentFetchesPerRun = 40;

        /// <summary>
        /// Imports LHDN documents (sent and received) for one company TIN over the lookback window.
        /// At most <paramref name="maxDocumentFetches"/> raw documents are fetched per run, newest first, so a
        /// large backlog never holds LHDN's shared get-document queue for long (interactive status checks
        /// share it). When documents are left over and this run stored at least one new document, a follow-up job of
        /// <paramref name="continuationJobType"/> is queued a couple of minutes later to fetch the rest.
        /// </summary>
        public async Task<string> RunFullImportFromLhdnAsync(string tin, string? triggeredBy = "System", int lookbackDays = 3,
            int maxDocumentFetches = DefaultMaxDocumentFetchesPerRun, string continuationJobType = eInvWorld.Models.Background.SyncJobType.SupplierRefresh)
        {
            if (maxDocumentFetches < 1) maxDocumentFetches = DefaultMaxDocumentFetchesPerRun;
            var accessToken = await _tokenService.GetAccessTokenForTIN(tin);
            var allSummaries = await GetAllUuidsForSubmitterAsync(tin, accessToken, lookbackDays);

            if (allSummaries == null || !allSummaries.Any())
                return $"No documents found for TIN: {LogSanitizer.MaskTin(tin)}";

            // Only invoices we already have locally can be safely skipped — an unseen UUID always needs
            // the raw-document fetch, since /documents/search never carries line-item content.
            var uuidsInBatch = allSummaries.Select(s => s.uuid).ToList();
            var existing = (await _context.InvoiceHeaders
                .Where(i => uuidsInBatch.Contains(i.UUID!))
                .Select(i => new
                {
                    i.UUID, i.LHDNStatusId, i.LongId,
                    // Needs repair: no supplier, no buyer, or a buyer PublicCustomer owned by no company
                    // (left by the pre-fix direction bug on received invoices).
                    NeedsRelink = i.SupplierId == null
                        || (i.CustomerId == null && i.PublicCustomerId == null)
                        || (i.CustomerId == null && i.PublicCustomer != null && (i.PublicCustomer.CreatedByCompanyId ?? 0) == 0)
                })
                .ToListAsync())
                .GroupBy(i => i.UUID!)
                .ToDictionary(g => g.Key, g => g.First());

            // A local row missing its supplier or buyer link (e.g. a past failed sync) is never skipped,
            // so the re-fetch can repair it — otherwise it stays invisible in every invoice tab. Otherwise an
            // invoice already synced that can no longer change status (per LHDN's own status/longId/
            // dateTimeValidated from this same search response) skips the raw-document fetch — the
            // endpoint that hits LHDN's strict rate limit.
            var (toFetch, skipped, deferred) = PlanDocumentFetches(allSummaries, maxDocumentFetches, summary =>
                existing.TryGetValue(summary.uuid, out var local)
                && !local.NeedsRelink
                && (IsStable(summary) || IsUnchangedLocally(summary, local.LHDNStatusId, !string.IsNullOrWhiteSpace(local.LongId))),
                isNew: summary => !existing.ContainsKey(summary.uuid));

            int imported = 0;
            int newImported = 0;
            int errors = 0;

            // REMOVED Parallel.ForEachAsync to prevent 429 DDoS penalties from LHDN
            foreach (var summary in toFetch)
            {
                try
                {
                    // Pacing is handled centrally by LhdnRateLimitHandler (even-paced per endpoint).
                    var docSummary = await _lhdnService.GetDocumentDetailsAsync(summary.uuid, accessToken);
                    if (docSummary != null && await _fullSyncHelper.SyncAllFromApiAsync(docSummary, tin))
                    {
                        imported++;
                        if (!existing.ContainsKey(summary.uuid)) newImported++;
                    }
                    else
                        errors++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync UUID: {Uuid}", summary.uuid);
                    errors++;
                }
            }

            var result = $"Imported {imported} documents, Skipped (already stable): {skipped}, Errors: {errors}";
            if (deferred > 0)
            {
                // Continue only when this run stored a document we didn't have: each follow-up then adds at least one
                // row, so the chain always ends. Re-fetching local rows (status refresh, or a supplier/buyer repair
                // that may never succeed) is not progress — it would re-queue forever on the same rows.
                bool queued = ShouldQueueContinuation(deferred, newImported) && await QueueImportContinuationAsync(tin, lookbackDays, continuationJobType);
                result += queued
                    ? $", Deferred: {deferred} (continuing in a follow-up job)"
                    : $", Deferred: {deferred} (picked up by the next refresh)";
            }
            return result;
        }

        /// <summary>
        /// Splits a search result into the documents to fetch now (at most <paramref name="max"/>), the number skipped
        /// (already up to date locally) and the number deferred to a later run. Documents not yet stored locally
        /// (<paramref name="isNew"/>) come first, then refreshes/repairs of local rows; newest first within each group,
        /// so rows that can never be repaired cannot take the whole budget and starve new documents.
        /// </summary>
        internal static (List<DocumentSummary> ToFetch, int Skipped, int Deferred) PlanDocumentFetches(
            IEnumerable<DocumentSummary> summaries, int max, Func<DocumentSummary, bool> canSkip,
            Func<DocumentSummary, bool>? isNew = null)
        {
            var needed = summaries
                .Where(s => !canSkip(s))
                .OrderByDescending(s => isNew?.Invoke(s) ?? false)
                .ThenByDescending(s => s.dateTimeReceived ?? s.dateTimeIssued)
                .ToList();
            int skipped = summaries.Count() - needed.Count;
            var toFetch = needed.Take(Math.Max(1, max)).ToList();
            return (toFetch, skipped, needed.Count - toFetch.Count);
        }

        /// <summary>
        /// A follow-up job is queued only when documents are left over and this run stored at least one document we
        /// didn't have, so every follow-up makes progress and the chain always ends.
        /// </summary>
        internal static bool ShouldQueueContinuation(int deferred, int newImported) => deferred > 0 && newImported > 0;

        /// <summary>Delay before a continuation job runs, leaving LHDN's get-document queue free for interactive work.</summary>
        private static readonly TimeSpan ImportContinuationDelay = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Queues a follow-up import for <paramref name="tin"/> unless one is already queued. Already-imported
        /// documents are skipped by the next run without a fetch, so each continuation fetches only what's left.
        /// </summary>
        private async Task<bool> QueueImportContinuationAsync(string tin, int lookbackDays, string jobType)
        {
            try
            {
                bool alreadyQueued = await _context.SyncJobs.AnyAsync(j =>
                    j.Tin == tin && j.Status == eInvWorld.Models.Background.SyncJobStatus.Queued &&
                    (j.JobType == eInvWorld.Models.Background.SyncJobType.SupplierRefresh ||
                     j.JobType == eInvWorld.Models.Background.SyncJobType.FullImport));
                if (alreadyQueued) return true;

                _context.SyncJobs.Add(new eInvWorld.Models.Background.SyncJob
                {
                    Tin = tin,
                    JobType = jobType,
                    Status = eInvWorld.Models.Background.SyncJobStatus.Queued,
                    QueuedAtUtc = DateTime.UtcNow,
                    NextRunAtUtc = DateTime.UtcNow.Add(ImportContinuationDelay),
                    MaxAttempts = 3,
                    TriggeredBy = "System (import continuation)",
                    PayloadJson = EINVWORLD.Services.Background.SyncJobPayload.Create(lookbackDays),
                });
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Never fail the import over this; the next scheduled or manual refresh still picks the rest up.
                _logger.LogWarning(ex, "Could not queue an import continuation for TIN {TIN}.", LogSanitizer.MaskTin(tin));
                return false;
            }
        }
        /// <summary>
        /// Gets all documents submitted by the specified TIN (both regular and self-billed)
        /// </summary>
        private async Task<List<DocumentSummary>?> GetAllUuidsForSubmitterAsync(string tin, string accessToken, int lookbackDays = 3)
        {
            // Use the existing method that searches by date range and gets all submitted documents
            // This will include both regular invoices (where tin is issuer) and self-billed invoices (where tin is submitter but receiver)
            return await _lhdnService.GetAllUuidsForTinAsync(tin, accessToken, lookbackDays);
        }


    }
}
