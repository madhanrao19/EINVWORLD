using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Services;
using eInvWorld.Models.Settings;
using eInvWorld.Models.Audit;
using eInvWorld.Models;
using Microsoft.Extensions.Options;

public class InvoiceFinalizerService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InvoiceFinalizerService> _logger;
    private readonly FilePathConfig _filePathConfig;
    private readonly int _intervalSeconds;

    public InvoiceFinalizerService(IServiceScopeFactory serviceScopeFactory, ILogger<InvoiceFinalizerService> logger, IOptions<FilePathConfig> filePathOptions, int intervalSeconds = 300)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _filePathConfig = filePathOptions.Value;
        _intervalSeconds = intervalSeconds;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Invoice Finalizer Service started...");
        _logger.LogWarning("InvoiceFinalizerService heartbeat at {Time}", DateTime.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var finalizer = scope.ServiceProvider.GetRequiredService<IInvoiceFinalizer>();

                await FinalizeInvoicesAsync(dbContext, finalizer, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InvoiceFinalizerService encountered an error");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }

    // Safety-net sweep: finalizes validated invoices the interactive flow and status poller missed.
    // The 10-minute grace period leaves fresh validations to those faster paths; the per-invoice
    // work (PDF, email, duplicate-send guard) lives in the shared IInvoiceFinalizer.
    private async Task FinalizeInvoicesAsync(
        ApplicationDbContext dbContext,
        IInvoiceFinalizer finalizer,
        CancellationToken stoppingToken)
    {
        var graceCutoff = DateTime.Now.AddMinutes(-10);

        var invoiceNos = await dbContext.InvoiceHeaders
            .AsNoTracking()
            .Where(i =>
                i.LHDNStatusId == "Valid" &&
                !string.IsNullOrWhiteSpace(i.LongId) &&
                i.DateTimeValidated != null &&
                i.DateTimeValidated <= graceCutoff &&
                (!i.IsPdfGenerated || !i.IsValidationEmailSent))
            .Select(i => i.InvoiceNo)
            .ToListAsync(stoppingToken);

        foreach (var invoiceNo in invoiceNos)
        {
            if (stoppingToken.IsCancellationRequested) break;
            await finalizer.FinalizeInvoiceAsync(invoiceNo, "Finalizer", stoppingToken);
        }
    }
}
