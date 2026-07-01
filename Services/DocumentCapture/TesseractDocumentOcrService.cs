using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;
using Tesseract;

namespace EINVWORLD.Services.DocumentCapture
{
    /// <summary>OCR fallback for AI Document Capture: turn a scanned (no-text-layer) PDF into text.</summary>
    public interface IDocumentOcrService
    {
        /// <summary>True only when OcrEnabled is set AND the tessdata folder exists.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Rasterizes up to <paramref name="maxPages"/> pages and OCRs them. Returns the recognised text,
        /// or an empty string when OCR is unavailable/fails (the caller then shows a graceful message).
        /// </summary>
        string OcrPdf(byte[] pdfBytes, int maxPages);
    }

    /// <summary>
    /// FOSS, on-prem OCR: PDFtoImage (PDFium, on SkiaSharp — no Ghostscript) rasterizes each scanned page,
    /// then Tesseract (Apache-2.0) recognises the text. Everything is gated behind DocumentCapture:OcrEnabled
    /// and a configured tessdata folder, and wrapped so a missing native runtime/tessdata degrades to "no
    /// text" rather than throwing — the page already handles an empty result as "couldn't read this scan".
    /// </summary>
    public sealed class TesseractDocumentOcrService : IDocumentOcrService
    {
        private readonly DocumentCaptureOptions _options;
        private readonly ILogger<TesseractDocumentOcrService> _log;

        public TesseractDocumentOcrService(DocumentCaptureOptions options, ILogger<TesseractDocumentOcrService> log)
        {
            _options = options;
            _log = log;
        }

        public bool IsAvailable =>
            _options.OcrEnabled
            && !string.IsNullOrWhiteSpace(_options.TessdataPath)
            && Directory.Exists(_options.TessdataPath);

        public string OcrPdf(byte[] pdfBytes, int maxPages)
        {
            if (!IsAvailable || pdfBytes is null || pdfBytes.Length == 0) return string.Empty;

            var language = string.IsNullOrWhiteSpace(_options.OcrLanguage) ? "eng" : _options.OcrLanguage;
            var dpi = _options.OcrDpi > 0 ? _options.OcrDpi : 300;
            var pages = Math.Max(1, maxPages);

            try
            {
                using var engine = new TesseractEngine(_options.TessdataPath, language, EngineMode.Default);
                var sb = new StringBuilder();

                // Render each page to a bitmap (PDFium), encode PNG, then OCR it.
                foreach (var bitmap in Conversion.ToImages(pdfBytes, options: new RenderOptions(Dpi: dpi)).Take(pages))
                {
                    using (bitmap)
                    using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        if (data is null) continue;
                        using var pix = Pix.LoadFromMemory(data.ToArray());
                        using var page = engine.Process(pix);
                        var text = page.GetText();
                        if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                    }
                }

                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                // Native runtime missing, bad tessdata, etc. — never break the upload flow.
                _log.LogWarning(ex, "OCR of the uploaded PDF failed (Tesseract/PDFium). Returning no text.");
                return string.Empty;
            }
        }
    }
}
