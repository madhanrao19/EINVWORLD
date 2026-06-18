namespace eInvWorld.Models
{
    public class ErrorDetail
    {
        public object? code { get; set; }
        public string? message { get; set; }
        public string? target { get; set; }
        public object? propertyPath { get; set; }
        public object? details { get; set; }
    }
}
