using EINVWORLD.Helpers;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EINVWORLD.Helpers
{
    public class InvoiceFullSyncHelper
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<InvoiceFullSyncHelper> _logger;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _invoiceLocks = new();

        public InvoiceFullSyncHelper(IServiceScopeFactory serviceScopeFactory, ILogger<InvoiceFullSyncHelper> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task<bool> SyncAllFromApiAsync(DocumentSummary summary)
        {
            if (summary == null || string.IsNullOrWhiteSpace(summary.uuid)) return false;

            // 🔥 Get or create a lock specifically for this UUID
            var invoiceLock = _invoiceLocks.GetOrAdd(summary.uuid, _ => new SemaphoreSlim(1, 1));

            await invoiceLock.WaitAsync(); // Wait if another thread is currently processing this UUID

            try
            {
                // 🔥 Create a fresh, isolated DbContext for this specific parallel thread
                using var scope = _serviceScopeFactory.CreateScope();
                var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                using var txn = await _dbContext.Database.BeginTransactionAsync();

                var submittingTIN = await TinHelper.GetSubmittingTinFromSummaryAsync(summary, _dbContext);
                _logger.LogInformation("🔑 Submitting TIN for API sync of Invoice {InvoiceNo}: {TIN}", summary.internalId, submittingTIN);

                var rootJson = new JObject();

                // --- 1. SAFELY DECODE PAYLOAD ---
                if (!string.IsNullOrWhiteSpace(summary.document))
                {
                    string trimmedDoc = summary.document.Trim();

                    // Check if LHDN returned an HTML error page instead of JSON
                    if (trimmedDoc.StartsWith("<"))
                    {
                        _logger.LogWarning("⚠️ Received HTML instead of JSON for UUID {UUID}. Skipping raw document parsing.", summary.uuid);
                    }
                    else
                    {
                        try
                        {
                            var wrapperJson = JObject.Parse(trimmedDoc);

                            if (wrapperJson["document"] != null)
                            {
                                string innerData = wrapperJson["document"]!.ToString().Trim();

                                // Check if LHDN returned raw JSON as a string instead of Base64
                                if (innerData.StartsWith("{") || innerData.StartsWith("["))
                                {
                                    rootJson = JObject.Parse(innerData);
                                }
                                else
                                {
                                    // Safely process Base64Url and Base64 payloads
                                    string base64Data = innerData.Replace(" ", "").Replace("\r", "").Replace("\n", "");
                                    base64Data = base64Data.Replace('-', '+').Replace('_', '/');

                                    int padding = base64Data.Length % 4;
                                    if (padding > 0)
                                    {
                                        base64Data = base64Data.PadRight(base64Data.Length + (4 - padding), '=');
                                    }

                                    string decodedString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
                                    rootJson = JObject.Parse(decodedString);
                                }
                            }
                            else
                            {
                                // No wrapper, it was returned flat
                                rootJson = wrapperJson;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("⚠️ Failed to parse raw document JSON for UUID {UUID}: {Msg}", summary.uuid, ex.Message);
                        }
                    }
                }

                // --- 2. DYNAMICALLY FIND DOCUMENT ROOT (Invoice, CreditNote, DebitNote, etc.) ---
                JToken? invoiceJson = null;
                if (rootJson != null)
                {
                    var docNodeName = rootJson.Properties()
                        .Select(p => p.Name)
                        .FirstOrDefault(n => n == "Invoice" || n == "CreditNote" || n == "DebitNote" || n == "RefundNote");

                    if (docNodeName != null)
                    {
                        var node = rootJson[docNodeName];
                        if (node is JArray array && array.Count > 0) invoiceJson = array[0];
                        else if (node is JObject obj) invoiceJson = obj;
                    }
                    else
                    {
                        invoiceJson = rootJson; // Fallback
                    }
                }

                var supplierJson = invoiceJson?["AccountingSupplierParty"]?[0]?["Party"]?[0];
                var customerJson = invoiceJson?["AccountingCustomerParty"]?[0]?["Party"]?[0];

                // Extract TINs from the JSON safely to determine direction
                string? supplierTinFromJson = supplierJson?["PartyIdentification"]?.Children<JToken>().FirstOrDefault(x => x["ID"]?[0]?["schemeID"]?.ToString() == "TIN")?["ID"]?[0]?["_"]?.ToString() ?? summary.issuerTin;
                string? customerTinFromJson = customerJson?["PartyIdentification"]?.Children<JToken>().FirstOrDefault(x => x["ID"]?[0]?["schemeID"]?.ToString() == "TIN")?["ID"]?[0]?["_"]?.ToString() ?? summary.receiverTin;

                PartyInfo? supplier = null;
                PartyInfo? customer = null;
                PublicCustomer? publicCustomer = null;

                // Find the ID of the company fetching this data
                var submittingCompany = await _dbContext.PartyInfos.FirstOrDefaultAsync(p => p.TIN == submittingTIN);
                int submittingCompanyId = submittingCompany?.PartyInfoId ?? 0;

                // Determine Direction: If the supplier TIN matches the TIN we are fetching with, it's a "Sent" invoice.
                bool isSentInvoice = (supplierTinFromJson == submittingTIN);

                if (isSentInvoice)
                {
                    // --- WE ARE THE SUPPLIER (SENT INVOICE) ---
                    supplier = await ParsePartyInfoFromJson.GetOrCreatePartyInfoAsync(_dbContext, supplierJson); // Just returns us

                    // Resolve Buyer: Try global PartyInfo first (in case it's a known B2B partner)
                    customer = await _dbContext.PartyInfos.FirstOrDefaultAsync(p => p.TIN == customerTinFromJson);

                    if (customer == null)
                    {
                        // Not in PartyInfo. Check our isolated PublicCustomer table under OUR company ID
                        publicCustomer = await _dbContext.PublicCustomers.FirstOrDefaultAsync(p => p.TIN == customerTinFromJson && p.CreatedByCompanyId == submittingCompanyId);

                        if (publicCustomer == null && customerJson != null)
                        {
                            // Buyer is entirely unknown. Create them in PublicCustomer under OUR company
                            publicCustomer = ParsePartyInfoFromJson.ExtractPublicCustomer(customerJson, customerTinFromJson, submittingCompanyId);
                            if (publicCustomer != null)
                            {
                                _dbContext.PublicCustomers.Add(publicCustomer);
                                await _dbContext.SaveChangesAsync(); // Save immediately so we can get the ID
                            }
                        }
                    }
                }
                else
                {
                    // --- WE ARE THE BUYER (RECEIVED INVOICE) ---
                    customer = await ParsePartyInfoFromJson.GetOrCreatePartyInfoAsync(_dbContext, customerJson); // Just returns us

                    // Resolve Supplier: We always put suppliers in the global PartyInfo table
                    supplier = await ParsePartyInfoFromJson.GetOrCreatePartyInfoAsync(_dbContext, supplierJson);
                }

                // --- FALLBACK IF RAW DOCUMENT ACCESS WAS DENIED BY LHDN ---
                if (string.IsNullOrWhiteSpace(summary.document))
                {
                    _logger.LogWarning("⚠️ Raw document access denied by LHDN for TIN {TIN}. Falling back to summary data.", submittingTIN);
                }

                if (supplier == null && !string.IsNullOrWhiteSpace(summary.issuerTin))
                {
                    supplier = await _dbContext.PartyInfos.FirstOrDefaultAsync(p => p.TIN == summary.issuerTin);
                }

                if (customer == null && publicCustomer == null && !string.IsNullOrWhiteSpace(summary.receiverTin))
                {
                    customer = await _dbContext.PartyInfos.FirstOrDefaultAsync(p => p.TIN == summary.receiverTin);

                    // Optional: If we STILL didn't find them in PartyInfos, check PublicCustomer using the fallback TIN
                    if (customer == null)
                    {
                        publicCustomer = await _dbContext.PublicCustomers.FirstOrDefaultAsync(p => p.TIN == summary.receiverTin && p.CreatedByCompanyId == submittingCompanyId);
                    }
                }

                _logger.LogInformation("📊 Parsed supplier: {SupplierTIN} (ID: {SupplierID}), customer: {CustomerTIN} (ID: {CustomerID})",
                    supplier?.TIN, supplier?.PartyInfoId, customer?.TIN, customer?.PartyInfoId);

                string Truncate(string? val, int maxLength) =>
                    string.IsNullOrWhiteSpace(val) ? val ?? string.Empty : (val.Length > maxLength ? val.Substring(0, maxLength) : val);

                string currencyCode = Truncate(invoiceJson?["DocumentCurrencyCode"]?[0]?["_"]?.ToString() ?? "MYR", 3);
                string foreignCurrency = Truncate(invoiceJson?["TaxExchangeRate"]?[0]?["SourceCurrencyCode"]?[0]?["_"]?.ToString(), 3);
                decimal? exchangeRate = invoiceJson?["TaxExchangeRate"]?[0]?["CalculationRate"]?[0]?["_"]?.ToObject<decimal?>();

                decimal? jsonTaxIncAmount = invoiceJson?["LegalMonetaryTotal"]?[0]?["TaxInclusiveAmount"]?[0]?["_"]?.ToObject<decimal?>();
                decimal? jsonTaxAmount = invoiceJson?["TaxTotal"]?[0]?["TaxAmount"]?[0]?["_"]?.ToObject<decimal?>();

                string bankAccountNo = Truncate(invoiceJson?["PaymentMeans"]?[0]?["PayeeFinancialAccount"]?[0]?["ID"]?[0]?["_"]?.ToString(), 150);
                string bankName = Truncate(invoiceJson?["PaymentMeans"]?[0]?["PayeeFinancialAccount"]?[0]?["Name"]?[0]?["_"]?.ToString(), 100);
                string paymentTerms = Truncate(invoiceJson?["PaymentTerms"]?[0]?["Note"]?[0]?["_"]?.ToString(), 300);
                string poDoNo = Truncate(invoiceJson?["OrderReference"]?[0]?["ID"]?[0]?["_"]?.ToString(), 100);
                string refDocumentNo = Truncate(invoiceJson?["BillingReference"]?[0]?["InvoiceDocumentReference"]?[0]?["ID"]?[0]?["_"]?.ToString(), 100);
                string refUuid = Truncate(invoiceJson?["BillingReference"]?[0]?["InvoiceDocumentReference"]?[0]?["UUID"]?[0]?["_"]?.ToString(), 100);

                var invoice = await _dbContext.InvoiceHeaders
                                    .Include(x => x.InvoiceLines)
                                    .ThenInclude(y => y.InvoiceTaxes)
                                    .FirstOrDefaultAsync(i => i.UUID == summary.uuid);

                var docTypeCode = await EInvoiceTypeHelper.GetCodeFromDescriptionAsync(summary.typeName, _dbContext);

                decimal? calculatedTaxAmount = null;
                if (summary.total.HasValue && summary.totalExcludingTax.HasValue)
                {
                    calculatedTaxAmount = summary.total.Value - summary.totalExcludingTax.Value;
                }

                if (invoice == null)
                {
                    string finalInvoiceNo = summary.internalId;
                    int counter = 1;

                    while (await _dbContext.InvoiceHeaders.AnyAsync(i => i.InvoiceNo == finalInvoiceNo))
                    {
                        finalInvoiceNo = $"{summary.internalId}({counter})";
                        counter++;
                    }

                    invoice = new InvoiceHeader
                    {
                        InvoiceNo = finalInvoiceNo,
                        PrefixedID = finalInvoiceNo,
                        Currency = currencyCode,
                        ForeignCurrency = foreignCurrency,
                        ExchangeRate = exchangeRate,
                        DocTypeCode = docTypeCode ?? "01",
                        BankAccountNo = bankAccountNo,
                        BankName = bankName,
                        PaymentTerms = paymentTerms,
                        PoDoNo = poDoNo,
                        RefDocumentNo = refDocumentNo,
                        RefUUID = refUuid,
                        IsValidationEmailSent = true,
                        ValidationEmailSentAt = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow),
                        ValidationEmailSentTo = "System (Skipped for Sync)",

                        UUID = summary.uuid,
                        SubmissionID = summary.submissionUid,
                        LongId = summary.longId,
                        InternalStatusId = summary.status,
                        LHDNStatusId = summary.status,

                        CreatedDate = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow),
                        LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow),
                        IssueDate = summary.dateTimeIssued.HasValue ? DateTimeHelper.ToMalaysiaTime(summary.dateTimeIssued.Value) : null,
                        DateTimeReceived = summary.dateTimeReceived.HasValue ? DateTimeHelper.ToMalaysiaTime(summary.dateTimeReceived.Value) : null,
                        DateTimeValidated = summary.dateTimeValidated.HasValue ? DateTimeHelper.ToMalaysiaTime(summary.dateTimeValidated.Value) : null,
                        CancelDateTime = summary.cancelDateTime?.ToLocalTime(),
                        RejectedTimestamp = summary.rejectRequestDateTime?.ToLocalTime(),

                        RejectedReason = summary.documentStatusReason,
                        RejectedBy = summary.rejectedByUserId,
                        CreatedBy = summary.createdByUserId ?? "SystemSync",

                        TotalAmountExclTax = summary.totalExcludingTax,
                        TotalDiscountAmount = summary.totalDiscount,
                        TotalNetAmount = summary.totalNetAmount,
                        TotalPayableAmount = summary.totalPayableAmount,
                        TotalAmountIncTax = jsonTaxIncAmount ?? summary.total,
                        TotalTaxAmount = jsonTaxAmount ?? calculatedTaxAmount,

                        SupplierId = supplier?.PartyInfoId,
                        CustomerId = customer?.PartyInfoId,
                        PublicCustomerId = publicCustomer?.PublicCustomerId,
                    };

                    _dbContext.InvoiceHeaders.Add(invoice);
                }
                else
                {
                    invoice.SubmissionID = summary.submissionUid ?? invoice.SubmissionID;
                    invoice.LongId = summary.longId ?? invoice.LongId;
                    invoice.DocTypeCode = docTypeCode ?? invoice.DocTypeCode;
                    invoice.LHDNStatusId = summary.status ?? invoice.LHDNStatusId;
                    invoice.InternalStatusId = summary.status ?? invoice.InternalStatusId;
                    invoice.Currency = string.IsNullOrWhiteSpace(currencyCode) ? invoice.Currency : currencyCode;

                    invoice.ForeignCurrency = string.IsNullOrWhiteSpace(foreignCurrency) ? invoice.ForeignCurrency : foreignCurrency;
                    invoice.ExchangeRate = exchangeRate ?? invoice.ExchangeRate;

                    invoice.BankAccountNo = string.IsNullOrWhiteSpace(bankAccountNo) ? invoice.BankAccountNo : bankAccountNo;
                    invoice.BankName = string.IsNullOrWhiteSpace(bankName) ? invoice.BankName : bankName;
                    invoice.PaymentTerms = string.IsNullOrWhiteSpace(paymentTerms) ? invoice.PaymentTerms : paymentTerms;
                    invoice.PoDoNo = string.IsNullOrWhiteSpace(poDoNo) ? invoice.PoDoNo : poDoNo;
                    invoice.RefDocumentNo = string.IsNullOrWhiteSpace(refDocumentNo) ? invoice.RefDocumentNo : refDocumentNo;
                    invoice.RefUUID = string.IsNullOrWhiteSpace(refUuid) ? invoice.RefUUID : refUuid;

                    invoice.IssueDate = summary.dateTimeIssued.HasValue ? DateTimeHelper.ToMalaysiaTime(summary.dateTimeIssued.Value) : invoice.IssueDate;
                    invoice.DateTimeReceived = summary.dateTimeReceived.HasValue ? DateTimeHelper.ToMalaysiaTime(summary.dateTimeReceived.Value) : invoice.DateTimeReceived;
                    invoice.DateTimeValidated = summary.dateTimeValidated.HasValue ? DateTimeHelper.ToMalaysiaTime(summary.dateTimeValidated.Value) : invoice.DateTimeValidated;
                    invoice.CancelDateTime = summary.cancelDateTime?.ToLocalTime() ?? invoice.CancelDateTime;
                    invoice.RejectedTimestamp = summary.rejectRequestDateTime?.ToLocalTime() ?? invoice.RejectedTimestamp;

                    invoice.RejectedReason = summary.documentStatusReason ?? invoice.RejectedReason;
                    invoice.RejectedBy = summary.rejectedByUserId ?? invoice.RejectedBy;

                    invoice.TotalAmountExclTax = summary.totalExcludingTax ?? invoice.TotalAmountExclTax;
                    invoice.TotalDiscountAmount = summary.totalDiscount ?? invoice.TotalDiscountAmount;
                    invoice.TotalNetAmount = summary.totalNetAmount ?? invoice.TotalNetAmount;
                    invoice.TotalPayableAmount = summary.totalPayableAmount ?? invoice.TotalPayableAmount;

                    invoice.TotalAmountIncTax = jsonTaxIncAmount ?? summary.total ?? invoice.TotalAmountIncTax;
                    invoice.TotalTaxAmount = jsonTaxAmount ?? calculatedTaxAmount ?? invoice.TotalTaxAmount;

                    invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);

                    invoice.SupplierId = supplier?.PartyInfoId ?? invoice.SupplierId;
                    if (customer != null)
                    {
                        invoice.CustomerId = customer.PartyInfoId;
                        invoice.PublicCustomerId = null;
                    }
                    else if (publicCustomer != null)
                    {
                        invoice.PublicCustomerId = publicCustomer.PublicCustomerId;
                        invoice.CustomerId = null;
                    }

                    if (invoiceJson != null)
                    {
                        var existingTaxes = invoice.InvoiceLines?
                            .SelectMany(line => line.InvoiceTaxes ?? new List<InvoiceTax>())
                            .ToList() ?? new List<InvoiceTax>();

                        if (existingTaxes.Any()) _dbContext.InvoiceTaxes.RemoveRange(existingTaxes);
                        if (invoice.InvoiceLines != null && invoice.InvoiceLines.Any()) _dbContext.InvoiceLines.RemoveRange(invoice.InvoiceLines);
                    }
                }

                if (invoiceJson != null)
                {
                    var lineArr = (invoiceJson?["InvoiceLine"] ??
                                   invoiceJson?["CreditNoteLine"] ??
                                   invoiceJson?["DebitNoteLine"]) as JArray;

                    var newLines = new List<InvoiceLine>();

                    if (lineArr != null)
                    {
                        int idx = 1;
                        foreach (var lineToken in lineArr)
                        {
                            var newLine = new InvoiceLine
                            {
                                InvoiceHeaderInvoiceNo = invoice.InvoiceNo,
                                LineNumber = idx,
                                ItemDescription = lineToken["Item"]?[0]?["Description"]?[0]?["_"]?.ToString() ?? string.Empty,
                                ClassificationCode = lineToken["Item"]?[0]?["CommodityClassification"]?[0]?["ItemClassificationCode"]?[0]?["_"]?.ToString() ?? string.Empty,
                                Quantity = lineToken["InvoicedQuantity"]?[0]?["_"]?.ToObject<decimal?>() ??
                                           lineToken["CreditedQuantity"]?[0]?["_"]?.ToObject<decimal?>() ??
                                           lineToken["DebitedQuantity"]?[0]?["_"]?.ToObject<decimal?>() ?? 0,
                                UnitOfMeasure = lineToken["InvoicedQuantity"]?[0]?["unitCode"]?.ToString() ??
                                                lineToken["CreditedQuantity"]?[0]?["unitCode"]?.ToString() ??
                                                lineToken["DebitedQuantity"]?[0]?["unitCode"]?.ToString() ?? string.Empty,
                                UnitPrice = lineToken["Price"]?[0]?["PriceAmount"]?[0]?["_"]?.ToObject<decimal?>() ?? 0,
                                Subtotal = lineToken["LineExtensionAmount"]?[0]?["_"]?.ToObject<decimal?>() ?? 0,
                                AmountExclTax = lineToken["LineExtensionAmount"]?[0]?["_"]?.ToObject<decimal?>() ?? 0,
                                InvoiceTaxes = new List<InvoiceTax>()
                            };

                            var taxTotal = lineToken["TaxTotal"]?.FirstOrDefault();
                            var taxSubtotals = taxTotal?["TaxSubtotal"] as JArray;
                            if (taxSubtotals != null)
                            {
                                foreach (var taxToken in taxSubtotals)
                                {
                                    newLine.InvoiceTaxes.Add(new InvoiceTax
                                    {
                                        TaxCategory = taxToken["TaxCategory"]?[0]?["ID"]?[0]?["_"]?.ToString() ?? string.Empty,
                                        TaxPercentage = taxToken["Percent"]?[0]?["_"]?.ToObject<decimal?>() ?? 0,
                                        TaxAmount = taxToken["TaxAmount"]?[0]?["_"]?.ToObject<decimal?>() ?? 0,
                                    });
                                }
                            }

                            newLines.Add(newLine);
                            idx++;
                        }
                    }
                    if (newLines.Any()) _dbContext.InvoiceLines.AddRange(newLines);
                }

                await _dbContext.SaveChangesAsync();
                await txn.CommitAsync();

                return true;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to sync Invoice {InvoiceNo} from LHDN API: {Error}", summary?.internalId, ex.Message);
                return false;
            }
            finally
            {
                invoiceLock.Release();
            }
        }
    }
}