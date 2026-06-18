namespace eInvWorld.Models.InputModel
{
    public class TaxTypeSummary
    {
        public string TaxType { get; set; } = null!;
        public decimal TaxableAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TaxPercentage { get; set; }
    }
}
