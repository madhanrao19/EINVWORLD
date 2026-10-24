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
        /// Classification code given to an item line left blank: 022 (Others), or for self-billed documents
        /// 035 when the supplier is the foreign-supplier general TIN and 004 otherwise. Shared by Create,
        /// Edit and recurring generation so the default can't drift between them.
        /// </summary>
        public static string DefaultClassificationCode(string? docTypeCode, string? supplierTin) =>
            !IsSelfBilledDocType(docTypeCode) ? "022"
            : supplierTin == "EI00000000030" ? "035" : "004";

        /// <summary>Credit, debit and refund notes (and their self-billed versions), which must reference an original invoice's UUID.</summary>
        public static bool IsAdjustmentDocType(string? docTypeCode) =>
            docTypeCode is "02" or "03" or "04" or "12" or "13" or "14";

        private static readonly string[] ForeignSupplierClassifications =
            { "010", "011", "033", "034", "035", "036", "037", "038", "039", "040", "041", "045" };

        /// <summary>
        /// LHDN rule for self-billed documents whose supplier is a general TIN: General Public (EI00000000010) needs
        /// classification 004, Foreign Supplier (EI00000000030) one of 010, 011, 033-041, 045. Null when the code is allowed.
        /// </summary>
        public static string? SelfBilledClassificationError(string? docTypeCode, string? supplierTin, string? classificationCode)
        {
            if (!IsSelfBilledDocType(docTypeCode)) return null;
            if (supplierTin == "EI00000000010" && classificationCode != "004")
                return "For self-billed with supplier: General Public, classification code must be 004.";
            if (supplierTin == "EI00000000030" && !ForeignSupplierClassifications.Contains(classificationCode))
                return "For self-billed with supplier: Foreign Supplier, classification code must be one of: " + string.Join(", ", ForeignSupplierClassifications) + ".";
            return null;
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
