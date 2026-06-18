using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.Audit
{
    public class InvoiceHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string InvoiceNo { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = null!; // e.g., "Created", "Submitted", "Validated", "EmailSent"

        [Required]
        [MaxLength(100)]
        public string PerformedBy { get; set; } = null!; // username or system

        public DateTime Timestamp { get; set; } = DateTime.Now;

        [MaxLength(1000)]
        public string? Remarks { get; set; }
    }
}
