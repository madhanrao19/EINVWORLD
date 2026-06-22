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
    }
}
