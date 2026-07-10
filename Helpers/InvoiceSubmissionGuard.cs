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

            // Refresh the concurrency token of any already-tracked instance so the caller's
            // post-submission SaveChanges succeeds (see XML doc above).
            // Case-insensitive to match SQL Server's default collation — the claim UPDATE above
            // matches the row regardless of casing, so the reload must find the same entity.
            var tracked = db.ChangeTracker.Entries<InvoiceHeader>()
                .Where(e => string.Equals(e.Entity.InvoiceNo, invoiceNo, StringComparison.OrdinalIgnoreCase))
                .ToList();
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
