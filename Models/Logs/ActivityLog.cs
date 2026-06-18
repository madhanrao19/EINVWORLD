using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.Logs
{
    public class ActivityLog
    {
        [Key]
        public int LogId { get; set; } // Primary Key

        [Required]
        public string InvoiceNo { get; set; } = null!; // Link to Invoice

        [Required]
        public string Action { get; set; } = null!; // Action performed (e.g., "Submitted", "Resubmitted", "Edited")

        public string? Status { get; set; } // Current status of the invoice (e.g., "Draft", "Submitted")

        public DateTime ActionDate { get; set; } // Timestamp of the action

        [Required]
        public string PerformedBy { get; set; } = null!; // User who performed the action

        public string? Notes { get; set; } // Optional comments or metadata
    }
}
