using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using eInvWorld.Models.InputModel;

namespace eInvWorld.Models.SmartCapture
{
    /// <summary>
    /// Smart Capture Stage 2 (reduced first cut): a per-company "extraction template" learned automatically
    /// from the company's own confirmed drafts — a simple majority-vote of what the user actually confirmed
    /// (doc type, currency, tax type/rate) the last several times. Used purely as an advisory hint appended
    /// to the AI prompt (EInvoiceAssistantService.SuggestInvoiceAsync); it never sets a field directly, never
    /// bypasses the review screen, and never blocks or auto-approves anything. One row per company.
    /// </summary>
    public class SmartCaptureCompanyHint
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CompanyPartyInfoId { get; set; }

        [ForeignKey(nameof(CompanyPartyInfoId))]
        public PartyInfo? CompanyPartyInfo { get; set; }

        /// <summary>Most frequently confirmed LHDN document type code, tracked with a streaming
        /// Boyer-Moore majority-vote counter (<see cref="DocTypeVotes"/>) so no history table is needed —
        /// each field converges independently to whatever the company confirms most often.</summary>
        [MaxLength(10)]
        public string? MostCommonDocTypeCode { get; set; }
        public int DocTypeVotes { get; set; }

        [MaxLength(10)]
        public string? MostCommonCurrency { get; set; }
        public int CurrencyVotes { get; set; }

        [MaxLength(20)]
        public string? MostCommonTaxType { get; set; }
        public int TaxTypeVotes { get; set; }

        public decimal? MostCommonTaxRatePercent { get; set; }
        public int TaxRateVotes { get; set; }

        /// <summary>Total confirmed drafts this hint has learned from — the hint is only surfaced to the AI
        /// once this reaches a minimum threshold (see SmartCaptureCompanyHintService), so a single early
        /// confirmation can't skew every future suggestion.</summary>
        public int SampleCount { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
