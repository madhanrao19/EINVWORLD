namespace eInvWorld.Models.ViewModels
{
    public class DocumentDetailTaxViewModel
    {
        public string TaxCategory { get; set; } = null!;  // e.g., Sales Tax, VAT, etc.
        public decimal? TaxPercentage { get; set; }  // e.g., 5%, 10%, etc.
        public decimal? TaxAmount { get; set; }

        // Calculate the tax based on the amount excluding tax (optional)
        public decimal CalculateTax(decimal amountExclTax)
        {
            if (TaxPercentage.HasValue && amountExclTax > 0)
            {
                return (amountExclTax * TaxPercentage.Value) / 100;
            }
            return 0;
        }
    }
}
