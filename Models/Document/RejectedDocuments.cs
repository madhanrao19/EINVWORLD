namespace eInvWorld.Models.Document
{
    public class RejectedDocuments
    {
        public string invoiceCodeNumber { get; set; } = null!;
        public ErrorResponse error { get; set; } = null!;
    }
}
