namespace eInvWorld.Models
{
    public class EInvoiceNotificationModel
	{
        public string RecipientName { get; set; } = null!;
        public string DocumentId { get; set; } = null!;
        public string RejectionReason { get; set; } = null!;
        public string RejectedTimestamp { get; set; } = null!;
        public string InvoiceLink { get; set; } = null!;
        public string AccountLink { get; set; } = null!;
        public string LogoUrl { get; set; } = null!;
        public string? CancelLink { get; set; }
        public string ContactLink { get; set; } = null!;
        public string Year { get; internal set; } = null!;

		// ✅ Add these two:
		public string? IssueDate { get; set; }
		public string? ValidatedTimestamp { get; set; }
	}
}
