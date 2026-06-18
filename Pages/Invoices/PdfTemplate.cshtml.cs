using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using eInvWorld.Models.Document;
using eInvWorld.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using eInvWorld.Models.InputModel;
using System;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
using eInvWorld.Services;
using eInvWorld.Helpers;
using Microsoft.AspNetCore.Authorization;


namespace eInvWorld.Pages.Invoices
{
    [Authorize(Roles = "Admin,Supplier")]
    public class PdfTemplateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PdfTemplateModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly QRCodeGeneratorService _qrCodeService;
        public InvoiceHeader InvoiceDetail { get; set; } = null!;
        public List<InvoiceLine> InvoiceLines { get; set; } = new();
        public DropdownHelper DropdownHelper { get; }


        // Totals
        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalAmountInclTax { get; set; }

        public string QRCodeBase64 { get; set; } = null!;

        public PdfTemplateModel(ApplicationDbContext context, ILogger<PdfTemplateModel> logger, IConfiguration configuration, QRCodeGeneratorService qrCodeService, DropdownHelper dropdownHelper)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _qrCodeService = qrCodeService;
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
                .Include(i => i.Supplier)  //  Load Supplier (PartyInfo)
                        .ThenInclude(s => s.State)   // ✅ Load Supplier's State Name
                     .Include(i => i.Supplier)
                        .ThenInclude(s => s.Country) // ✅ Load Supplier's Country Name
                    .Include(i => i.Customer)  //  Load Buyer (PartyInfo)
                        .ThenInclude(c => c.State)   // ✅ Load Customer's State Name
                    .Include(i => i.Customer)
                        .ThenInclude(c => c.Country) // ✅ Load Customer's Country Name
                .Include(i => i.InvoiceLines)
                .ThenInclude(il => il.InvoiceTaxes) //  Load Taxes on Items
                .Include(i => i.LHDNStatus)  //  Ensure LHDNStatus is loaded
                .Include(i => i.InternalStatus)  //  Ensure InternalStatus is loaded
                .FirstOrDefaultAsync(i => i.InvoiceNo == InvoiceNo) ?? null!;

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
