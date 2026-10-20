using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.Recurring;
using eInvWorld.Models.Background;
using eInvWorld.Services.Extensions;
using eInvWorld.Services.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace eInvWorld.Services.Background
{
    public class RecurringInvoiceWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RecurringInvoiceWorker> _logger;

        public RecurringInvoiceWorker(IServiceScopeFactory scopeFactory, ILogger<RecurringInvoiceWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ Recurring Invoice Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueInvoicesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ A fatal error occurred during the recurring invoice generation cycle.");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task ProcessDueInvoicesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoiceService = scope.ServiceProvider.GetRequiredService<InvoiceService>();
            var filePathConfig = scope.ServiceProvider.GetRequiredService<IOptions<FilePathConfig>>().Value;
            var statusMappingService = scope.ServiceProvider.GetRequiredService<IStatusMappingService>();
            var signingService = scope.ServiceProvider.GetRequiredService<eInvWorld.Services.IDocumentSigningService>();

            var mytTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
            var mytNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, mytTimeZone);
            var today = mytNow.Date;

            var utcNow = DateTime.UtcNow;

            var dueProfiles = await context.RecurringProfiles
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.InvoiceLines)
                        .ThenInclude(l => l.Taxes)
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.Supplier)
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.Customer)
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.PublicCustomer)
                .Where(p => p.Status == "Active" && p.NextRunDate.Date <= today)
                .ToListAsync();

            if (!dueProfiles.Any()) return;

            _logger.LogInformation($"Found {dueProfiles.Count} recurring profiles due for generation.");

            foreach (var profile in dueProfiles)
            {
                var historyLog = new RecurringRunHistory
                {
                    RecurringProfileId = profile.Id,
                    RunTimestamp = mytNow,
                    RunStatus = "Processing"
                };

                try
                {
                    if (profile.InvoiceTemplate == null)
                        throw new Exception("Base Invoice Template is missing or was deleted.");

                    // Shared generator: numeric max over InvoiceHeaders + InvoiceHistories with collision retry
                    // (the old local one sorted numbers as text, so EINV99999 > EINV100000 and numbers repeated).
                    string newInvoiceNo = invoiceService.GenerateNextInvoiceNumber();

                    var newInvoice = new InvoiceHeader
                    {
                        InvoiceNo = newInvoiceNo,
                        PrefixedID = newInvoiceNo,
                        CreatedDate = mytNow,
                        IssueDate = mytNow,
                        DocTypeCode = profile.InvoiceTemplate.DocTypeCode,
                        Currency = profile.InvoiceTemplate.Currency ?? "MYR",
                        ForeignCurrency = profile.InvoiceTemplate.ForeignCurrency ?? profile.InvoiceTemplate.Currency ?? "MYR",
                        ExchangeRate = profile.InvoiceTemplate.ExchangeRate,

                        SupplierId = profile.InvoiceTemplate.SupplierId,
                        Supplier = profile.InvoiceTemplate.Supplier!,

                        CustomerId = profile.InvoiceTemplate.CustomerId,
                        Customer = profile.InvoiceTemplate.Customer!,

                        PublicCustomerId = profile.InvoiceTemplate.PublicCustomerId,
                        PublicCustomer = profile.InvoiceTemplate.PublicCustomer,

                        InternalStatusId = statusMappingService.GetStatusIdByCode("Draft") ?? "Draft",
                        CreatedBy = "System_Automation",
                        UpdatedBy = "System_Automation",
                        LastUpdated = mytNow,

                        InvoicePeriod = profile.InvoiceTemplate.InvoicePeriod,
                        StartDate = profile.InvoiceTemplate.StartDate,
                        EndDate = profile.InvoiceTemplate.EndDate,
                        OriginalInvoiceDate = profile.InvoiceTemplate.OriginalInvoiceDate,
                        Notes = "Recurring invoice generated via Automation",

                        TotalDiscountAmount = profile.InvoiceTemplate.TotalDiscountAmount,
                        TotalAmountExclTax = profile.InvoiceTemplate.TotalAmountExclTax,
                        TotalTaxAmount = profile.InvoiceTemplate.TotalTaxAmount,
                        TotalAmountIncTax = profile.InvoiceTemplate.TotalAmountIncTax,
                        TotalNetAmount = profile.InvoiceTemplate.TotalNetAmount,
                        TotalPayableAmount = profile.InvoiceTemplate.TotalPayableAmount ?? profile.InvoiceTemplate.TotalAmountIncTax,

                        PaymentTerms = profile.InvoiceTemplate.PaymentTerms ?? "",
                        BankAccountNo = profile.InvoiceTemplate.BankAccountNo,
                        BankName = profile.InvoiceTemplate.BankName,
                        PoDoNo = profile.InvoiceTemplate.PoDoNo,
                        RefDocumentNo = profile.InvoiceTemplate.RefDocumentNo,
                        Attention = profile.InvoiceTemplate.Attention,

                        // FIX: Use (tl, index) to dynamically generate the LineNumber!
                        InvoiceLines = profile.InvoiceTemplate.InvoiceLines.Select((tl, index) => new InvoiceLine
                        {
                            LineNumber = index + 1,
                            ItemCode = tl.ItemCode ?? "",
                            ClassificationCode = tl.ClassificationCode,
                            ItemDescription = tl.ItemDescription,
                            Quantity = tl.Quantity ?? 1,
                            UnitOfMeasure = tl.UnitOfMeasure,
                            UnitPrice = tl.UnitPrice ?? 0,
                            DiscountAmount = tl.DiscountAmount,
                            AmountExclTax = tl.AmountExclTax,
                            AmountInclTax = tl.AmountInclTax,
                            InvoiceTaxes = tl.Taxes.Select(tax => new InvoiceTax
                            {
                                TaxCategory = tax.TaxCategory,
                                TaxPercentage = tax.TaxPercentage,
                                TaxAmount = tax.TaxAmount,
                                TaxExemptionReason = tax.TaxCategory == "E" && string.IsNullOrWhiteSpace(tax.TaxExemptionReason)
                                    ? "Tax exempted as per applicable regulations"
                                    : tax.TaxExemptionReason ?? ""
                            }).ToList()
                        }).ToList()
                    };

                    foreach (var line in newInvoice.InvoiceLines)
                    {
                        line.InvoiceHeader = newInvoice;
                        line.CalculateAmounts();
                    }

                    context.InvoiceHeaders.Add(newInvoice);
                    historyLog.GeneratedInvoiceNo = newInvoiceNo;

                    var invoiceMapper = new InvoiceMapper(context, signingService);
                    string invoiceJson = invoiceMapper.MapToJsonModel(newInvoice);

                    var draftsFolder = filePathConfig.DraftFolder;
                    if (!Directory.Exists(draftsFolder))
                    {
                        Directory.CreateDirectory(draftsFolder);
                    }
                    var draftPath = Path.Combine(draftsFolder, $"{newInvoiceNo}.json");
                    await File.WriteAllTextAsync(draftPath, invoiceJson);

                    context.InvoiceHistories.Add(new InvoiceHistory
                    {
                        InvoiceNo = newInvoiceNo,
                        Action = "Created",
                        Timestamp = utcNow,
                        PerformedBy = "System_Automation",
                        Remarks = "Invoice draft created via Recurring Profile"
                    });

                    // 7. Auto-submit: queue the standard SubmitDocument job instead of calling LHDN here. The draft
                    //    row is saved in the same SaveChanges below, so LHDN never holds a document we have no row
                    //    for, and submission goes through the one pipeline that has the atomic claim, retry and audit.
                    if (profile.AutoSubmitToMyInvois)
                    {
                        context.SyncJobs.Add(new SyncJob
                        {
                            Tin = EINVWORLD.Helpers.TinHelper.ResolveSubmitterTin(newInvoice) ?? string.Empty,
                            JobType = SyncJobType.SubmitDocument,
                            Status = SyncJobStatus.Queued,
                            QueuedAtUtc = utcNow,
                            NextRunAtUtc = utcNow,
                            MaxAttempts = 3,
                            TriggeredBy = "System (Recurring Invoice)",
                            PayloadJson = EINVWORLD.Services.Background.SyncJobPayload.CreateForInvoice(newInvoiceNo),
                        });
                        historyLog.RunStatus = "Success_SubmitQueued";
                    }
                    else
                    {
                        historyLog.RunStatus = "Success_Draft";
                    }

                    profile.NextRunDate = CalculateNextRunDate(profile.NextRunDate, profile.Frequency, today);

                    context.RecurringRunHistories.Add(historyLog);
                    await context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    context.ChangeTracker.Clear();

                    Exception realError = ex;
                    while (realError.InnerException != null)
                    {
                        realError = realError.InnerException;
                    }

                    string exactErrorMessage = realError.Message;

                    if (exactErrorMessage.Length > 250)
                    {
                        exactErrorMessage = exactErrorMessage.Substring(0, 250) + "...";
                    }

                    historyLog.RunStatus = "Failed_Internal";
                    historyLog.ErrorMessage = exactErrorMessage;

                    context.RecurringRunHistories.Add(historyLog);

                    profile.NextRunDate = CalculateNextRunDate(profile.NextRunDate, profile.Frequency, today);
                    context.RecurringProfiles.Update(profile);

                    await context.SaveChangesAsync();

                    _logger.LogError($"❌ Failed to process Recurring Profile ID {profile.Id}. Reason: {exactErrorMessage}");
                }
            }
        }

        private DateTime CalculateNextRunDate(DateTime currentRunDate, string frequency, DateTime today)
        {
            var next = AdvanceOnce(currentRunDate, frequency);

            // If the profile is behind (e.g. the worker was stopped for a while, or the app was down),
            // advance to the next occurrence STRICTLY AFTER today instead of stepping a single period
            // per worker run. Without this, an hourly worker generates one backlog invoice every hour
            // until it catches up — a "storm" of same-dated invoices. This guarantees at most one
            // generated invoice per profile per day/week/month/year.
            int guard = 0;
            while (next.Date <= today && guard++ < 4000)
            {
                next = AdvanceOnce(next, frequency);
            }

            return next;
        }

        private static DateTime AdvanceOnce(DateTime date, string frequency) => frequency switch
        {
            "Daily" => date.AddDays(1),
            "Weekly" => date.AddDays(7),
            "Monthly" => date.AddMonths(1),
            "Annually" => date.AddYears(1),
            _ => date.AddMonths(1)
        };
    }
}