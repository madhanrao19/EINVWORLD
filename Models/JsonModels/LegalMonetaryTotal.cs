namespace eInvWorld.Models.JsonModels
{
    public class LegalMonetaryTotal
    {
        public List<Amount> LineExtensionAmount { get; set; } = new();
        public List<Amount> TaxExclusiveAmount { get; set; } = new();
        public List<Amount> TaxInclusiveAmount { get; set; } = new();
        public List<Amount> AllowanceTotalAmount { get; set; } = new();
        public List<Amount> ChargeTotalAmount { get; set; } = new();
        public List<Amount> PayableAmount { get; set; } = new();
        public List<PayableRoundingAmount> PayableRoundingAmount { get; set; } = new();
    }
}
