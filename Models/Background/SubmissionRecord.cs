using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.Background
{
    /// <summary>
    /// A record of a successful LHDN document submission, keyed by a hash of the submitted payload.
    /// Used for local idempotency: if the exact same payload is submitted again within a short window
    /// (double-click, impatient retry, recurring re-run), the cached response is returned instead of
    /// hitting LHDN again — which mirrors MyInvois' own 422 DuplicateSubmission detection (identical
    /// payload within ~10 minutes) and avoids creating duplicate documents.
    /// </summary>
    public class SubmissionRecord
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Submitter TIN (intermediary/on-behalf-of), or null for a session-scoped manual submit.</summary>
        [MaxLength(50)]
        public string? Tin { get; set; }

        /// <summary>SHA-256 (hex) of the canonical submission payload.</summary>
        [MaxLength(64)]
        public string PayloadHash { get; set; } = string.Empty;

        public int DocumentCount { get; set; }

        public DateTime SubmittedAtUtc { get; set; }

        /// <summary>The LHDN response body returned for this submission (replayed on a duplicate).</summary>
        public string? ResponseContent { get; set; }
    }
}
