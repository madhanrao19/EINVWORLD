using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Atomic guard that prevents the same invoice being submitted to LHDN twice (double-click / two
    /// tabs / retries). Implemented as a DB compare-and-set on InvoiceHeaders.SubmissionClaimedAtUtc, so
    /// exactly one concurrent request wins the claim and proceeds; the others are blocked. A claim older
    /// than <see cref="StaleAfter"/> is considered abandoned (e.g. a crashed submit) and can be reclaimed,
    /// so a missed release only delays a retry — it never locks an invoice permanently.
    /// </summary>
    public static class InvoiceSubmissionGuard
    {
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Atomically claims the invoice for submission. Returns true only for the single request that
        /// wins. Returns false if the invoice already has a UUID (already submitted) or another request
        /// holds a fresh claim.
        /// <para>
        /// Side effect on a winning claim: any <see cref="InvoiceHeader"/> for this invoice already
        /// tracked by <paramref name="db"/> is reloaded from the database. The claim UPDATE bumps the
        /// row's SQL Server rowversion, so a tracked entity loaded before the claim holds a stale
        /// <c>RowVersion</c> and its next SaveChanges would always fail with
        /// DbUpdateConcurrencyException. Callers must therefore apply their post-submission mutations
        /// AFTER claiming — unsaved changes made before the claim are discarded by the reload.
        /// </para>
        /// </summary>
        public static async Task<bool> TryClaimAsync(ApplicationDbContext db, string invoiceNo, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo)) return false;

            var now = DateTime.UtcNow;
            var staleBefore = now - StaleAfter;

            var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [InvoiceHeaders]
SET [SubmissionClaimedAtUtc] = {now}
WHERE [InvoiceNo] = {invoiceNo}
  AND ([UUID] IS NULL OR [UUID] = '')
  AND ([SubmissionClaimedAtUtc] IS NULL OR [SubmissionClaimedAtUtc] < {staleBefore})", ct);

            if (affected <= 0) return false;

            // Refresh the concurrency token of the tracked InvoiceHeader(s) so the caller's
            // post-submission SaveChanges succeeds (see XML doc above): the claim UPDATE bumped the row's
            // rowversion, so any instance loaded before the claim holds a stale RowVersion and its next
            // SaveChanges would always throw DbUpdateConcurrencyException.
            //
            // A submission flow only ever tracks the single invoice being submitted, so reload EVERY
            // tracked InvoiceHeader. The previous InvoiceNo string match was brittle — a CHAR/padded or
            // otherwise non-identical stored value would fail the exact-string comparison, silently skip
            // the reload, and let the stale token surface as a false concurrency conflict on submit.
            var tracked = db.ChangeTracker.Entries<InvoiceHeader>().ToList();
            foreach (var entry in tracked)
            {
                await entry.ReloadAsync(ct);
            }

            return true;
        }

        /// <summary>
        /// Releases the claim so the invoice can be submitted again — but only while it is still
        /// unsubmitted (no UUID). Call on any failure path after a successful claim. Best-effort.
        /// </summary>
        public static async Task ReleaseAsync(ApplicationDbContext db, string invoiceNo, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo)) return;
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [InvoiceHeaders]
SET [SubmissionClaimedAtUtc] = NULL
WHERE [InvoiceNo] = {invoiceNo}
  AND ([UUID] IS NULL OR [UUID] = '')", ct);
        }
    }
}
