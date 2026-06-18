using Newtonsoft.Json;

namespace eInvWorld.Models.JsonModels
{
    public class PayableRoundingAmount
    {
        public decimal? _ { get; set; }
        public string currencyID { get; set; } = null!;
    }
}
