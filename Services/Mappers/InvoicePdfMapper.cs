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

        private static readonly string[] LogoExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

        /// <summary>
        /// Physical file for a stored logo path, or null. Logo files ("/api/companies/logos/x.png", or a legacy
        /// absolute server path) resolve by file name inside <paramref name="logosFolder"/>; web paths
        /// ("/assets/x.png", "~/x.png") inside <paramref name="webRoot"/>. Only image extensions, never outside
        /// either folder: LogoPath was once settable from the public lead form.
        /// </summary>
        internal static string? ResolveLogoFile(string? logoPath, string? logosFolder, string webRoot)
        {
            if (string.IsNullOrWhiteSpace(logoPath)) return null;
            var parts = logoPath.Trim().Split('\\', '/');
            if (!LogoExtensions.Contains(Path.GetExtension(parts[^1]).ToLowerInvariant())) return null;

            bool inLogosFolder = logoPath.StartsWith("/api/companies/logos/", StringComparison.OrdinalIgnoreCase)
                || Path.IsPathRooted(logoPath) && !logoPath.StartsWith('/') && !logoPath.StartsWith('\\');
            if (inLogosFolder)
                return !string.IsNullOrWhiteSpace(logosFolder) && EINVWORLD.Helpers.SafePath.TryResolve(logosFolder, out var logoFile, parts[^1]) ? logoFile : null;

            var segments = logoPath.TrimStart('~').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return EINVWORLD.Helpers.SafePath.TryResolve(webRoot, out var webFile, segments) ? webFile : null;
        }

        public async Task<PdfTemplate_v2ViewModel?> CreateViewModelAsync(string invoiceNo)
        {
            // 1. Fetch Invoice with all necessary relationships. AsNoTracking: always read the row as stored.
            // The submit pages load the invoice (tracked) before LHDN answers, then write UUID/LongId/status
            // with ExecuteUpdate; a tracked query on the same context returned that stale copy, so the
            // validated PDF went out with no UUID, no validation date and no QR (EINV100567).
            var invoice = await _context.InvoiceHeaders
                .AsNoTracking()
                .Include(i => i.Supplier).ThenInclude(s => s.Country)
                .Include(i => i.Customer).ThenInclude(c => c.Country)
                .Include(i => i.PublicCustomer).ThenInclude(pc => pc!.Country)
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
                    string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string? fullLogoPath = ResolveLogoFile(invoice.Supplier.LogoPath, _filePathConfig.CompanyLogosFolder, webRoot);

                    // Read file and convert to Base64 to bake it into the PDF HTML
                    if (fullLogoPath != null && File.Exists(fullLogoPath))
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
                        // Never hand an unresolved value to the PDF engine: it would fetch it as a URL or local file.
                        _logger.LogWarning("Logo not embedded for supplier {SupplierId}: not an image inside the logos folder or wwwroot", invoice.Supplier.PartyInfoId);
                        invoice.Supplier.LogoPath = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to embed logo for PDF: {ex.Message}");
                    invoice.Supplier.LogoPath = null;
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