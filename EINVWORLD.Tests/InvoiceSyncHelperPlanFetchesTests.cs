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

        /// <summary>
        /// Simulates the import job chain the way RunFullImportFromLhdnAsync drives it: plan, fetch (new documents are
        /// stored unless they fail), then queue a follow-up only if ShouldQueueContinuation says so.
        /// Returns the number of runs and the UUIDs stored; throws if the chain does not stop.
        /// </summary>
        private static (int Runs, HashSet<string> Stored) RunChain(
            IReadOnlyList<DocumentSummary> lhdn, ISet<string> local, ISet<string> alwaysFails, int max = 40)
        {
            var stored = new HashSet<string>(local);
            for (int run = 1; run <= 1000; run++)
            {
                // Local rows are never skippable here: worst case, every one needs a repair that never succeeds.
                var (toFetch, _, deferred) = InvoiceSyncHelper.PlanDocumentFetches(lhdn, max, canSkip: _ => false,
                    isNew: d => !stored.Contains(d.uuid));
                int newImported = 0;
                foreach (var d in toFetch)
                    if (!alwaysFails.Contains(d.uuid) && stored.Add(d.uuid)) newImported++;
                if (!InvoiceSyncHelper.ShouldQueueContinuation(deferred, newImported)) return (run, stored);
            }
            throw new Xunit.Sdk.XunitException("Import continuation chain did not stop after 1000 runs.");
        }

        [Fact]
        public void All_unrepairable_local_rows_queue_no_follow_up()
        {
            var docs = Enumerable.Range(1, 100).Select(i => Doc($"L{i}", i)).ToList();

            var (runs, _) = RunChain(docs, local: docs.Select(d => d.uuid).ToHashSet(), alwaysFails: new HashSet<string>());

            Assert.Equal(1, runs);
        }

        [Fact]
        public void Mixed_batch_imports_every_new_document_then_stops()
        {
            // 60 newer local rows that never repair + 100 older new documents, cap 40.
            var local = Enumerable.Range(1, 60).Select(i => Doc($"L{i}", i)).ToList();
            var fresh = Enumerable.Range(1, 100).Select(i => Doc($"N{i}", 100 + i)).ToList();

            var (runs, stored) = RunChain(local.Concat(fresh).ToList(), local.Select(d => d.uuid).ToHashSet(), new HashSet<string>());

            Assert.All(fresh, d => Assert.Contains(d.uuid, stored));
            Assert.Equal(4, runs); // 40 + 40 + 20 new, then one run that finds nothing new
        }

        [Fact]
        public void Failing_new_documents_do_not_stop_the_others_and_the_chain_ends()
        {
            var fresh = Enumerable.Range(1, 50).Select(i => Doc($"N{i}", i)).ToList();
            var failing = fresh.Take(5).Select(d => d.uuid).ToHashSet(); // the 5 newest always fail

            var (runs, stored) = RunChain(fresh, new HashSet<string>(), failing);

            Assert.Equal(45, stored.Count);
            Assert.Equal(3, runs); // 35 new, 10 new, then a run with only failures and re-fetches
        }

        [Fact]
        public void A_run_where_every_fetch_fails_queues_no_follow_up()
        {
            // e.g. LHDN down: no follow-up; the next scheduled import or Refresh from API retries.
            var fresh = Enumerable.Range(1, 50).Select(i => Doc($"N{i}", i)).ToList();

            var (runs, stored) = RunChain(fresh, new HashSet<string>(), fresh.Select(d => d.uuid).ToHashSet());

            Assert.Equal(1, runs);
            Assert.Empty(stored);
        }

        [Theory]
        [InlineData(0, 5, false)]  // nothing left over
        [InlineData(7, 0, false)]  // left over, but this run stored nothing new (repairs/refreshes/failures only)
        [InlineData(7, 1, true)]
        public void Follow_up_needs_leftovers_and_a_new_document(int deferred, int newImported, bool expected) =>
            Assert.Equal(expected, InvoiceSyncHelper.ShouldQueueContinuation(deferred, newImported));

        [Fact]
        public void Non_positive_cap_still_makes_progress()
        {
            var (toFetch, _, deferred) = InvoiceSyncHelper.PlanDocumentFetches(new[] { Doc("a", 1), Doc("b", 2) }, max: 0, canSkip: _ => false);

            Assert.Single(toFetch);
            Assert.Equal(1, deferred);
        }
    }
}
