using Newtonsoft.Json;

namespace eInvWorld.Models.JsonModels
{
    public class ID
    {
        [JsonProperty(PropertyName = "_")]
        public string _ { get; set; } = null!;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? schemeID { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? schemeAgencyID { get; set; }

    }
}
