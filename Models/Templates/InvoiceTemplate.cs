using eInvWorld.Models.InputModel;
using System.ComponentModel.DataAnnotations.Schema;


namespace eInvWorld.Models.Templates
{
    public class InvoiceTemplate
    {
        public int Id { get; set; }
        public string? TemplateName { get; set; }
        public string? RefDocumentNo { get; set; }  // Reference Document Number

        public string DocTypeCode { get; set; } = null!;
        // ✅ Must match PartyInfo.PartyInfoId type (int)
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public int? PublicCustomerId { get; set; }
        public PublicCustomer? PublicCustomer { get; set; }

        [ForeignKey("SupplierId")]
        public virtual PartyInfo? Supplier { get; set; }

        [ForeignKey("CustomerId")]
        public virtual PartyInfo? Customer { get; set; }
        public string? Currency { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? ForeignCurrency { get; set; }  // Foreign Currency (if any)

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public InvoicePeriodEnum? InvoicePeriod { get; set; }

        // 🧮 Totals
        public decimal? TotalAmountIncTax { get; set; }  // Total Amount including Tax
        public decimal? TotalTaxAmount { get; set; }  // Total Tax Amount
        public decimal? TotalDiscountAmount { get; set; }  // Total Discount Amount
        public decimal? TotalAmountExclTax { get; set; }  // Total Amount excluding Tax
        public decimal? TotalPayableAmount { get; set; }  // Total Amount Payable
        public decimal? TotalNetAmount { get; set; }  // Total Net Amount

        public string? CreatedByUserId { get; set; } // For multi-tenant or audit
        public string? UpdatedByUserId { get; set; }
        public DateTime? LastUpdated { get; set; }

        public bool IsFavorite { get; set; } = false;

        // Additional fields to match InvoiceHeaderView
        public string? Notes { get; set; }
        public string? OldRegNo { get; set; }
        public string? BankAccountNo { get; set; }
        public string? BankName { get; set; }
        public string? Attention { get; set; }
        public DateTime? OriginalInvoiceDate { get; set; }
        public string? PoDoNo { get; set; }
        public string? PaymentTerms { get; set; }

        public List<InvoiceTemplateLine> InvoiceLines { get; set; } = new();
    }

    public class InvoiceTemplateLine
    {
        public int Id { get; set; }
        public int InvoiceTemplateId { get; set; }
        public string ClassificationCode { get; set; } = null!;
        public string? ItemCode { get; set; }
        public string ItemDescription { get; set; } = null!;
        public decimal? Quantity { get; set; }
        public string UnitOfMeasure { get; set; } = null!;
        public decimal? UnitPrice { get; set; }
        public decimal? Subtotal { get; set; } // ✅ Add this line
        public decimal? AmountInclTax { get; set; }  // Amount including tax
        public decimal? AmountExclTax { get; set; }  // Amount excluding tax
        public decimal? DiscountAmount { get; set; }  // Discount applied to the line

        public List<InvoiceTemplateTax> Taxes { get; set; } = new();

        /// <summary>
        /// Automatically calculates subtotal, tax, and total amounts.
        /// </summary>
        public void CalculateAmounts()
        {
            if (Quantity == null || UnitPrice == null) return;

            Subtotal = Quantity.Value * UnitPrice.Value;
            DiscountAmount ??= 0;
            AmountExclTax = Subtotal - DiscountAmount;

            // ✅ Calculate Total Tax Amount from Associated Taxes
            decimal totalTax = 0;
            foreach (var tax in Taxes)
            {
                tax.TaxAmount = (tax.TaxPercentage ?? 0) * AmountExclTax.Value / 100;
                totalTax += tax.TaxAmount.Value;
            }

            AmountInclTax = AmountExclTax + totalTax;
        }
    }

    public class InvoiceTemplateTax
    {
        public int Id { get; set; }
        public int InvoiceTemplateLineId { get; set; }
        public string TaxCategory { get; set; } = null!;
        public decimal? TaxPercentage { get; set; }
        public decimal? TaxAmount { get; set; }
        public string? TaxExemptionReason { get; set; }
    }

}
