using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.InputModel
{
    public class TaxSummary
    {
        [Key]
        public int TaxSummaryId { get; set; }
        public int DocumentHeaderId { get; set; } // Link back to DocumentHeader
        public decimal TotalTaxableAmount { get; set; } // Sum of taxable amounts
        public decimal TotalTaxAmount { get; set; } // Sum of tax amounts
        public decimal TotalTax { get; set; } // Sum of all tax amounts for the document

        // Additional fields can be added as needed, such as total tax per tax type
        //public List<TaxTypeSummary> TaxTypeSummaries { get; set; }
    }
}
