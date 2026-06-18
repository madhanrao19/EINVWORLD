using Newtonsoft.Json;

namespace eInvWorld.Models.JsonModels
{
    public class TaxSubtotal
    {
        public List<TaxableAmount> TaxableAmount { get; set; } = new();
        public List<TaxAmount> TaxAmount { get; set; } = new();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Percent> Percent { get; set; } = new();
        public List<TaxCategory> TaxCategory { get; set; } = new();

    }
}
