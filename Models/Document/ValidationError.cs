namespace eInvWorld.Models.Document
{
    public class ValidationError
    {
        public string propertyName { get; set; } = null!;

        public string propertyPath { get; set; } = null!;

        public string errorCode { get; set; } = null!;

        public string error { get; set; } = null!;

        public string errorMs { get; set; } = null!;

        public List<ValidationInnerError> innerError { get; set; } = null!;
    }
}
