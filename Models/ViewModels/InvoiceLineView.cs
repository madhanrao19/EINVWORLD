using EINVWORLD.Helpers.Validation;

namespace eInvWorld.Models.ViewModels
{
    // Individual line items in the invoice
    public class InvoiceLineView
    {
        public int LineNumber { get; set; }
        [MaxDecimalPlaces(6, ErrorMessage = "Quantity allows at most 6 decimal places.")]
        public decimal? Quantity { get; set; }
        public string? ItemCode { get; set; }
        public string ItemDescription { get; set; } = null!;
        public string UnitOfMeasure { get; set; } = null!;
        [MaxDecimalPlaces(4, ErrorMessage = "Unit price allows at most 4 decimal places.")]
        public decimal? UnitPrice { get; set; }
        public decimal? Subtotal { get; set; }
        public decimal? AmountInclTax { get; set; }
        public decimal? AmountExclTax { get; set; }
        [MaxDecimalPlaces(2, ErrorMessage = "Discount allows at most 2 decimal places.")]
        public decimal? DiscountAmount { get; set; }
        public string ClassificationCode { get; set; } = null!; // MSIC Code

        // List of taxes applied to this line
        public List<InvoiceTaxView> Taxes { get; set; } = new List<InvoiceTaxView>();

        /// <summary>
        /// ✅ Method to Calculate Amounts (Subtotal, Tax, and Total)
        /// </summary>
        public void CalculateAmounts()
        {
            if (Quantity == null || UnitPrice == null) return;

            Subtotal = Quantity.Value * UnitPrice.Value;
            DiscountAmount ??= 0;
            AmountExclTax = Subtotal - DiscountAmount;

            // ✅ Calculate Tax Amounts for each tax category
            decimal totalTax = 0;
            foreach (var tax in Taxes)
            {
                tax.TaxAmount = (tax.TaxPercentage ?? 0) * AmountExclTax.Value / 100;
                totalTax += tax.TaxAmount ?? 0;
            }

            AmountInclTax = AmountExclTax + totalTax;
        }
    }
}
