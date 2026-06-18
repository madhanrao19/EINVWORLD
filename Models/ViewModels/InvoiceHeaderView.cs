using eInvWorld.Models.InputModel;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.ViewModels
{
    // Header information for the invoice
    public class InvoiceHeaderView
    {
        public string? InvoiceNo { get; set; }
        public string? RefDocumentNo { get; set; }
        public DateTime? IssueDate { get; set; }
        
        // Format for Flatpickr (Y-m-d H:i)
        public string IssueDateFormatted => IssueDate?.ToString("yyyy-MM-dd HH:mm") ?? "";
        public string DocTypeCode { get; set; } = null!;

        public string? UUID { get; set; }
        public InvoicePeriodEnum? InvoicePeriod { get; set; }
        public string Currency { get; set; } = null!;
        public string ForeignCurrency { get; set; } = null!;
        public decimal? ExchangeRate { get; set; }

        public int SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public int? PublicCustomerId { get; set; }

        public decimal? TotalAmountIncTax { get; set; }
        public decimal? TotalTaxAmount { get; set; }
        public decimal? TotalDiscountAmount { get; set; }
        public decimal? TotalAmountExclTax { get; set; }
        public decimal? TotalPayableAmount { get; set; }
        public decimal? TotalNetAmount { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Dropdown data for selecting Supplier and Customer
        public List<PartyInfo> Suppliers { get; set; } = new List<PartyInfo>();
        public List<PartyInfo> Customers { get; set; } = new List<PartyInfo>();

        // Invoice lines and their associated taxes
        public List<InvoiceLineView> InvoiceLines { get; set; } = new List<InvoiceLineView>();

        public string? RefUUID { get; set; }

        // ✅ Add this for template loading
        public string? TemplateName { get; set; }

        public string? Notes { get; set; }

        // Additional Party Information Fields
        public string? OldRegNo { get; set; }
        public string? BankAccountNo { get; set; }
        public string? BankName { get; set; }
        public string? Attention { get; set; }

        [StringLength(3, ErrorMessage = "Incoterms must be exactly 3 characters.")]
        public string? Incoterms { get; set; }

        [StringLength(150, ErrorMessage = "Prepayment Reference Number cannot exceed 150 characters.")]
        public string? PrepaymentReferenceNumber { get; set; }
        public DateTime? OriginalInvoiceDate { get; set; } // Date Invoice (original date)
        public string? PoDoNo { get; set; } // PO/DO No

        [MaxLength(200)]
        [Display(Name = "Payment Terms")]
        public string? PaymentTerms { get; set; }

        /// <summary>
        /// Calculate all invoice totals from line items
        /// </summary>
        public void CalculateInvoiceTotals()
        {
            if (InvoiceLines == null || !InvoiceLines.Any())
            {
                TotalAmountExclTax = 0;
                TotalTaxAmount = 0;
                TotalAmountIncTax = 0;
                TotalDiscountAmount = 0;
                TotalPayableAmount = 0;
                TotalNetAmount = 0;
                return;
            }

            // Ensure all line amounts are calculated first
            foreach (var line in InvoiceLines)
            {
                line.CalculateAmounts();
            }

            // Calculate header totals from line items
            TotalAmountExclTax = InvoiceLines.Sum(l => l.AmountExclTax ?? 0);
            TotalTaxAmount = InvoiceLines.Sum(l => l.Taxes?.Sum(t => t.TaxAmount ?? 0) ?? 0);
            TotalAmountIncTax = InvoiceLines.Sum(l => l.AmountInclTax ?? 0);
            TotalDiscountAmount = InvoiceLines.Sum(l => l.DiscountAmount ?? 0);
            TotalPayableAmount = TotalAmountIncTax;
            TotalNetAmount = TotalAmountIncTax;
        }

    }
}
