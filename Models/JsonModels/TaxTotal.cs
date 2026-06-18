namespace eInvWorld.Models.JsonModels
{
    public class TaxTotal
    {
        public List<TaxAmount> TaxAmount { get; set; } = new();
        public List<TaxSubtotal> TaxSubtotal { get; set; } = new();
    }
}
