using eInvWorld.Data;
using eInvWorld.Helpers;
using EINVWORLD.Helpers;
using eInvWorld.Models;
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

                        // For a Valid doc, honour the per-caller cooldown before re-polling.
                        var lastChecked = invoice.LastUpdated ?? invoice.CreatedDate;
                        var myTimeNow = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                        if (InvoiceSyncRules.ShouldSkipValidRefresh(invoice.LHDNStatusId, !string.IsNullOrWhiteSpace(invoice.LongId), lastChecked, myTimeNow, triggeredBy))
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
            string updatedBy = "System")
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

                // For a Valid doc, honour the per-caller cooldown before re-polling.
                var lastChecked = invoice.LastUpdated ?? invoice.CreatedDate;
                var myTimeNow = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                if (InvoiceSyncRules.ShouldSkipValidRefresh(invoice.LHDNStatusId, !string.IsNullOrWhiteSpace(invoice.LongId), lastChecked, myTimeNow, updatedBy))
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
                        await Task.Delay(15000); // Wait 15 seconds
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[LHDN Sync] Details polling error for {invoice.InvoiceNo}");
                }
            }
            _logger.LogInformation("[SYNC] SyncLhdnInvoiceStatusAsync END for InvoiceNo: {InvoiceNo}, Updated: {Updated}", invoice.InvoiceNo, updated);

            return updated;
        }

        public async Task<string> RunFullImportFromLhdnAsync(string tin, string? triggeredBy = "System", int lookbackDays = 3)
        {
            var accessToken = await _tokenService.GetAccessTokenForTIN(tin);
            var allUuids = await GetAllUuidsForSubmitterAsync(tin, accessToken, lookbackDays);

            if (allUuids == null || !allUuids.Any())
                return $"No documents found for TIN: {tin}";

            int imported = 0;
            int errors = 0;

            // REMOVED Parallel.ForEachAsync to prevent 429 DDoS penalties from LHDN
            foreach (var uuid in allUuids)
            {
                try
                {
                    // Pacing is handled centrally by LhdnRateLimitHandler (even-paced per endpoint).
                    var docSummary = await _lhdnService.GetDocumentDetailsAsync(uuid, accessToken);
                    if (docSummary != null)
                    {
                        await _fullSyncHelper.SyncAllFromApiAsync(docSummary);
                        imported++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync UUID: {Uuid}", uuid);
                    errors++;
                }
            }

            return $"Imported {imported} documents, Errors: {errors} (submitted by TIN)";
        }
        /// <summary>
        /// Gets all document UUIDs submitted by the specified TIN (both regular and self-billed)
        /// </summary>
        private async Task<List<string>?> GetAllUuidsForSubmitterAsync(string tin, string accessToken, int lookbackDays = 3)
        {
            // Use the existing method that searches by date range and gets all submitted documents
            // This will include both regular invoices (where tin is issuer) and self-billed invoices (where tin is submitter but receiver)
            return await _lhdnService.GetAllUuidsForTinAsync(tin, accessToken, lookbackDays);
        }


    }
}
