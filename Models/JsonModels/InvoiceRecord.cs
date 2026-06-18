namespace eInvWorld.Models.JsonModels
{
    public class InvoiceRecord
    {
        public string ID { get; set; } = null!;
        public string IssueDate { get; set; } = null!;
        public string InvoiceTypeCode { get; set; } = null!;
        public string CurrencyCode { get; set; } = null!;
        public string SupplierTIN { get; set; } = null!;
        public string SupplierName { get; set; } = null!;
        public string SupplierAddress { get; set; } = null!;
        public string ItemCode { get; set; } = null!;
        public string ItemDescription { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

}
