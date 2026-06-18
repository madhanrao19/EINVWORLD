namespace eInvWorld.Models.JsonModels
{
    public class TaxExchangeRate
    {
        public List<SourceCurrencyCode> SourceCurrencyCode { get; set; } = new();
        public List<TargetCurrencyCode> TargetCurrencyCode { get; set; } = new();
        public List<CalculationRate> CalculationRate { get; set; } = new();
    }
}
