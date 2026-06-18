using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.InputModel
{
    public class InvoiceTax
    {
        [Key]
        public int InvoiceTaxId { get; set; }  // Primary Key
        public int InvoiceLineId { get; set; }  // Foreign Key to InvoiceLine
        public string TaxCategory { get; set; } = null!;  // Tax category (e.g., Sales Tax, VAT)
        public decimal? TaxPercentage { get; set; }  // Tax percentage (e.g., 5%, 10%)
        public decimal? TaxAmount { get; set; }  // Calculated tax amount for this line
        public string? TaxExemptionReason { get; set; }

        // Navigation Property
        public virtual InvoiceLine InvoiceLine { get; set; } = null!;

        [NotMapped]
        public string TaxCategoryDescription { get; set; } = null!;
    }

}
