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

        /// <summary>Fields LHDN adds later (e.g. SDK error references) — kept instead of silently dropped.</summary>
        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken>? Extra { get; set; }
    }
}
