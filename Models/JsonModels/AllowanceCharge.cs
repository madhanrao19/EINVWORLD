using Newtonsoft.Json;

namespace eInvWorld.Models.JsonModels
{
    public class AllowanceCharge
    {
        public List<ChargeIndicator> ChargeIndicator { get; set; } = new();
        public List<AllowanceChargeReason> AllowanceChargeReason { get; set; } = new();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<MultiplierFactorNumeric> MultiplierFactorNumeric { get; set; } = new();
        public List<Amount> Amount { get; set; } = new();
    }
}
