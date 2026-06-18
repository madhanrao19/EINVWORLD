namespace eInvWorld.Models
{
    public class InvoiceLineItem
    {

        public string ClassificationCode { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Quantity { get; set; }
        public string Measurement { get; set; } = null!;
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; } // This could be calculated based on Quantity * UnitPrice
        public string TaxCategory { get; set; } = null!;
        public decimal TaxPercentage { get; set; }
        public decimal TaxableAmount { get; set; } // This could be calculated as Subtotal
        public decimal TotalAmount { get; set; } // This should be Subtotal + TaxableAmount
    }
}
