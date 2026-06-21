using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.Audit
{
    /// <summary>
    /// A tamper-evident audit record. Each row stores the hash of the previous row (<see cref="PreviousHash"/>)
    /// and a hash of its own contents chained onto it (<see cref="RowHash"/>). Recomputing the chain detects
    /// any insert, delete, or edit of historical rows — important for e-invoicing/tax compliance where the
    /// trail must be demonstrably unaltered. This table is append-only by convention (never UPDATE/DELETE).
    /// </summary>
    public class AuditLog
    {
        [Key]
        public long Id { get; set; }

        /// <summary>Groups related events from one logical operation (e.g. a submit + its response).</summary>
        [MaxLength(64)]
        public string? CorrelationId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        /// <summary>What happened, e.g. InvoiceSubmitted / DocumentCancelled / DocumentRejected / LoginSucceeded.</summary>
        [MaxLength(80)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(450)]
        public string? UserId { get; set; }

        [MaxLength(256)]
        public string? UserName { get; set; }

        [MaxLength(50)]
        public string? Tin { get; set; }

        [MaxLength(100)]
        public string? InvoiceNo { get; set; }

        [MaxLength(100)]
        public string? Uuid { get; set; }

        public string? OldValueJson { get; set; }
        public string? NewValueJson { get; set; }

        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        /// <summary>RowHash of the immediately preceding audit row (or the genesis constant for the first).</summary>
        [MaxLength(64)]
        public string PreviousHash { get; set; } = string.Empty;

        /// <summary>SHA-256 (hex) of this row's content chained onto <see cref="PreviousHash"/>.</summary>
        [MaxLength(64)]
        public string RowHash { get; set; } = string.Empty;
    }
}
