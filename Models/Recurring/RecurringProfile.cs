using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.Recurring
{
    public class RecurringProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ProfileName { get; set; } = string.Empty;

        // Links to your existing Template table
        [Required]
        public int InvoiceTemplateId { get; set; }

        // Core identifiers (Mapped to match the template's logic)
        [Required]
        public int SupplierId { get; set; }

        public int? CustomerId { get; set; }
        public int? PublicCustomerId { get; set; }

        [Required]
        public string Frequency { get; set; } = "Monthly"; // Options: Daily, Weekly, Monthly, Annually

        [Required]
        public DateTime NextRunDate { get; set; }

        public bool AutoSubmitToMyInvois { get; set; }

        public string Status { get; set; } = "Active"; // Options: Active, Paused

        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property back to your existing template
        [ForeignKey("InvoiceTemplateId")]
        public virtual Templates.InvoiceTemplate? InvoiceTemplate { get; set; }
    }
}