using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.ViewModels
{
    // Tax information for an individual line or invoice
    public class InvoiceTaxView
    {
        public string TaxCategory { get; set; } = null!;  // e.g., Sales Tax, VAT, etc.
        public decimal? TaxPercentage { get; set; }  // e.g., 5%, 10%, etc.
        public decimal? TaxAmount { get; set; }
        
        [RequiredIfTaxCategoryIsE]
        public string? TaxExemptionReason { get; set; }

        public class RequiredIfTaxCategoryIsE : ValidationAttribute
        {
            protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
            {
                var taxView = (InvoiceTaxView)validationContext.ObjectInstance;

                if (taxView.TaxCategory == "E" && string.IsNullOrWhiteSpace(taxView.TaxExemptionReason))
                {
                    return new ValidationResult("Tax exemption reason is required when Tax Category is 'E'.");
                }

                return ValidationResult.Success;
            }
        }

    }
}
