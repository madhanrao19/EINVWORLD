namespace eInvWorld.Models.JsonModels
{
    public class PriceAmount
    {
        public decimal _ { get; set; } // The monetary amount
        public string currencyID { get; set; } = null!; // The currency (e.g., "MYR")
    }
}
