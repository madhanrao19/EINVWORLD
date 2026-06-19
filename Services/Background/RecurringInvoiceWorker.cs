using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.Recurring;
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
using System.Text.RegularExpressions;
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
            var lhdnApiService = scope.ServiceProvider.GetRequiredService<LHDNApiService>();
            var filePathConfig = scope.ServiceProvider.GetRequiredService<IOptions<FilePathConfig>>().Value;
            var statusMappingService = scope.ServiceProvider.GetRequiredService<IStatusMappingService>();

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

                    string newInvoiceNo = await GenerateInvoiceNumberAsync(context);

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

                    var invoiceMapper = new InvoiceMapper();
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

                    // 7. Handle LHDN Auto-Submission if toggled ON
                    if (profile.AutoSubmitToMyInvois)
                    {
                        try
                        {
                            // ✅ 7a. Get the correct TIN from the Database objects (Bypasses Session)
                            string? tin = EINVWORLD.Helpers.TinHelper.ResolveSubmitterTin(newInvoice);

                            if (string.IsNullOrWhiteSpace(tin))
                            {
                                throw new Exception($"Missing TIN for document type {newInvoice.DocTypeCode}. Cannot auto-submit.");
                            }

                            var documentPayload = new eInvWorld.Models.JsonModels.Documents
                            {
                                Format = "JSON",
                                DocumentHash = lhdnApiService.ComputeSHA256Hash(invoiceJson),
                                CodeNumber = newInvoice.InvoiceNo,
                                Document = lhdnApiService.Base64Encode(invoiceJson)
                            };

                            var payloadList = new System.Collections.Generic.List<eInvWorld.Models.JsonModels.Documents> { documentPayload };

                            // ✅ 7b. Pass the TIN into the API Service
                            var responseJson = await lhdnApiService.SubmitDocumentsAsync(payloadList, tin);

                            // 🚀 NEW FIX: Parse the LHDN JSON response to extract UUID and SubmissionID
                            var apiResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<eInvWorld.Models.Document.SuccessSubmit>(responseJson);
                            var acceptedDoc = apiResponse?.acceptedDocuments?.FirstOrDefault();

                            if (acceptedDoc != null && !string.IsNullOrEmpty(acceptedDoc.uuid))
                            {
                                newInvoice.UUID = acceptedDoc.uuid;
                                newInvoice.SubmissionID = apiResponse?.submissionUID;
                                _logger.LogInformation($"✅ Captured UUID: {newInvoice.UUID} and SubmissionID: {newInvoice.SubmissionID} for {newInvoice.InvoiceNo}");
                            }

                            newInvoice.InternalStatusId = statusMappingService.GetStatusIdByCode("Submitted") ?? "Submitted";
                            newInvoice.LHDNStatusId = "Submitted";
                            historyLog.RunStatus = "Success_Submitted";

                            // Log Submission
                            context.InvoiceHistories.Add(new InvoiceHistory
                            {
                                InvoiceNo = newInvoiceNo,
                                Action = "Submitted",
                                Timestamp = utcNow,
                                PerformedBy = "System_Automation",
                                Remarks = $"Auto-submitted to LHDN by Recurring worker. UUID: {newInvoice.UUID}"
                            });
                        }
                        catch (Exception apiEx)
                        {
                            newInvoice.InternalStatusId = statusMappingService.GetStatusIdByCode("Draft") ?? "Draft";
                            historyLog.RunStatus = "LHDN_Failed";
                            historyLog.ErrorMessage = apiEx.Message;
                            _logger.LogWarning($"LHDN Auto-Submit failed for {newInvoiceNo}: {apiEx.Message}");
                        }
                    }
                    else
                    {
                        historyLog.RunStatus = "Success_Draft";
                    }

                    profile.NextRunDate = CalculateNextRunDate(profile.NextRunDate, profile.Frequency);

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

                    profile.NextRunDate = CalculateNextRunDate(profile.NextRunDate, profile.Frequency);
                    context.RecurringProfiles.Update(profile);

                    await context.SaveChangesAsync();

                    _logger.LogError($"❌ Failed to process Recurring Profile ID {profile.Id}. Reason: {exactErrorMessage}");
                }
            }
        }

        private async Task<string> GenerateInvoiceNumberAsync(ApplicationDbContext context)
        {
            var latestInvoice = await context.InvoiceHeaders
                .Where(i => i.InvoiceNo.StartsWith("EINV"))
                .OrderByDescending(i => i.InvoiceNo)
                .Select(i => i.InvoiceNo)
                .FirstOrDefaultAsync();

            int nextSequence = 1;

            if (!string.IsNullOrEmpty(latestInvoice))
            {
                var match = Regex.Match(latestInvoice, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int currentSequence))
                {
                    nextSequence = currentSequence + 1;
                }
            }

            return $"EINV{nextSequence:D5}";
        }

        private DateTime CalculateNextRunDate(DateTime currentRunDate, string frequency)
        {
            return frequency switch
            {
                "Daily" => currentRunDate.AddDays(1),
                "Weekly" => currentRunDate.AddDays(7),
                "Monthly" => currentRunDate.AddMonths(1),
                "Annually" => currentRunDate.AddYears(1),
                _ => currentRunDate.AddMonths(1)
            };
        }
    }
}