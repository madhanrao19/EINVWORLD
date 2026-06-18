using Newtonsoft.Json;

namespace eInvWorld.Models.JsonModels
{
    public class IdentificationCode
    {
        public string _ { get; set; } = null!;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string listID { get; set; } = null!;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string listAgencyID { get; set; } = null!;
    }
}
