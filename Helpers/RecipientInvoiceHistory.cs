using System;
using System.Collections.Generic;
using System.Linq;
using eInvWorld.Models.Audit;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// What an invoice's RECIPIENT may see of its activity history: the document's LHDN lifecycle only (submitted,
    /// status, validated, cancelled, rejected), never the issuer's internal records — staff usernames, edits, drafts,
    /// emails, PDFs, submission errors, attention notes. An allowlist, so a new internal action stays hidden by default.
    /// The issuer and Admins get the full history (<see cref="UserExtensions.IsIssuerOfInvoiceAsync"/>).
    /// </summary>
    public static class RecipientInvoiceHistory
    {
        public const string Issuer = "Issuer";
        public const string Lhdn = "LHDN MyInvois";

        // Action -> who it is shown as performed by (null: keep it, the recipient's own action) and whether the remarks
        // are public lifecycle facts (statuses, LHDN dates, the reasons LHDN itself shows both parties).
        private static readonly Dictionary<string, (string? By, bool Remarks)> Visible = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Submitted"] = (Issuer, false),          // remarks name the internal submission id
            ["StatusSync"] = (Lhdn, true),            // "Updated status: Valid"
            ["DateTimeValidated"] = (Lhdn, true),
            ["Cancelled"] = (Issuer, true),           // the cancellation reason, also sent to LHDN
            ["CancelDateTime"] = (Lhdn, true),
            ["CancelledReason"] = (Lhdn, true),
            ["RejectedTimestamp"] = (Lhdn, true),
            ["RejectedReason"] = (Lhdn, true),
            ["RequestReject"] = (null, true),         // the recipient's own rejection request
        };

        /// <summary>The recipient's view of <paramref name="history"/>: allowlisted actions only, performer replaced by
        /// <see cref="Issuer"/>/<see cref="Lhdn"/>, internal remarks dropped. Order is preserved; inputs are not modified.</summary>
        public static List<InvoiceHistory> For(IEnumerable<InvoiceHistory> history) =>
            history.Where(h => Visible.ContainsKey(h.Action))
                .Select(h =>
                {
                    var rule = Visible[h.Action];
                    return new InvoiceHistory
                    {
                        Id = h.Id,
                        InvoiceNo = h.InvoiceNo,
                        Action = h.Action,
                        Timestamp = h.Timestamp,
                        PerformedBy = rule.By ?? h.PerformedBy,
                        Remarks = rule.Remarks ? h.Remarks : null
                    };
                })
                .ToList();
    }
}
