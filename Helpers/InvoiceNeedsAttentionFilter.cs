using eInvWorld.Models.InputModel;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Single source of truth for the "Needs Attention" composite invoice state, shared by the
    /// Dashboard panel and the Invoice List filter chip (Phase 1C/1D of the Finance UX Redesign)
    /// so the two can never disagree on which invoices qualify.
    /// </summary>
    public static class InvoiceNeedsAttentionFilter
    {
        /// <summary>
        /// An invoice needs attention if it is Invalid (and not a Draft), failed transmission,
        /// awaiting a reject request, or a Draft that has sat untouched for more than 3 days —
        /// unless it has been resolved (see <see cref="Unresolved"/>).
        /// An invoice can match more than one condition at once, so callers must count/select
        /// DISTINCT invoices from this predicate rather than summing per-condition counts.
        /// </summary>
        /// <param name="query">The (already tenant-scoped) invoices to filter.</param>
        /// <param name="allInvoices">
        /// Unscoped InvoiceHeaders set used only for the "Valid Resend copy exists" lookup. The copy is
        /// tenant-checked when it is saved, so it always belongs to the same user as its original.
        /// </param>
        public static IQueryable<InvoiceHeader> Apply(IQueryable<InvoiceHeader> query, IQueryable<InvoiceHeader> allInvoices)
        {
            var agingDraftCutoff = DateTime.Now.AddDays(-3);
            return Unresolved(query.Where(i =>
                (i.LHDNStatusId == "Invalid" && i.InternalStatusId != "Draft") ||
                i.InternalStatusId == "TransmissionError" ||
                i.InternalStatusId == "RequestReject" ||
                (i.InternalStatusId == "Draft" && i.CreatedDate <= agingDraftCutoff)), allInvoices);
        }

        /// <summary>
        /// Drops invoices the user has dealt with: those marked resolved, and those with a Resend copy
        /// that is now LHDN Valid. Only Invalid / reject-requested invoices can be marked resolved
        /// (drafts and transmission errors are fixed in place), so this never hides those.
        /// </summary>
        public static IQueryable<InvoiceHeader> Unresolved(IQueryable<InvoiceHeader> query, IQueryable<InvoiceHeader> allInvoices)
            => query.Where(i =>
                i.AttentionResolvedAt == null &&
                !allInvoices.Any(r => r.ResentFromInvoiceNo == i.InvoiceNo && r.LHDNStatusId == "Valid"));

        /// <summary>Whether an invoice is in a state the user may mark as resolved (Invalid or reject-requested).</summary>
        public static bool CanResolve(InvoiceHeader invoice)
            => (invoice.LHDNStatusId == "Invalid" && invoice.InternalStatusId != "Draft") ||
               invoice.InternalStatusId == "RequestReject";
    }
}
