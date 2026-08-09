using eInvWorld.Data;
using eInvWorld.Models.SmartCapture;
using EINVWORLD.Services.Assistant;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Services.SmartCapture
{
    /// <summary>
    /// Smart Capture Stage 2 (reduced first cut): learns a per-company extraction hint from each confirmed
    /// draft using a streaming Boyer-Moore majority-vote counter per field (no history table needed), and
    /// exposes it — above a minimum sample threshold — as advisory-only context for the AI suggestion
    /// prompt. Never writes to the InvoiceHeader/draft path; never blocks or auto-approves anything.
    /// </summary>
    public sealed class SmartCaptureCompanyHintService
    {
        /// <summary>Below this many confirmed drafts, a single early confirmation could dominate the vote —
        /// don't surface the hint to the AI yet.</summary>
        private const int MinSamplesToSurface = 3;

        private readonly ApplicationDbContext _context;

        public SmartCaptureCompanyHintService(ApplicationDbContext context) => _context = context;

        /// <summary>Loads the company's hints for use in an AI prompt, or null if none exist yet / not
        /// enough samples have been confirmed to trust the majority vote.</summary>
        public async Task<CompanyCaptureHints?> GetAsync(int companyPartyInfoId, CancellationToken ct)
        {
            var hint = await _context.SmartCaptureCompanyHints
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.CompanyPartyInfoId == companyPartyInfoId, ct);

            if (hint is null || hint.SampleCount < MinSamplesToSurface) return null;

            return new CompanyCaptureHints(
                hint.MostCommonDocTypeCode, hint.MostCommonCurrency, hint.MostCommonTaxType, hint.MostCommonTaxRatePercent);
        }

        /// <summary>Records one confirmed draft's field values into the company's rolling majority vote.
        /// Called once, right after a draft is successfully created — never before, so a document that
        /// fails or is abandoned never influences future suggestions. <paramref name="currency"/> and
        /// <paramref name="taxType"/> originate from the AI/OCR suggestion (free text, not an allowlisted
        /// field like the confirmed doc type) — truncated to the column length here, at the single choke
        /// point, so a malformed value from a document's content can never throw and abort an otherwise-
        /// successful Confirm postback (the draft has already been created by the time this runs).</summary>
        public async Task RecordConfirmedAsync(
            int companyPartyInfoId, string? docTypeCode, string? currency, string? taxType, decimal? taxRatePercent, CancellationToken ct)
        {
            var hint = await _context.SmartCaptureCompanyHints
                .FirstOrDefaultAsync(h => h.CompanyPartyInfoId == companyPartyInfoId, ct);

            if (hint is null)
            {
                hint = new SmartCaptureCompanyHint { CompanyPartyInfoId = companyPartyInfoId };
                _context.SmartCaptureCompanyHints.Add(hint);
            }

            (hint.MostCommonDocTypeCode, hint.DocTypeVotes) = Vote(hint.MostCommonDocTypeCode, hint.DocTypeVotes, Truncate(docTypeCode, 10));
            (hint.MostCommonCurrency, hint.CurrencyVotes) = Vote(hint.MostCommonCurrency, hint.CurrencyVotes, Truncate(currency, 10));
            (hint.MostCommonTaxType, hint.TaxTypeVotes) = Vote(hint.MostCommonTaxType, hint.TaxTypeVotes, Truncate(taxType, 20));
            (hint.MostCommonTaxRatePercent, hint.TaxRateVotes) = VoteDecimal(hint.MostCommonTaxRatePercent, hint.TaxRateVotes, taxRatePercent);

            hint.SampleCount++;
            hint.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }

        private static string? Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) ? value : value.Length <= maxLength ? value : value[..maxLength];

        /// <summary>Classic streaming Boyer-Moore majority-vote update: agree -> increment; disagree ->
        /// decrement, and once the count hits zero the new value takes over. Converges to whichever value
        /// is confirmed most often without storing per-confirmation history.</summary>
        private static (string? Current, int Votes) Vote(string? current, int votes, string? incoming)
        {
            if (string.IsNullOrWhiteSpace(incoming)) return (current, votes);
            incoming = incoming.Trim();

            if (votes <= 0 || current is null)
                return (incoming, 1);
            if (string.Equals(current, incoming, StringComparison.OrdinalIgnoreCase))
                return (current, votes + 1);

            votes--;
            return votes <= 0 ? (incoming, 1) : (current, votes);
        }

        private static (decimal? Current, int Votes) VoteDecimal(decimal? current, int votes, decimal? incoming)
        {
            if (incoming is null) return (current, votes);

            if (votes <= 0 || current is null)
                return (incoming, 1);
            if (current.Value == incoming.Value)
                return (current, votes + 1);

            votes--;
            return votes <= 0 ? (incoming, 1) : (current, votes);
        }
    }
}
