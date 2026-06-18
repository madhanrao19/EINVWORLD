using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Models
{
    public class InvoiceForm
    {
     
        public int Id { get; set; } // Change to int for auto-incrementing

        [Required(ErrorMessage = "The Invoice Code Number is required.")]
        [MaxLength(50, ErrorMessage = "The Invoice Code Number cannot exceed 50 characters.")]
        public string eInvoiceCodeNumber { get; set; } = null!;

        [Required(ErrorMessage = "The Invoice Type Code is required.")]
        [MaxLength(2, ErrorMessage = "The Invoice Type Code cannot exceed 2 characters.")]
        public string eInvoiceTypeCode { get; set; } = null!;



        [Required(ErrorMessage = "The Invoice Date is required.")]
        [DataType(DataType.Date)]
        public DateTime eInvoiceDate { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "The Invoice Time is required.")]
        [DataType(DataType.Time)]
        public TimeSpan eInvoiceTime { get; set; } = DateTime.UtcNow.TimeOfDay;
        // Navigation property to represent the relationship with Supplier



        // Currency Exchange Rate
        [Precision(18, 2)]
        public decimal? CurrencyExchangeRate { get; set; } // Optional, since it's applicable when currency differs

        public string SourceCurrencyCode { get; set; } = null!; // Document currency code
        public string TargetCurrencyCode { get; set; } = "MYR"; // Default target currency is MYR

        [StringLength(50)]
        public string? BillingFrequency { get; set; }

        // Billing Period Start and End Dates
        [DataType(DataType.Date)]
        public DateTime? BillingPeriodStartDate { get; set; } // Optional

        [DataType(DataType.Date)]
        public DateTime? BillingPeriodEndDate { get; set; } // Optional

      



    }
}
