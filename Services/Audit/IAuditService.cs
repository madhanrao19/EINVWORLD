using System.Threading;
using System.Threading.Tasks;

namespace EINVWORLD.Services.Audit
{
    /// <summary>Details for one audit entry. All fields optional except the action (passed separately).</summary>
    public sealed class AuditEntry
    {
        public string? CorrelationId { get; set; }
        public string? Tin { get; set; }
        public string? InvoiceNo { get; set; }
        public string? Uuid { get; set; }
        public string? OldValueJson { get; set; }
        public string? NewValueJson { get; set; }
        /// <summary>Override the user (else taken from the current HttpContext). Used by background jobs.</summary>
        public string? UserNameOverride { get; set; }
    }

    public sealed record AuditVerificationResult(bool Ok, long RowsChecked, long? FirstBrokenId, string Message);

    /// <summary>Appends to the tamper-evident audit chain and verifies its integrity.</summary>
    public interface IAuditService
    {
        /// <summary>Appends an audit row, chaining its hash onto the previous row. Never throws to the caller.</summary>
        Task WriteAsync(string action, AuditEntry entry, CancellationToken ct = default);

        /// <summary>Recomputes the whole chain and reports the first row (if any) that fails verification.</summary>
        Task<AuditVerificationResult> VerifyChainAsync(CancellationToken ct = default);
    }
}
