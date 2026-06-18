using eInvWorld.Data;
using eInvWorld.Models.JsonModels;
using eInvWorld.Models.Templates;
using eInvWorld.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace eInvWorld.Services
{
    public class InvoiceTemplateService
    {
        public List<InvoiceTemplateLine> MapToTemplateLines(List<InvoiceLineView> lines)
        {
            return lines.Select(line =>
            {
                var templateLine = new InvoiceTemplateLine
                {
                    ClassificationCode = line.ClassificationCode,
                    ItemCode = string.IsNullOrWhiteSpace(line.ItemCode) ? "" : line.ItemCode,
                    ItemDescription = line.ItemDescription,
                    Quantity = line.Quantity,
                    UnitOfMeasure = line.UnitOfMeasure,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount,
                    Taxes = line.Taxes.Select(tax => new InvoiceTemplateTax
                    {
                        TaxCategory = tax.TaxCategory,
                        TaxPercentage = tax.TaxPercentage,
                        TaxAmount = tax.TaxAmount,
                        TaxExemptionReason = tax.TaxExemptionReason ?? ""
                    }).ToList()
                };

                // ✅ Perform calculation for subtotal and tax
                templateLine.CalculateAmounts();

                return templateLine;
            }).ToList();
        }

        // Optionally move this here too
        public string GenerateNextTemplateName(ApplicationDbContext _context)
        {
            var lastTemplate = _context.InvoiceTemplates
                .OrderByDescending(t => t.Id)
                .FirstOrDefault()?.TemplateName;

            int lastNumber = 0;
            if (!string.IsNullOrEmpty(lastTemplate) && lastTemplate.StartsWith("Template_TPL"))
            {
                int.TryParse(lastTemplate.Substring(13), out lastNumber); // skip "Template_TPL"
            }

            return $"Template_TPL{(lastNumber + 1):D5}";
        }

        public InvoiceTemplate CreateTemplateFromInvoice(InvoiceHeaderView invoice, string templateName, string userId, ApplicationDbContext _context)
        {
            return new InvoiceTemplate
            {
                TemplateName = string.IsNullOrWhiteSpace(templateName)
                    ? GenerateNextTemplateName(_context)
                    : templateName.Trim(),
                DocTypeCode = invoice.DocTypeCode,
                RefDocumentNo = invoice.RefDocumentNo,
                SupplierId = invoice.SupplierId,

                // 🔥 HYBRID FIX: Safely map BOTH Customer types and handle zeros as nulls
                CustomerId = invoice.CustomerId > 0 ? invoice.CustomerId : null,
                PublicCustomerId = invoice.PublicCustomerId > 0 ? invoice.PublicCustomerId : null,

                Currency = invoice.Currency,
                ExchangeRate = invoice.ExchangeRate,
                ForeignCurrency = string.IsNullOrEmpty(invoice.ForeignCurrency)
                    ? invoice.Currency ?? "MYR" // ✅ Default fallback
                    : invoice.ForeignCurrency,
                StartDate = invoice.StartDate,
                EndDate = invoice.EndDate,
                InvoicePeriod = invoice.InvoicePeriod,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                InvoiceLines = MapToTemplateLines(invoice.InvoiceLines),
                TotalAmountExclTax = invoice.TotalAmountExclTax,
                TotalTaxAmount = invoice.TotalTaxAmount,
                TotalAmountIncTax = invoice.TotalAmountIncTax,
                TotalDiscountAmount = invoice.TotalDiscountAmount,
                TotalPayableAmount = invoice.TotalPayableAmount,
                TotalNetAmount = invoice.TotalNetAmount,
                // Additional fields to match InvoiceHeaderView
                Notes = invoice.Notes,
                OldRegNo = invoice.OldRegNo,
                BankAccountNo = invoice.BankAccountNo,
                BankName = invoice.BankName,
                Attention = invoice.Attention,
                OriginalInvoiceDate = invoice.OriginalInvoiceDate,
                PoDoNo = invoice.PoDoNo,
                PaymentTerms = invoice.PaymentTerms
            };
        }
    }
}