using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using eInvWorld.Models.JsonModels;
using Newtonsoft.Json;

namespace eInvWorld.Models.InputModel
{
    public class InvoiceLine
    {
        [Key]
        public int InvoiceLineId { get; set; }  // Primary Key
        public int InvoiceHeaderId { get; set; }  // Foreign Key to InvoiceHeader (deprecated - use InvoiceHeaderInvoiceNo)
        [Required]
        public string InvoiceHeaderInvoiceNo { get; set; } = null!;  // Foreign Key to InvoiceHeader.InvoiceNo
        public int LineNumber { get; set; }  // Line Number (for order)
        public decimal? Quantity { get; set; }  // Quantity of the item
        public string? ItemCode { get; set; }  // Item Code (SKU, etc.)
        public string ItemDescription { get; set; } = null!;  // Item Description
        public string UnitOfMeasure { get; set; } = null!;  // Unit of Measurement (e.g., kg, pcs)
        public decimal? UnitPrice { get; set; }  // Unit Price
        public decimal? Subtotal { get; set; }  // Subtotal for the line
        public decimal? AmountInclTax { get; set; }  // Amount including tax
        public decimal? AmountExclTax { get; set; }  // Amount excluding tax
        public decimal? DiscountAmount { get; set; }  // Discount applied to the line
        public string ClassificationCode { get; set; } = null!;  // Classification Code (MSIC Code)

        // Navigation Properties
        [JsonIgnore] // Prevent circular reference during serialization
        public virtual InvoiceHeader InvoiceHeader { get; set; } = null!;  // Parent invoice header

        //public virtual ICollection<InvoiceTax> InvoiceTaxes { get; set; }  // Associated taxes for this line

        public virtual List<InvoiceTax> InvoiceTaxes { get; set; } = new();

        public List<AllowanceCharge>? AllowanceCharge;


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
            foreach (var tax in InvoiceTaxes)
            {
                tax.TaxAmount = (tax.TaxPercentage ?? 0) * AmountExclTax.Value / 100;
                totalTax += tax.TaxAmount.Value;
            }

            AmountInclTax = AmountExclTax + totalTax;
        }

    }
}
