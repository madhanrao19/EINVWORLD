namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Pure decision rules for LHDN status synchronisation, consolidated from logic that was
    /// duplicated across the sync helpers. Kept side-effect-free so the behaviour is unit-testable.
    /// </summary>
    public static class InvoiceSyncRules
    {
        /// <summary>
        /// A document in a terminal LHDN state (Invalid / Cancelled / Rejected) never changes again,
        /// so there is no point polling it.
        /// </summary>
        public static bool IsPermanentStatus(string? lhdnStatusId)
            => lhdnStatusId is "Invalid" or "Cancelled" or "Rejected";

        /// <summary>
        /// For an already-<c>Valid</c> document, decides whether a re-poll should be SKIPPED based on a
        /// per-caller cooldown (to avoid hammering LHDN):
        ///   • BackgroundService — 15 min while the LongId/QR is still missing, 1 h once present.
        ///   • UISession        — 60 s (stops users spamming refresh).
        /// Non-Valid statuses are never skipped on this basis (they still need polling).
        /// </summary>
        public static bool ShouldSkipValidRefresh(
            string? lhdnStatusId, bool hasLongId, DateTime lastCheckedMyt, DateTime nowMyt, string? triggeredBy)
        {
            if (lhdnStatusId != "Valid") return false;

            var elapsed = nowMyt - lastCheckedMyt;
            return triggeredBy switch
            {
                "BackgroundService" => hasLongId ? elapsed.TotalHours < 1 : elapsed.TotalMinutes < 15,
                "UISession" => elapsed.TotalSeconds < 60,
                _ => false
            };
        }
    }
}
