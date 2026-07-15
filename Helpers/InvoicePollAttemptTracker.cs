using System.Collections.Concurrent;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Process-wide record of when each invoice was last *attempted* to be polled against LHDN,
    /// regardless of whether that poll changed any data. The persisted <c>InvoiceHeader.LastUpdated</c>
    /// only advances when LHDN data actually changed, so the cooldown in
    /// <see cref="InvoiceSyncRules.ShouldSkipValidRefresh"/> never engaged for long-unchanged Valid
    /// invoices — every UI tick and background pass re-polled them, flooding the LHDN rate limit (429).
    /// Combining the DB timestamp with this in-memory attempt time makes the cooldown hold without
    /// extra DB writes (which would churn the rowversion and provoke concurrency conflicts).
    ///
    /// Timestamps are Malaysia time to stay comparable with <c>LastUpdated</c>. The store is static and
    /// in-process, which is correct on this single-instance deployment (same assumption as
    /// <c>LhdnRateLimitHandler</c>); after an app restart the first pass re-polls once, paced by the
    /// rate-limit handler.
    /// </summary>
    public static class InvoicePollAttemptTracker
    {
        private static readonly ConcurrentDictionary<string, DateTime> _lastAttemptMyt =
            new(StringComparer.OrdinalIgnoreCase);

        // Prune guard: aged entries are dropped once the map grows past MaxEntries; if everything is
        // still fresh (a very large sync window), the oldest overflow is evicted instead so MaxEntries
        // is a real bound. Evicting a fresh entry is harmless — worst case that invoice is re-polled once.
        private static readonly TimeSpan MaxAge = TimeSpan.FromHours(2);
        private const int MaxEntries = 10_000;

        /// <summary>Records that a poll of <paramref name="invoiceNo"/> is being attempted now.</summary>
        public static void RecordAttempt(string invoiceNo, DateTime nowMyt)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo)) return;

            _lastAttemptMyt[invoiceNo] = nowMyt;

            if (_lastAttemptMyt.Count > MaxEntries)
            {
                Prune(nowMyt);
            }
        }

        /// <summary>
        /// Returns the later of the persisted last-checked time and the last in-memory poll attempt,
        /// for use as the cooldown reference time.
        /// </summary>
        public static DateTime GetEffectiveLastChecked(string invoiceNo, DateTime persistedLastCheckedMyt)
        {
            if (!string.IsNullOrWhiteSpace(invoiceNo) &&
                _lastAttemptMyt.TryGetValue(invoiceNo, out var attempt) &&
                attempt > persistedLastCheckedMyt)
            {
                return attempt;
            }
            return persistedLastCheckedMyt;
        }

        private static void Prune(DateTime nowMyt)
        {
            foreach (var entry in _lastAttemptMyt)
            {
                if (nowMyt - entry.Value > MaxAge)
                {
                    _lastAttemptMyt.TryRemove(entry.Key, out _);
                }
            }

            // Everything (or nearly everything) is still fresh: evict oldest-first down to the cap so
            // the map never grows unbounded and RecordAttempt doesn't rescan a map that can't shrink.
            var overflow = _lastAttemptMyt.Count - MaxEntries;
            if (overflow > 0)
            {
                foreach (var entry in _lastAttemptMyt.OrderBy(e => e.Value).Take(overflow))
                {
                    _lastAttemptMyt.TryRemove(entry.Key, out _);
                }
            }
        }

        /// <summary>Clears all recorded attempts. Intended for tests; harmless elsewhere (next poll re-records).</summary>
        public static void Reset() => _lastAttemptMyt.Clear();
    }
}
