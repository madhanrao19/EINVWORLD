// File: Services/PDFGeneratorService.cs
using eInvWorld.Models;
using eInvWorld.Models.Settings;
using eInvWorld.Services.PdfRendering;
using EINVWORLD.Helpers;
using EINVWORLD.Models.ViewModels;
using EINVWORLD.Services.Mappers;
using Microsoft.Extensions.Options;

namespace eInvWorld.Services
{
    public class PDFGeneratorService : IPdfGeneratorService
    {
        private readonly ILogger<PDFGeneratorService> _logger;
        private readonly FilePathConfig _filePathConfig;
        private readonly PDFGenerationSettings _pdfSettings;
        private readonly IPdfRenderer _renderer;
        private readonly IRazorViewToStringRenderer _razorRenderer;
        private readonly InvoicePdfMapper _pdfMapper;


        public PDFGeneratorService(
            ILogger<PDFGeneratorService> logger,
            IOptions<FilePathConfig> filePathConfig,
            IOptions<PDFGenerationSettings> pdfSettings,
            IPdfRenderer renderer,
            IRazorViewToStringRenderer razorRenderer,
            InvoicePdfMapper pdfMapper)
        {
            _logger = logger;
            _filePathConfig = filePathConfig.Value;
            _pdfSettings = pdfSettings.Value;
            _renderer = renderer;
            _razorRenderer = razorRenderer;
            _pdfMapper = pdfMapper;

        }

        public async Task<string> GeneratePdfFromHtmlAsync(string invoiceNo, string htmlContent)
        {
            string pdfFolder = _filePathConfig.GeneratedPdfFolder;
            Directory.CreateDirectory(pdfFolder);

            // invoiceNo reaches here from user-facing routes/query values (e.g. the PDF-download
            // handler) — resolve it through SafePath (same guard used for resource/logo/editor
            // uploads elsewhere) instead of a raw Path.Combine, so it can never escape the PDF
            // folder via "..", a path separator, or another invalid-filename character.
            if (!SafePath.TryResolve(pdfFolder, out var pdfPath, $"{invoiceNo}.pdf"))
            {
                throw new ArgumentException($"Invalid invoice number for PDF generation: '{invoiceNo}'.", nameof(invoiceNo));
            }

            // Engine (DinkToPdf default, or Puppeteer) is selected by config and injected as IPdfRenderer.
            var pdfBytes = await _renderer.RenderHtmlToPdfAsync(htmlContent);
            await WriteFileWithRetryAsync(pdfPath, pdfBytes);

            _logger.LogInformation("✅ PDF generated ({Engine}) for {InvoiceNo} at: {PdfPath}",
                _pdfSettings.Engine, invoiceNo, pdfPath);
            return pdfPath;
        }

        public async Task<string> GeneratePdfAsync(string invoiceNo)
        {
            var viewModel = await _pdfMapper.CreateViewModelAsync(invoiceNo);

            if (viewModel == null)
            {
                _logger.LogError($"❌ ViewModel is NULL for invoice {invoiceNo}");
                throw new InvalidOperationException("ViewModel is null.");
            }

            try
            {
                // 1. Get the folder from your AppSettings and ensure it exists
                string logoFolder = _filePathConfig.CompanyLogosFolder;
                if (!Directory.Exists(logoFolder))
                {
                    Directory.CreateDirectory(logoFolder);
                }

                // 2. Target the exact logo property using your PdfTemplate_v2ViewModel structure
                string? dbLogoString = viewModel.InvoiceDetail?.Supplier?.LogoPath;

                if (!string.IsNullOrEmpty(dbLogoString) && dbLogoString.StartsWith("data:image"))
                {
                    string safeTin = viewModel.InvoiceDetail?.Supplier?.TIN ?? "UnknownTIN";
                    // Data URI is "data:image/...;base64,<data>" — split on the first comma; if absent, treat the whole string as the payload.
                    var commaParts = dbLogoString.Split(',', 2);
                    var base64Data = commaParts.Length > 1 ? commaParts[1] : commaParts[0];

                    // 3. Create a unique fingerprint based on the image size
                    int length = base64Data.Length;
                    string fingerprint = length.ToString() + "_" + base64Data.Substring(0, Math.Min(10, length)).GetHashCode();

                    string logoFileName = $"{safeTin}_logo_{fingerprint}.png";
                    string fullLogoPath = Path.Combine(logoFolder, logoFileName);

                    // 4. Only write to the hard drive if this exact version doesn't exist yet
                    if (!File.Exists(fullLogoPath))
                    {
                        byte[] imageBytes = Convert.FromBase64String(base64Data);
                        await File.WriteAllBytesAsync(fullLogoPath, imageBytes);
                        _logger.LogDebug($"[PDF] Saved NEW physical logo file: {logoFileName}");
                    }

                    // 5. Overwrite the massive Base64 string with the tiny absolute file path
                    if (viewModel.InvoiceDetail?.Supplier != null)
                    {
                        viewModel.InvoiceDetail.Supplier.LogoPath = fullLogoPath;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PDF] Failed to intercept Base64 logo. DinkToPdf may struggle to render.");
            }

            var html = await _razorRenderer.RenderViewToStringAsync<PdfTemplate_v2ViewModel>(
                "/Views/Invoices/PdfTemplate_v2.cshtml", viewModel);

            return await GeneratePdfFromHtmlAsync(invoiceNo, html);
        }

        // A same-named PDF being regenerated (e.g. reject/cancel emailing a "fresh" copy) can race a
        // still-open handle from the previous write — a virus scanner, another concurrent request for
        // the same invoice, etc. That's transient, so one short-delay retry clears it without the
        // caller (an interactive reject/cancel request) seeing a spurious failure.
        private async Task WriteFileWithRetryAsync(string path, byte[] bytes)
        {
            try
            {
                await File.WriteAllBytesAsync(path, bytes);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "PDF write to {PdfPath} hit a locked file; retrying once after a short delay.", EINVWORLD.Helpers.LogSanitizer.ForLog(path));
                await Task.Delay(500);
                await File.WriteAllBytesAsync(path, bytes);
            }
        }
    }
}