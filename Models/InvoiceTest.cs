using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class InvoiceTest
    {
        public int Id { get; set; } // or use a different type if needed

        [Required(ErrorMessage = "The Invoice Type Code is required.")]
        [MaxLength(2, ErrorMessage = "The Invoice Type Code cannot exceed 2 characters.")]
        public string eInvoiceTypeCode { get; set; } = null!;

        [Required(ErrorMessage = "The Invoice Code Number is required.")]
        [MaxLength(50, ErrorMessage = "The Invoice Code Number cannot exceed 50 characters.")]
        public string eInvoiceCodeNumber { get; set; } = null!;

        [Required(ErrorMessage = "The Invoice Date is required.")]
        [DataType(DataType.Date)]
        public DateTime eInvoiceDate { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "The Invoice Time is required.")]
        [DataType(DataType.Time)]
        public TimeSpan eInvoiceTime { get; set; } = DateTime.UtcNow.TimeOfDay;
    }
}
