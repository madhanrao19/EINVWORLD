namespace eInvWorld.Models
{
    public class ErrorResponse
    {
        public string code { get; set; } = null!;
        public string message { get; set; } = null!;
        public string target { get; set; } = null!;
        public List<ErrorDetail> details { get; set; } = new();
    }
}
