namespace eInvWorld.Models.Document
{
    public class SuccessSubmit
    {
        public string submissionUID { get; set; } = null!;

        public List<AcceptedDocuments> acceptedDocuments { get; set; } = new();

        public List<RejectedDocuments> rejectedDocuments { get; set; } = new();
    }
}
