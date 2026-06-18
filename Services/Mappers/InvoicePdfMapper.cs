using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.InputModel;
using eInvWorld.Pages.Invoices;
using eInvWorld.Services;
using EINVWORLD.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using eInvWorld.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System;

namespace EINVWORLD.Services.Mappers
{
    public class InvoicePdfMapper
    {
        private readonly ApplicationDbContext _context;
        private readonly QRCodeGeneratorService _qrCodeService;
        private readonly DropdownHelper _dropdownHelper;
        private readonly ILogger<InvoicePdfMapper> _logger;
        private readonly IConfiguration _configuration;
        private readonly FilePathConfig _filePathConfig;

        public InvoicePdfMapper(
            ApplicationDbContext context,
            QRCodeGeneratorService qrCodeService,
            DropdownHelper dropdownHelper,
            ILogger<InvoicePdfMapper> logger,
            IConfiguration configuration,
            IOptions<FilePathConfig> filePathConfig) // Added Options for FilePathConfig
        {
            _context = context;
            _qrCodeService = qrCodeService;
            _dropdownHelper = dropdownHelper;
            _logger = logger;
            _configuration = configuration;
            _filePathConfig = filePathConfig.Value;
        }

        public async Task<PdfTemplate_v2ViewModel?> CreateViewModelAsync(string invoiceNo)
        {
            // 1. Fetch Invoice with all necessary relationships
            var invoice = await _context.InvoiceHeaders
                .Include(i => i.Supplier).ThenInclude(s => s.State)
                .Include(i => i.Supplier).ThenInclude(s => s.Country)
                .Include(i => i.Customer).ThenInclude(c => c.State)
                .Include(i => i.Customer).ThenInclude(c => c.Country)
                .Include(i => i.PublicCustomer)
                .Include(i => i.InvoiceLines).ThenInclude(il => il.InvoiceTaxes)
                .Include(i => i.LHDNStatus)
                .Include(i => i.InternalStatus)
                .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);

            if (invoice == null)
            {
                _logger.LogError($"Invoice not found for InvoiceNo: {invoiceNo}");
                return null;
            }

            var lines = invoice.InvoiceLines.ToList();

            // 2. Map Tax Descriptions for each line item
            var taxTypes = await _context.TaxTypes.Where(t => t.IsActive).ToListAsync();
            foreach (var line in lines)
            {
                foreach (var tax in line.InvoiceTaxes)
                {
                    tax.TaxCategoryDescription = taxTypes.FirstOrDefault(t => t.Code == tax.TaxCategory)?.Description ?? tax.TaxCategory;
                }
            }

            // 3. Calculate Summary Totals
            decimal totalQuantity = lines.Sum(item => item.Quantity ?? 0);
            decimal totalAmount = lines.Sum(item => (item.Quantity ?? 0) * (item.UnitPrice ?? 0));
            decimal totalDiscount = lines.Sum(item => item.DiscountAmount ?? 0);
            decimal totalInclTax = lines.Sum(item => item.AmountInclTax ?? 0);

            // 4. Handle QR Code Generation
            string qrCodeBase64 = string.Empty;
            if (!string.IsNullOrEmpty(invoice.UUID) && !string.IsNullOrEmpty(invoice.LongId))
            {
                qrCodeBase64 = _qrCodeService.GenerateQRCodeBase64(invoice.UUID, invoice.LongId);
            }

            // 5. EMBED SUPPLIER LOGO (Fix for Virtual/Physical Path Mismatch)
            if (!string.IsNullOrEmpty(invoice.Supplier?.LogoPath))
            {
                try
                {
                    string fullLogoPath = string.Empty;

                    // CASE A: Newer API-based path format (e.g., /api/companies/logos/filename.png)
                    if (invoice.Supplier.LogoPath.StartsWith("/api/companies/logos/", StringComparison.OrdinalIgnoreCase))
                    {
                        string fileName = Path.GetFileName(invoice.Supplier.LogoPath);
                        fullLogoPath = Path.Combine(_filePathConfig.CompanyLogosFolder, fileName);
                    }
                    // CASE B: Legacy path format inside wwwroot (e.g., ~/images/logo.png or /images/logo.png)
                    else
                    {
                        string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                        string cleanPath = invoice.Supplier.LogoPath.TrimStart('~', '/').Replace("/", Path.DirectorySeparatorChar.ToString());
                        fullLogoPath = Path.Combine(webRoot, cleanPath);
                    }

                    // Read file and convert to Base64 to bake it into the PDF HTML
                    if (File.Exists(fullLogoPath))
                    {
                        var logoBytes = await File.ReadAllBytesAsync(fullLogoPath);
                        string base64String = Convert.ToBase64String(logoBytes);
                        string extension = Path.GetExtension(fullLogoPath)?.ToLower().Replace(".", "") ?? "png";
                        if (extension == "jpg") extension = "jpeg";

                        // Update the model with the Data URI so the PDF engine sees it locally
                        invoice.Supplier.LogoPath = $"data:image/{extension};base64,{base64String}";
                        _logger.LogInformation($"Successfully embedded logo from: {fullLogoPath}");
                    }
                    else
                    {
                        _logger.LogWarning($"Logo file not found physically at: {fullLogoPath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to embed logo for PDF: {ex.Message}");
                }
            }

            string fullCurrencyName = "";
            if (!string.IsNullOrEmpty(invoice.Currency))
            {
                var currencyEntry = await _context.CurrencyCodes
                    .FirstOrDefaultAsync(c => c.Code == invoice.Currency);

                if (currencyEntry != null)
                {
                    fullCurrencyName = currencyEntry.Currency;
                }
            }
            var typeEntry = await _context.EInvoiceTypes
                .FirstOrDefaultAsync(t => t.Code == invoice.DocTypeCode);
            string invoiceTypeDescription = typeEntry?.Description ?? invoice.DocTypeCode;

            // 6. Return the constructed ViewModel
            return new PdfTemplate_v2ViewModel
            {
                InvoiceDetail = invoice,
                InvoiceLines = lines,
                TotalQuantity = totalQuantity,
                TotalAmount = totalAmount,
                TotalDiscount = totalDiscount,
                TotalAmountInclTax = totalInclTax,
                QRCodeBase64 = qrCodeBase64,
                FullCurrencyName = fullCurrencyName,
                InvoiceTypeDescription = invoiceTypeDescription
            };
        }
    }
}