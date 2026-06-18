namespace eInvWorld.Models.ViewModels
{
    public class InvoiceByCustomerSummary
    {
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public int Year { get; set; }
        public string? Currency { get; set; }
        public string? Customer { get; set; }
        public int Count { get; set; }
    }

    public class InvoiceKpiSummary
    {
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public int Year { get; set; }
        public string? Currency { get; set; }
        public int TotalInvoices { get; set; }
        public int Received { get; set; }
        public int Valid { get; set; }
        public int Rejected { get; set; }
        public int Invalid { get; set; }
        public int Expired { get; set; }
    }

    public class InvoiceTopProduct
    {
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public int Year { get; set; }
        public string? Currency { get; set; }
        public string? Product { get; set; }
        public int Count { get; set; }
    }

    public class InvoiceRejectedReason
    {
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public int Year { get; set; }
        public string? Currency { get; set; }
        public string? Reason { get; set; }
        public int Count { get; set; }
    }

    public class InvoiceTypeBreakdown
    {
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public int Year { get; set; }
        public string? Currency { get; set; }
        public string? Type { get; set; }
        public int Count { get; set; }
    }

    public class InvoiceMonthlySummary
    {
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public string? Month { get; set; }
        public string? Currency { get; set; }
        public int Count { get; set; }
        public decimal? TotalAmount { get; set; }
    }

    public class InvoiceTaxSummary
    {
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public string? MonthName { get; set; }
        public int Year { get; set; }
        public decimal SST2 { get; set; }
        public decimal SST3 { get; set; }
        public decimal NonSST { get; set; }
        public decimal Amount { get; set; }
    }
}