using System;
using System.Collections.Generic;
using System.Linq;
using eInvWorld.Models.Document;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Pure tests for InvoiceSyncHelper.PlanDocumentFetches — the per-run cap that keeps a large LHDN import
    /// (e.g. the 30-day "Refresh from API") from holding the shared get-document queue: newest first, at most
    /// N fetches, the rest deferred to a follow-up job, and up-to-date documents never fetched at all.
    /// </summary>
    public class InvoiceSyncHelperPlanFetchesTests
    {
        private static DocumentSummary Doc(string uuid, int daysAgo) =>
            new() { uuid = uuid, dateTimeReceived = DateTime.UtcNow.AddDays(-daysAgo) };

        [Fact]
        public void Caps_fetches_newest_first_and_defers_the_rest()
        {
            var docs = Enumerable.Range(1, 10).Select(i => Doc($"U{i}", daysAgo: i)).ToList();

            var (toFetch, skipped, deferred) = InvoiceSyncHelper.PlanDocumentFetches(docs, max: 3, canSkip: _ => false);

            Assert.Equal(new[] { "U1", "U2", "U3" }, toFetch.Select(d => d.uuid));
            Assert.Equal(0, skipped);
            Assert.Equal(7, deferred);
        }

        [Fact]
        public void Skipped_documents_do_not_use_the_cap()
        {
            var docs = new List<DocumentSummary> { Doc("old-stable", 1), Doc("new", 2), Doc("newer", 3) };

            var (toFetch, skipped, deferred) = InvoiceSyncHelper.PlanDocumentFetches(docs, max: 2, canSkip: d => d.uuid == "old-stable");

            Assert.Equal(new[] { "new", "newer" }, toFetch.Select(d => d.uuid));
            Assert.Equal(1, skipped);
            Assert.Equal(0, deferred);
        }

        [Fact]
        public void New_documents_come_before_newer_local_rows_that_need_a_refetch()
        {
            // 3 newer local rows that keep needing a re-fetch (e.g. an unrepairable buyer link) must not use the
            // whole budget: the older documents we don't have yet are fetched first, so each run makes progress.
            var docs = new List<DocumentSummary>
            {
                Doc("local-1", 1), Doc("local-2", 2), Doc("local-3", 3), Doc("new-old", 10), Doc("new-older", 11)
            };

            var (toFetch, skipped, deferred) = InvoiceSyncHelper.PlanDocumentFetches(docs, max: 3, canSkip: _ => false,
                isNew: d => d.uuid.StartsWith("new"));

            Assert.Equal(new[] { "new-old", "new-older", "local-1" }, toFetch.Select(d => d.uuid));
            Assert.Equal(0, skipped);
            Assert.Equal(2, deferred);
        }

        [Fact]
        public void Non_positive_cap_still_makes_progress()
        {
            var (toFetch, _, deferred) = InvoiceSyncHelper.PlanDocumentFetches(new[] { Doc("a", 1), Doc("b", 2) }, max: 0, canSkip: _ => false);

            Assert.Single(toFetch);
            Assert.Equal(1, deferred);
        }
    }
}
