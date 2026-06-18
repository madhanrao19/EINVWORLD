using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.Logs;
using eInvWorld.Models.ViewModels;
using eInvWorld.Services.Extensions;
using eInvWorld.Services.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eInvWorld.Services
{
    public class InvoiceDraftService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceDraftService> _logger;
        private readonly FilePathConfig _filePathConfig;
        private readonly InvoiceHistoryService _historyService;
        private readonly IStatusMappingService _statusMappingService;

        public InvoiceDraftService(
            ApplicationDbContext context,
            ILogger<InvoiceDraftService> logger,
            IOptions<FilePathConfig> filePathConfig,
            InvoiceHistoryService historyService,
            IStatusMappingService statusMappingService)
        {
            _context = context;
            _logger = logger;
            _filePathConfig = filePathConfig.Value;
            _historyService = historyService;
            _statusMappingService = statusMappingService;
        }

        public bool SaveDraft(InvoiceHeaderView model, string username, PartyInfo supplier, PartyInfo customer, string invoiceJson, ISession session)
        {
            try
            {
                var existing = _context.InvoiceHeaders
                    .Include(i => i.InvoiceLines)
                        .ThenInclude(l => l.InvoiceTaxes)
                    .FirstOrDefault(i => i.InvoiceNo == model.InvoiceNo);

                if (existing != null)
                {
                    _logger.LogInformation("🔁 Updating existing draft for {InvoiceNo}", model.InvoiceNo);

                    // Scalar updates
                    existing.RefDocumentNo = model.RefDocumentNo;
                    existing.StartDate = model.StartDate;
                    existing.EndDate = model.EndDate;
                    existing.IssueDate = model.IssueDate;
                    existing.Currency = model.Currency ?? "MYR";
                    existing.ExchangeRate = model.ExchangeRate;
                    existing.DocTypeCode = model.DocTypeCode ?? "01";
                    existing.InvoicePeriod = model.InvoicePeriod;
                    existing.Supplier = supplier;
                    existing.Customer = customer;
                    existing.TotalAmountExclTax = model.TotalAmountExclTax;
                    existing.TotalTaxAmount = model.TotalTaxAmount;
                    existing.TotalAmountIncTax = model.TotalAmountIncTax;
                    existing.TotalPayableAmount = model.TotalPayableAmount;
                    existing.TotalNetAmount = model.TotalNetAmount;
                    existing.UpdatedBy = username;
                    existing.LastUpdated = DateTime.UtcNow;

                    // Clear and replace lines
                    _context.InvoiceLines.RemoveRange(existing.InvoiceLines);
                    existing.InvoiceLines = MapInvoiceLines(model) ?? new List<InvoiceLine>();

                    _historyService.Log(existing.InvoiceNo, "Updated", "Invoice draft updated");
                }
                else
                {
                    _logger.LogInformation("🆕 Creating new draft for {InvoiceNo}", model.InvoiceNo);

                    var newInvoice = new InvoiceHeader
                    {
                        InvoiceNo = model.InvoiceNo ?? string.Empty,
                        RefDocumentNo = model.RefDocumentNo,
                        StartDate = model.StartDate,
                        EndDate = model.EndDate,
                        IssueDate = model.IssueDate,
                        Currency = model.Currency ?? "MYR",
                        ExchangeRate = model.ExchangeRate,
                        DocTypeCode = model.DocTypeCode ?? "01",
                        InvoicePeriod = model.InvoicePeriod,
                        Supplier = supplier,
                        Customer = customer,
                        TotalAmountExclTax = model.TotalAmountExclTax,
                        TotalTaxAmount = model.TotalTaxAmount,
                        TotalAmountIncTax = model.TotalAmountIncTax,
                        TotalPayableAmount = model.TotalPayableAmount,
                        TotalNetAmount = model.TotalNetAmount,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = username,
                        LastUpdated = DateTime.UtcNow,
                        UpdatedBy = username,
                        InternalStatusId = _statusMappingService.GetStatusIdByCode("Draft"),
                        InvoiceLines = MapInvoiceLines(model) ?? new List<InvoiceLine>()
                    };

                    _context.InvoiceHeaders.Add(newInvoice);
                    _historyService.Log(model.InvoiceNo ?? string.Empty, "Created", "New invoice draft created");
                }

                _context.SaveChanges();

                var filePath = Path.Combine(_filePathConfig.DraftFolder, $"{model.InvoiceNo}.json");
                Directory.CreateDirectory(_filePathConfig.DraftFolder);
                File.WriteAllText(filePath, invoiceJson);
                session.SetString("DraftFilePath", filePath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to save draft {InvoiceNo}", model.InvoiceNo);
                return false;
            }
        }


        public List<InvoiceLine>? MapInvoiceLines(InvoiceHeaderView invoiceView)
        {
            return invoiceView.InvoiceLines?.Select(line => new InvoiceLine
            {
                LineNumber = line.LineNumber,
                ItemCode = line.ItemCode,
                ItemDescription = line.ItemDescription,
                UnitOfMeasure = line.UnitOfMeasure,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                AmountInclTax = line.AmountInclTax,
                AmountExclTax = line.AmountExclTax,
                DiscountAmount = line.DiscountAmount,
                ClassificationCode = line.ClassificationCode,
                InvoiceTaxes = line.Taxes?.Select(tax => new InvoiceTax
                {
                    TaxCategory = tax.TaxCategory,
                    TaxPercentage = tax.TaxPercentage,
                    TaxAmount = tax.TaxAmount,
                    TaxExemptionReason = tax.TaxExemptionReason
                }).ToList() ?? new List<InvoiceTax>()
            }).ToList();
        }

        public bool EditDraft(InvoiceHeaderView model, string updatedBy, PartyInfo supplier, PartyInfo customer, string json, ISession session)
        {
            try
            {
                var existing = _context.InvoiceHeaders
                    .Include(i => i.InvoiceLines)
                    .ThenInclude(l => l.InvoiceTaxes)
                    .FirstOrDefault(i => i.InvoiceNo == model.InvoiceNo);

                if (existing == null)
                    return false;

                existing.RefDocumentNo = model.RefDocumentNo;
                existing.IssueDate = model.IssueDate;
                existing.StartDate = model.StartDate;
                existing.EndDate = model.EndDate;
                existing.DocTypeCode = model.DocTypeCode;
                existing.Currency = model.Currency;
                existing.ExchangeRate = model.ExchangeRate;
                existing.SupplierId = supplier.PartyInfoId;
                existing.CustomerId = customer.PartyInfoId;
                existing.InvoicePeriod = model.InvoicePeriod;
                existing.TotalAmountExclTax = model.TotalAmountExclTax;
                existing.TotalTaxAmount = model.TotalTaxAmount;
                existing.TotalAmountIncTax = model.TotalAmountIncTax;
                existing.TotalPayableAmount = model.TotalPayableAmount;
                existing.TotalNetAmount = model.TotalNetAmount;
                existing.LastUpdated = DateTime.UtcNow;
                existing.UpdatedBy = updatedBy;

                _context.InvoiceLines.RemoveRange(existing.InvoiceLines);
                existing.InvoiceLines = MapInvoiceLines(model) ?? new List<InvoiceLine>();

                _context.SaveChanges();

                // Save JSON file
                var draftPath = Path.Combine(_filePathConfig.DraftFolder, $"{model.InvoiceNo}.json");
                File.WriteAllText(draftPath, json);
                session.SetString("DraftFilePath", draftPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to update draft invoice {model.InvoiceNo}");
                return false;
            }
        }

    }
}
