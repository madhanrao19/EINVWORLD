using System;
using System.Text;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace EINVWORLD.Services.DocumentCapture
{
    /// <summary>Extracts the text layer from an uploaded document for downstream AI extraction.</summary>
    public interface IDocumentTextExtractor
    {
        /// <summary>
        /// Returns the extractable text of a PDF (up to <paramref name="maxPages"/> pages). Returns an
        /// empty string when the PDF has no text layer (e.g. a scanned image) — that case needs OCR,
        /// which is a later phase.
        /// </summary>
        string ExtractPdfText(byte[] pdfBytes, int maxPages);
    }

    /// <summary>
    /// PDF text-layer extractor built on PdfPig (UglyToad.PdfPig — MIT, pure managed, fits the FOSS-only
    /// policy). No native OCR engine: digital supplier invoices (PDFs with a real text layer) extract
    /// cleanly; scanned images return empty and are reported to the user as "needs OCR".
    /// </summary>
    public sealed class PdfDocumentTextExtractor : IDocumentTextExtractor
    {
        private readonly ILogger<PdfDocumentTextExtractor> _log;

        public PdfDocumentTextExtractor(ILogger<PdfDocumentTextExtractor> log) => _log = log;

        public string ExtractPdfText(byte[] pdfBytes, int maxPages)
        {
            if (pdfBytes is null || pdfBytes.Length == 0) return string.Empty;

            try
            {
                using var doc = PdfDocument.Open(pdfBytes);
                var pages = Math.Min(doc.NumberOfPages, Math.Max(1, maxPages));
                var sb = new StringBuilder();
                for (var i = 1; i <= pages; i++)
                {
                    var text = doc.GetPage(i).Text;
                    if (!string.IsNullOrWhiteSpace(text))
                        sb.AppendLine(text);
                }
                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to extract text from uploaded PDF.");
                return string.Empty;
            }
        }
    }
}
