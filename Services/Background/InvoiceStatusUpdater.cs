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
                await RunFinalizerAsync(dbContext, pdfService, notificationService);

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

    private async Task RunFinalizerAsync(ApplicationDbContext dbContext, IPdfGeneratorService pdfService, IEInvoiceNotificationService notificationService)
    {
        var invoices = dbContext.InvoiceHeaders
            .Include(i => i.Customer)
            .Include(i => i.Supplier)
            .Where(i => i.LHDNStatusId == "Valid" && !string.IsNullOrWhiteSpace(i.LongId) && i.DateTimeValidated != null
                        && (!i.IsPdfGenerated || !i.IsValidationEmailSent))
            .ToList();

        foreach (var invoice in invoices)
        {
            bool changesMade = false;
            string pdfPath = Path.Combine(_filePathConfig.GeneratedPdfFolder, $"{invoice.InvoiceNo}.pdf");

            // 1. ONLY generate PDF if it is missing (Prevents background crash)
            if (!invoice.IsPdfGenerated || !File.Exists(pdfPath))
            {
                try
                {
                    await pdfService.GeneratePdfAsync(invoice.InvoiceNo);
                    invoice.IsPdfGenerated = true;
                    invoice.PdfGeneratedAt = DateTime.Now;
                    changesMade = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PDF generation failed for {InvoiceNo}", invoice.InvoiceNo);
                }
            }

            // 2. Send email ONLY if PDF exists and hasn't been sent
            if (!invoice.IsValidationEmailSent && File.Exists(pdfPath))
            {
                try
                {
                    // Convert dates to Malaysia time for email display
                    var issueDate = invoice.IssueDate.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(invoice.IssueDate.Value, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"))
                        : DateTime.Now;
                    var validatedDate = invoice.DateTimeValidated.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(invoice.DateTimeValidated.Value, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"))
                        : DateTime.Now;

                    // Send the email with the freshly generated PDF attached
                    await notificationService.SendValidatedNotificationEmail(
                        invoice.Customer?.CompanyName ?? "Customer",
                        invoice.Customer,
                        invoice.Supplier,
                        invoice.InvoiceNo,
                        issueDate,
                        validatedDate);

                    invoice.IsValidationEmailSent = true;
                    invoice.ValidationEmailSentAt = DateTime.Now;
                    invoice.ValidationEmailSentTo = string.Join(", ", new[] { invoice.Customer?.Email, invoice.Supplier?.Email }.Where(e => !string.IsNullOrWhiteSpace(e)));
                    changesMade = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Email send failed for {InvoiceNo}", invoice.InvoiceNo);
                }
            }

            if (changesMade)
                await dbContext.SaveChangesAsync();
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

            try
            {
                string? tin = TinHelper.ResolveSubmitterTin(invoice);

                // 1. Skip ONLY if TIN is missing
                if (string.IsNullOrWhiteSpace(tin) || GeneralTINHelper.IsGeneralTIN(tin))
                {
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
                            await dbContext.SaveChangesAsync();
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
                    _logger.LogInformation("Batch aborted: API unavailable for {InvoiceNo}", invoice.InvoiceNo);
                    continue;
                }

                await syncHelper.SyncInvoiceFromDocumentSummaryAsync(invoice.InvoiceNo, summary, "BackgroundService", saveImmediately: true);

                invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing invoice {InvoiceNo}", invoice.InvoiceNo);
                invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
                await dbContext.SaveChangesAsync();
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
                tokenService, statusSyncHelper, fullSyncHelper, syncLogger, filePathConfig);

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