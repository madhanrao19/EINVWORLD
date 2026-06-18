using eInvWorld.Models.JsonModels;

namespace eInvWorld.Models.JsonModels
{
    public class TaxCategory
    {
        public List<ID> ID { get; set; } = new();
        //public List<Percent> Percent { get; set; }
        //public List<TaxExemptionReason> TaxExemptionReason { get; set; }
        public List<TaxScheme> TaxScheme { get; set; } = new();
        public List<TaxExemptionReason> TaxExemptionReason { get; set; } = new();
    }
}
