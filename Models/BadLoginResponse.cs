namespace eInvWorld.Models
{
    public class BadLoginResponse
    {
        public string error { get; set; } = null!;
        public string error_description { get; set; } = null!;
        public string error_uri { get; set; } = null!;
    }
}
