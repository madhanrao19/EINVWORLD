namespace eInvWorld.Models.ViewModels
{
    public class DocumentDetailViewModel
    {
        public int Line { get; set; }
        public decimal? Qty { get; set; }
        public string ItemCode { get; set; } = null!;
        public string ItemDesc { get; set; } = null!;
        public string UOM { get; set; } = null!;
        public decimal? UnitPrice { get; set; }
        public decimal? AmountIncTax { get; set; }
        public decimal? AmountExclTax { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string ClassificationCode { get; set; } = null!; //MSIC Code

        // List of taxes associated with this document detail
        public List<DocumentDetailTaxViewModel> DocumentDetailTaxes { get; set; } = new List<DocumentDetailTaxViewModel>();

        public decimal Subtotal
        {
            get
            {
                return (UnitPrice ?? 0) * (Qty ?? 0) - (DiscountAmount ?? 0);
            }
        }

        // Total tax for this line item (sum of all taxes associated with this line)
        public decimal TotalTaxAmount
        {
            get
            {
                return DocumentDetailTaxes.Sum(tax => tax.TaxAmount ?? 0);
            }
        }
    }
}
