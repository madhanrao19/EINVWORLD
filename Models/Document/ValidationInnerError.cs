namespace eInvWorld.Models.Document
{
    public class ValidationInnerError
    {
        public string propertyName { get; set; } = null!;

        public string propertyPath { get; set; } = null!;

        public string errorCode { get; set; } = null!;

        public string error { get; set; } = null!;

        public string errorMs { get; set; } = null!;
    }
}
