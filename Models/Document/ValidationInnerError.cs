namespace eInvWorld.Models.Document
{
    public class ValidationInnerError
    {
        public string propertyName { get; set; } = null!;

        public string propertyPath { get; set; } = null!;

        public string errorCode { get; set; } = null!;

        public string error { get; set; } = null!;

        public string errorMs { get; set; } = null!;

        /// <summary>LHDN can nest inner errors more than one level deep.</summary>
        public List<ValidationInnerError>? innerError { get; set; }

        /// <summary>Fields LHDN adds later (e.g. SDK error references) — kept instead of silently dropped.</summary>
        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken>? Extra { get; set; }
    }
}
