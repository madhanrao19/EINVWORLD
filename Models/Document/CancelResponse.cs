namespace eInvWorld.Models.Document
{
    public class CancelResponse
    {
        public string uuid { get; set; } = null!;
        public string status { get; set; } = null!;
        public ErrorResponse error { get; set; } = null!;
    }
}
