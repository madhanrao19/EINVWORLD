using eInvWorld.Models.JsonModels;
using Newtonsoft.Json;

namespace eInvWorld.Models.JsonModels
{
    public class Party
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<IndustryClassificationCode> IndustryClassificationCode { get; set; } = new();
        public List<PartyIdentification> PartyIdentification { get; set; } = new();
        public List<PostalAddress> PostalAddress { get; set; } = new();
        public List<PartyLegalEntity> PartyLegalEntity { get; set; } = new();
        public List<Contact> Contact { get; set; } = new();
    }
}
