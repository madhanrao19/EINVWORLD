namespace eInvWorld.Models
{
    public class StdError
    {
        public string? status { get; set; }

        public string? statusCode { get; set; }

        public string? message { get; set; }

        public string? name { get; set; }

        public DocErrorDetail? error { get; set; }
    }
}
