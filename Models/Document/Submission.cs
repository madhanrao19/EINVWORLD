namespace eInvWorld.Models.Document
{
    public class Submission
    {
        public string submissionUid { get; set; } = null!;
        public int? documentCount { get; set; }
        public DateTime? dateTimeReceived { get; set; }
        public string overallStatus { get; set; } = null!;
        public List<DocumentSummary> documentSummary { get; set; } = new();
    }
}
