using eInvWorld.Models.InputModel;

namespace eInvWorld.Models.ViewModels
{
    public class DocumentHeaderViewModel
    {
        public string DocumentNo { get; set; } = null!;
        public string RefDocumentNo { get; set; } = null!;
        public DateTime? IssueDate { get; set; }
        public string DocTypeCode { get; set; } = null!;
        public InvoicePeriodEnum? InvoicePeriod { get; set; }

        public string Currency { get; set; } = null!;
        public string ForeignCurrency { get; set; } = null!;
        public decimal? ExchangeRate { get; set; }

        public int SupplierId { get; set; }
        public int CustomerId { get; set; }

        public decimal? AmountIncTax { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? AmountExlTax { get; set; }
        public decimal? TotalPayableAmount { get; set; }
        public decimal? TotalNetAmount { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Lists to hold Supplier and Customer details
        public List<PartyInfo> Suppliers { get; set; } = new List<PartyInfo>();
        public List<PartyInfo> Customers { get; set; } = new List<PartyInfo>();

        // List of document details (items in the invoice)
        public List<DocumentDetailViewModel> DocumentDetails { get; set; } = new List<DocumentDetailViewModel>();
    }
}
