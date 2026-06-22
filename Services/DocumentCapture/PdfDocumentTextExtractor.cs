using System;
using System.Text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.Extensions.Logging;

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
    /// PDF text-layer extractor built on the already-referenced iTextSharp.LGPLv2.Core. Pure/managed —
    /// no native OCR engine. Digital supplier invoices (PDFs with a real text layer) extract cleanly;
    /// scanned images return empty and are reported to the user as "needs OCR".
    /// </summary>
    public sealed class PdfDocumentTextExtractor : IDocumentTextExtractor
    {
        private readonly ILogger<PdfDocumentTextExtractor> _log;

        public PdfDocumentTextExtractor(ILogger<PdfDocumentTextExtractor> log) => _log = log;

        public string ExtractPdfText(byte[] pdfBytes, int maxPages)
        {
            if (pdfBytes is null || pdfBytes.Length == 0) return string.Empty;

            PdfReader? reader = null;
            try
            {
                reader = new PdfReader(pdfBytes);
                var pages = Math.Min(reader.NumberOfPages, Math.Max(1, maxPages));
                var sb = new StringBuilder();
                for (var page = 1; page <= pages; page++)
                {
                    var text = PdfTextExtractor.GetTextFromPage(reader, page);
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
            finally
            {
                reader?.Close();
            }
        }
    }
}
