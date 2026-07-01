namespace EINVWORLD.Services.DocumentCapture
{
    /// <summary>Bound from the "DocumentCapture" config section. OFF by default.</summary>
    public sealed class DocumentCaptureOptions
    {
        public const string SectionName = "DocumentCapture";

        /// <summary>Master switch. Also requires the AI assistant (AIAssistant:Enabled) to be on,
        /// since capture turns the extracted text into a suggestion via the local LLM.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Reject uploads larger than this.</summary>
        public int MaxFileSizeMb { get; set; } = 10;

        /// <summary>Only read this many pages of a PDF (bounds work + keeps the LLM prompt sane).</summary>
        public int MaxPages { get; set; } = 15;

        /// <summary>
        /// When a PDF has no text layer (a scanned image), OCR it with Tesseract instead of rejecting it.
        /// OFF by default — needs the native Tesseract/PDFium runtimes deployed and a tessdata folder.
        /// </summary>
        public bool OcrEnabled { get; set; } = false;

        /// <summary>Folder holding Tesseract language files (e.g. eng.traineddata). Required when OcrEnabled.</summary>
        public string TessdataPath { get; set; } = string.Empty;

        /// <summary>Tesseract language(s), '+'-joined (e.g. "eng" or "eng+msa").</summary>
        public string OcrLanguage { get; set; } = "eng";

        /// <summary>DPI to rasterize PDF pages at before OCR. Higher = better accuracy but slower.</summary>
        public int OcrDpi { get; set; } = 300;
    }
}
