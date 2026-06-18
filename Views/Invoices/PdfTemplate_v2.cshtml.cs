using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using eInvWorld.Pages.Invoices;
using eInvWorld.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EINVWORLD.Views.Invoices
{
    [AllowAnonymous]
    public class PdfTemplate_v2Model : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PdfTemplateModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly QRCodeGeneratorService _qrCodeService;
        private readonly DropdownHelper _dropdownHelper;


        public InvoiceHeader? InvoiceDetail { get; set; }
        public List<InvoiceLine> InvoiceLines { get; set; } = new();
        public DropdownHelper DropdownHelper { get; }

        public string? InvoiceTypeDescription { get; set; }
        // Totals
        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalAmountInclTax { get; set; }
        public string? FullCurrencyName { get; set; }
        public string? QRCodeBase64 { get; set; }

        public PdfTemplate_v2Model(ApplicationDbContext context, ILogger<PdfTemplateModel> logger, IConfiguration configuration, QRCodeGeneratorService qrCodeService, DropdownHelper dropdownHelper)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _qrCodeService = qrCodeService;
            _dropdownHelper = dropdownHelper;
            DropdownHelper = dropdownHelper;
        }
        public async Task OnGetAsync(string InvoiceNo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(InvoiceNo))
                {
                    _logger.LogError("Invoice No. is missing.");
                    return;
                }

                InvoiceDetail = await _context.InvoiceHeaders
                .Include(i => i.Supplier)  
                    .ThenInclude(s => s.State)   
                .Include(i => i.Supplier)
                    .ThenInclude(s => s.Country) 
                .Include(i => i.Customer) 
                    .ThenInclude(c => c.State) 
                .Include(i => i.Customer)
                    .ThenInclude(c => c.Country) 
                .Include(i => i.PublicCustomer)
                    .ThenInclude(p => p!.State)
                .Include(i => i.PublicCustomer)
                    .ThenInclude(p => p!.Country)
                .Include(i => i.InvoiceLines)
                .ThenInclude(il => il.InvoiceTaxes)
                .Include(i => i.LHDNStatus) 
                .Include(i => i.InternalStatus) 
                .FirstOrDefaultAsync(i => i.InvoiceNo == InvoiceNo);

                if (InvoiceDetail == null)
                {
                    _logger.LogError($"Invoice with InvoiceNo {InvoiceNo} not found.");
                    return;
                }

                InvoiceLines = InvoiceDetail.InvoiceLines.ToList(); // Store InvoiceLines separately

                //  **Calculate Totals**
                if (InvoiceLines.Any())
                {
                    TotalQuantity = InvoiceLines.Sum(item => item.Quantity ?? 0);
                    TotalAmount = InvoiceLines.Sum(item => (item.Quantity ?? 0) * (item.UnitPrice ?? 0));
                    TotalDiscount = InvoiceLines.Sum(item => item.DiscountAmount ?? 0);
                    TotalAmountInclTax = InvoiceLines.Sum(item => item.AmountInclTax ?? 0);
                }

                // ✅ Generate QR Code using UUID and LongId
                if (!string.IsNullOrEmpty(InvoiceDetail.UUID) && !string.IsNullOrEmpty(InvoiceDetail.LongId))
                {
                    QRCodeBase64 = _qrCodeService.GenerateQRCodeBase64(InvoiceDetail.UUID, InvoiceDetail.LongId);
                }

                // Map the Document Type Code to its Description
                var typeEntry = await _context.EInvoiceTypes
                    .FirstOrDefaultAsync(t => t.Code == InvoiceDetail.DocTypeCode);
                InvoiceTypeDescription = typeEntry?.Description ?? InvoiceDetail.DocTypeCode;

                var taxTypes = await _context.TaxTypes.Where(t => t.IsActive).ToListAsync();

                foreach (var line in InvoiceLines)
                {
                    foreach (var tax in line.InvoiceTaxes)
                    {
                        tax.TaxCategoryDescription = taxTypes.FirstOrDefault(t => t.Code == tax.TaxCategory)?.Description ?? tax.TaxCategory;
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining Invoice record to generate PDF.");
            }


            return;
        }

        public static string FormatDescription(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var lines = input
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .ToList();

            if (lines.Count == 0) return string.Empty;

            // First line bolded
            var result = $"<strong>{lines[0]}</strong>";

            // Add remaining lines as <br/> without bullets
            for (int i = 1; i < lines.Count; i++)
            {
                result += $"<br/>{lines[i]}";
            }

            return result;
        }

    }
}
