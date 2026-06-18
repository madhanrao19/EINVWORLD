using eInvWorld.Data;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using System;
using System.Linq;

namespace EINVWORLD.Helpers
{
    public static class TinHelper
    {
        /// <summary>
        /// Returns true if the internal DocTypeCode is a self-billed document (11–14).
        /// </summary>
        public static bool IsSelfBilledDocType(string? docTypeCode)
        {
            return docTypeCode is "11" or "12" or "13" or "14";
        }

        /// <summary>
        /// The single source of truth for "whose TIN/token acts on this document": for self-billed
        /// documents (11–14) it is the Customer's TIN; otherwise the Supplier's TIN. Consolidates logic
        /// that was previously copy-pasted across the sync helpers and background workers.
        /// </summary>
        public static string? ResolveSubmitterTin(InvoiceHeader invoice)
        {
            ArgumentNullException.ThrowIfNull(invoice);
            return IsSelfBilledDocType(invoice.DocTypeCode)
                ? invoice.Customer?.TIN
                : invoice.Supplier?.TIN;
        }
        public static async Task<string> GetSubmittingTinFromSummaryAsync(DocumentSummary summary, ApplicationDbContext context)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            // For BOTH standard and self-billed invoices, the Submitter is ALWAYS the Issuer.
            // LHDN automatically swaps the TINs in their system for self-billed docs.
            return summary.issuerTin?.Trim()
                ?? throw new InvalidOperationException($"Missing issuerTin for document {summary.uuid}");
        }


    }
}
