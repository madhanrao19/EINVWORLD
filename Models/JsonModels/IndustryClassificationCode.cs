using Newtonsoft.Json;

namespace eInvWorld.Models.JsonModels
{
    public class IndustryClassificationCode
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string _ { get; set; } = null!;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string name { get; set; } = null!;
    }
}
