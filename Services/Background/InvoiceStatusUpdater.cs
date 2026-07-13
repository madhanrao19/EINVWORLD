using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.Settings;
using eInvWorld.Models.Audit;
using eInvWorld.Services;
using eInvWorld.Helpers;
using EINVWORLD.Helpers;

public class InvoiceStatusUpdater : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InvoiceStatusUpdater> _logger;
    private readonly FilePathConfig _filePathConfig;
    private readonly InvoiceStatusUpdaterSettings _settings;
    private int _cycleCount = 0;

    public InvoiceStatusUpdater(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<InvoiceStatusUpdater> logger,
        IOptions<FilePathConfig> filePathConfig,
        IOptions<InvoiceStatusUpdaterSettings> settings)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _filePathConfig = filePathConfig.Value;
        _settings = settings.Value;
    }

    /// <summary>
    /// Saves pending changes, treating an optimistic-concurrency conflict (InvoiceHeader.RowVersion) as a
    /// benign skip: another writer (user cancel/edit or a parallel sync) updated the row first. Local
    /// changes are discarded by reloading the conflicting entries so the scoped context stays usable, and
    /// the next poll cycle re-syncs the invoice from LHDN — the source of truth for status fields.
    /// </summary>
    private async Task<bool> TrySaveAsync(ApplicationDbContext dbContext, string invoiceNo)
    {
        try
        {
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                "Concurrency conflict saving invoice {InvoiceNo}: another writer updated it first. Skipping; the next poll re-syncs from LHDN.",
                invoiceNo);
            foreach (var entry in ex.Entries)
            {
                await entry.ReloadAsync();
            }
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Invoice Status Updater is disabled in configuration. Service will not run.");
            return;
        }

        _logger.LogInformation("Invoice Status Updater is running...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var lhdnApiService = scope.ServiceProvider.GetRequiredService<LHDNApiService>();
                var pdfService = scope.ServiceProvider.GetRequiredService<PDFGeneratorService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<EInvoiceNotificationService>();
                var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
                var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

                var syncLogger = loggerFactory.CreateLogger<InvoiceStatusSyncHelper>();
                var syncHelper = new InvoiceStatusSyncHelper(dbContext, syncLogger);

                await UpdateInvoiceStatuses(dbContext, lhdnApiService, tokenService, pdfService, notificationService, syncHelper, stoppingToken);

                var finalizer = scope.ServiceProvider.GetRequiredService<IInvoiceFinalizer>();
                await RunFinalizerAsync(dbContext, finalizer, stoppingToken);

                // Enqueue outbound webhooks for invoices that reached a terminal LHDN status (no-op unless
                // Webhooks:Enabled and at least one enabled subscription exists).
                var webhookDispatch = scope.ServiceProvider.GetRequiredService<eInvWorld.Services.Webhooks.IWebhookDispatchService>();
                await webhookDispatch.ScanAndEnqueueAsync(dbContext, stoppingToken);

                // Run LHDN import every 10 cycles to avoid overloading the API
                _cycleCount++;
                if (_cycleCount % 10 == 0)
                {
                    await RunLhdnImportAsync(scope, dbContext, tokenService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background invoice sync loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.PollingIntervalSeconds), stoppingToken);
        }
    }

    // PDF/email finalization for validated invoices. The per-invoice work lives in the shared
    // IInvoiceFinalizer (also called inline by the submit flow); this loop is the safety net for
    // invoices that validated while no interactive request was around to finalize them.
    private async Task RunFinalizerAsync(ApplicationDbContext dbContext, IInvoiceFinalizer finalizer, CancellationToken stoppingToken)
    {
        var invoiceNos = await dbContext.InvoiceHeaders
            .AsNoTracking()
            .Where(i => i.LHDNStatusId == "Valid" && !string.IsNullOrWhiteSpace(i.LongId) && i.DateTimeValidated != null
                        && (!i.IsPdfGenerated || !i.IsValidationEmailSent))
            .Select(i => i.InvoiceNo)
            .ToListAsync(stoppingToken);

        foreach (var invoiceNo in invoiceNos)
        {
            if (stoppingToken.IsCancellationRequested) break;
            await finalizer.FinalizeInvoiceAsync(invoiceNo, "BackgroundService", stoppingToken);
        }
    }

    private async Task UpdateInvoiceStatuses(
        ApplicationDbContext dbContext,
        ILHDNApiService lhdnApiService,
        ITokenService tokenService,
        IPdfGeneratorService pdfService,
        IEInvoiceNotificationService notificationService,
        InvoiceStatusSyncHelper syncHelper,
        CancellationToken stoppingToken)
    {
        var invoicesToProcess = await dbContext.InvoiceHeaders
            .Include(i => i.Customer).Include(i => i.Supplier)
            .Where(i =>
                // 1. Keep checking if it hasn't reached a final status yet
                (i.LHDNStatusId != "Valid" && i.LHDNStatusId != "Rejected" && i.LHDNStatusId != "Cancelled" && i.LHDNStatusId != "Invalid")
                ||
                // 2. OR keep checking if it is Valid but we haven't grabbed the LongId (QR code) yet
                (i.LHDNStatusId == "Valid" && string.IsNullOrWhiteSpace(i.LongId))
            )
            .OrderBy(i => i.LastUpdated ?? i.CreatedDate)
            .Take(10)
            .ToListAsync();

        foreach (var invoice in invoicesToProcess)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Correlate all logs for this invoice's sync attempt under one id.
            using var _corr = Serilog.Context.LogContext.PushProperty("CorrelationId", $"statussync-{invoice.InvoiceNo}");
            try
            {
                string? tin = TinHelper.ResolveSubmitterTin(invoice);

                // 1. Skip ONLY if TIN is missing or general — but log it, so an invoice that silently
                //    stops syncing because its TIN can't be resolved is visible to operators.
                if (string.IsNullOrWhiteSpace(tin) || GeneralTINHelper.IsGeneralTIN(tin))
                {
                    _logger.LogWarning("Skipping status sync for invoice {InvoiceNo}: submitter TIN unresolved or general ({Tin}).",
                        invoice.InvoiceNo, string.IsNullOrWhiteSpace(tin) ? "none" : tin);
                    continue;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Throttling
                var accessToken = await tokenService.GetAccessTokenForTIN(tin);

                // 2. FIX: If UUID is missing but SubmissionID exists, poll LHDN to get the UUID
                if (!string.IsNullOrWhiteSpace(invoice.SubmissionID) && string.IsNullOrWhiteSpace(invoice.UUID))
                {
                    try
                    {
                        var submissionSummary = await lhdnApiService.PollSubmissionStatusAsync(invoice.SubmissionID, accessToken);
                        if (!string.IsNullOrWhiteSpace(submissionSummary?.uuid))
                        {
                            invoice.UUID = submissionSummary.uuid;
                            dbContext.InvoiceHeaders.Update(invoice);
                            if (!await TrySaveAsync(dbContext, invoice.InvoiceNo)) continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to poll submission status for {InvoiceNo}", invoice.InvoiceNo);
                    }
                }

                // 3. Now, if it STILL doesn't have a UUID after trying to fetch it, skip it
                if (string.IsNullOrWhiteSpace(invoice.UUID))
                {
                    continue;
                }

                // 4. Proceed normally with the UUID
                var summary = await lhdnApiService.GetDocumentDetailsWithRetryAsync(invoice.UUID, accessToken);

                if (summary == null)
                {
                    // Not available yet (e.g. LHDN hasn't indexed a just-submitted document — 404).
                    // Bump LastUpdated so this invoice moves to the back of the queue instead of
                    // permanently occupying a slot at the front of every batch and starving newer ones.
                    _logger.LogInformation("Document details not available yet for {InvoiceNo}; will retry next cycle.", invoice.InvoiceNo);
                    invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                    await TrySaveAsync(dbContext, invoice.InvoiceNo);
                    continue;
                }

                await syncHelper.SyncInvoiceFromDocumentSummaryAsync(invoice.InvoiceNo, summary, "BackgroundService", saveImmediately: true);

                invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                await TrySaveAsync(dbContext, invoice.InvoiceNo);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // App shutting down mid-batch — stop quietly, don't log it as a sync error.
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests || ex.Message.Contains("429"))
            {
                // GetDocumentDetailsWithRetryAsync re-throws 429 after exhausting its own penalty waits,
                // expecting the batch to stop. Honouring that here — instead of moving on to the next
                // invoice — avoids hammering an API that just rate-limited us; the whole queue retries
                // next cycle.
                _logger.LogWarning("LHDN rate limit (429) persisted while syncing {InvoiceNo}; aborting this poll cycle. Remaining invoices retry next cycle.", invoice.InvoiceNo);
                invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                await TrySaveAsync(dbContext, invoice.InvoiceNo);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing invoice {InvoiceNo}", invoice.InvoiceNo);
                invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                await TrySaveAsync(dbContext, invoice.InvoiceNo);
            }
        }
    }


    private async Task RunLhdnImportAsync(IServiceScope scope, ApplicationDbContext dbContext, ITokenService tokenService)
    {
        try
        {
            _logger.LogInformation("🔄 [LHDN Import] Starting scheduled LHDN API import...");

            var userCompanyTINs = await dbContext.UserCompanies
                .Include(uc => uc.PartyInfo)
                .Where(uc => uc.PartyInfo != null && !string.IsNullOrWhiteSpace(uc.PartyInfo.TIN))
                .Select(uc => uc.PartyInfo.TIN)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("🏢 [LHDN Import] Found {Count} user company TINs to import for", userCompanyTINs.Count);

            if (!userCompanyTINs.Any())
            {
                _logger.LogWarning("⚠️ [LHDN Import] No user company TINs found. Skipping import.");
                return;
            }

            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var syncLogger = loggerFactory.CreateLogger<InvoiceSyncHelper>();

            var lhdnApiService = scope.ServiceProvider.GetRequiredService<LHDNApiService>();
            var pdfService = scope.ServiceProvider.GetRequiredService<PDFGeneratorService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<EInvoiceNotificationService>();
            var statusSyncHelper = scope.ServiceProvider.GetRequiredService<InvoiceStatusSyncHelper>();
            var fullSyncHelper = scope.ServiceProvider.GetRequiredService<InvoiceFullSyncHelper>();
            var filePathConfig = scope.ServiceProvider.GetRequiredService<IOptions<FilePathConfig>>();

            var invoiceSyncHelper = new InvoiceSyncHelper(
                dbContext, lhdnApiService, pdfService, notificationService,
                tokenService, statusSyncHelper, fullSyncHelper, syncLogger, filePathConfig,
                scope.ServiceProvider.GetRequiredService<IInvoiceFinalizer>());

            foreach (var tin in userCompanyTINs)
            {
                try
                {
                    var result = await invoiceSyncHelper.RunFullImportFromLhdnAsync(tin, "BackgroundService");
                    _logger.LogInformation("✅ [LHDN Import] Result for TIN {TIN}: {Result}", tin, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [LHDN Import] Failed to import for TIN {TIN}: {Error}", tin, ex.Message);
                }
            }

            _logger.LogInformation("🎉 [LHDN Import] Completed scheduled LHDN API import for all user companies");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [LHDN Import] Critical error during LHDN import process: {Error}", ex.Message);
        }
    }
}