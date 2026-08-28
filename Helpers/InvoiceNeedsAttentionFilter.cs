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
        /// awaiting a reject request, or a Draft that has sat untouched for more than 3 days.
        /// An invoice can match more than one condition at once, so callers must count/select
        /// DISTINCT invoices from this predicate rather than summing per-condition counts.
        /// </summary>
        public static IQueryable<InvoiceHeader> Apply(IQueryable<InvoiceHeader> query)
        {
            var agingDraftCutoff = DateTime.Now.AddDays(-3);
            return query.Where(i =>
                (i.LHDNStatusId == "Invalid" && i.InternalStatusId != "Draft") ||
                i.InternalStatusId == "TransmissionError" ||
                i.InternalStatusId == "RequestReject" ||
                (i.InternalStatusId == "Draft" && i.CreatedDate <= agingDraftCutoff));
        }
    }
}
