using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Helpers
{
    /// <summary>The fields that make two invoices look like the same bill.</summary>
    public sealed record DuplicateKey(
        int? SupplierId, int? CustomerId, int? PublicCustomerId, string? DocTypeCode, string? Currency,
        decimal? TotalPayable, string? PoDoNo, string? RefDocumentNo, DateTime? IssueDate);

    /// <summary>An existing invoice that may be the same bill.</summary>
    public sealed record DuplicateMatch(string InvoiceNo, string? Uuid, string? Status, DateTime? IssueDate, decimal? Total, string Reason);

    /// <summary>
    /// Finds probable duplicates: same supplier, buyer, document type, currency and total (±0.01), plus the same PO/DO
    /// number, the same reference number or the same issue date. Only ever used to WARN (legitimate repeats exist);
    /// cancelled and LHDN-invalid invoices are ignored. Recurring generation is deliberately not checked: every run
    /// repeats the template's PO/reference and total by design.
    /// </summary>
    public static class DuplicateInvoiceFinder
    {
        public const int MaxMatches = 3;

        public static async Task<List<DuplicateMatch>> FindAsync(ApplicationDbContext db, DuplicateKey key, string? excludeInvoiceNo, CancellationToken ct = default)
        {
            if (key.SupplierId is null or 0 || key.TotalPayable is null || (key.CustomerId is null or 0 && key.PublicCustomerId is null or 0))
                return new();

            var po = Blank(key.PoDoNo) ? null : key.PoDoNo!.Trim();
            var reference = Blank(key.RefDocumentNo) ? null : key.RefDocumentNo!.Trim();
            var day = key.IssueDate?.Date;
            var low = key.TotalPayable.Value - 0.01m;
            var high = key.TotalPayable.Value + 0.01m;

            var query = db.InvoiceHeaders.AsNoTracking().Where(i => i.SupplierId == key.SupplierId);
            query = key.CustomerId > 0
                ? query.Where(i => i.CustomerId == key.CustomerId)
                : query.Where(i => i.PublicCustomerId == key.PublicCustomerId);
            var candidates = await query
                .Where(i => i.DocTypeCode == key.DocTypeCode
                    && i.Currency == key.Currency
                    && i.TotalPayableAmount >= low && i.TotalPayableAmount <= high
                    && i.InvoiceNo != excludeInvoiceNo
                    && i.InternalStatusId != "Cancelled" && i.LHDNStatusId != "Cancelled" && i.LHDNStatusId != "Invalid"
                    && ((po != null && i.PoDoNo == po)
                        || (reference != null && i.RefDocumentNo == reference)
                        || (day != null && i.IssueDate >= day && i.IssueDate < day.Value.AddDays(1))))
                .OrderByDescending(i => i.CreatedDate)
                .Take(MaxMatches)
                .Select(i => new { i.InvoiceNo, i.UUID, i.LHDNStatusId, i.InternalStatusId, i.IssueDate, i.TotalPayableAmount, i.PoDoNo, i.RefDocumentNo })
                .ToListAsync(ct);

            return candidates.Select(c => new DuplicateMatch(
                c.InvoiceNo, c.UUID, c.LHDNStatusId ?? c.InternalStatusId, c.IssueDate, c.TotalPayableAmount,
                po != null && c.PoDoNo == po ? "PO/DO number"
                : reference != null && c.RefDocumentNo == reference ? "reference number"
                : "issue date")).ToList();
        }

        /// <summary>Probable duplicates of a saved invoice (Create / Edit after Save Draft).</summary>
        public static async Task<List<DuplicateMatch>> FindForSavedAsync(ApplicationDbContext db, string invoiceNo, CancellationToken ct = default)
        {
            var key = await db.InvoiceHeaders.AsNoTracking()
                .Where(i => i.InvoiceNo == invoiceNo)
                .Select(i => new DuplicateKey(i.SupplierId, i.CustomerId, i.PublicCustomerId, i.DocTypeCode, i.Currency,
                    i.TotalPayableAmount, i.PoDoNo, i.RefDocumentNo, i.IssueDate))
                .FirstOrDefaultAsync(ct);
            return key == null ? new() : await FindAsync(db, key, invoiceNo, ct);
        }

        /// <summary>
        /// Import preview: an existing invoice between the same two parties carrying the same reference number (the CSV's
        /// own invoice number is stored in RefDocumentNo), i.e. the same file or row imported again. Totals are not compared
        /// because the preview total isn't computed exactly like a saved invoice's.
        /// </summary>
        public static async Task<string?> FindImportedAsync(ApplicationDbContext db, int? supplierId, int? customerId, int? publicCustomerId, string? reference, CancellationToken ct = default)
        {
            if (supplierId is null or 0 || Blank(reference) || (customerId is null or 0 && publicCustomerId is null or 0)) return null;
            var r = reference!.Trim();
            var query = db.InvoiceHeaders.AsNoTracking().Where(i => i.SupplierId == supplierId && i.RefDocumentNo == r
                && i.InternalStatusId != "Cancelled" && i.LHDNStatusId != "Cancelled" && i.LHDNStatusId != "Invalid");
            query = customerId > 0 ? query.Where(i => i.CustomerId == customerId) : query.Where(i => i.PublicCustomerId == publicCustomerId);
            return await query.OrderByDescending(i => i.CreatedDate).Select(i => i.InvoiceNo).FirstOrDefaultAsync(ct);
        }

        /// <summary>One plain-text warning line per match, for save responses and import previews.</summary>
        public static List<string> Warnings(IEnumerable<DuplicateMatch> matches) =>
            matches.Select(m => $"Possible duplicate: {m.InvoiceNo} has the same buyer, total and {m.Reason}.").ToList();

        private static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
    }
}
