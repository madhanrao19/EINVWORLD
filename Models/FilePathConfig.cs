namespace eInvWorld.Models
{
    public class FilePathConfig
    {
        public string BasePath { get; set; } = null!;
        public string DraftFolder { get; set; } = null!;
        public string SubmittedFolder { get; set; } = null!;
        public string ValidFolder { get; set; } = null!;
        public string InvalidFolder { get; set; } = null!;
        public string InvoiceCounterFilePath { get; set; } = null!;
        public string CancelledFolder { get; set; } = null!;
		public string GeneratedPdfFolder { get; set; } = null!;
        public string ResourceImagesFolder { get; set; } = null!;
        public string EditorUploadsFolder { get; set; } = null!;
        public string CompanyLogosFolder { get; set; } = null!;
	}
}
