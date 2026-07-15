using System;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Covers the in-memory poll-attempt tracker and its interplay with
    /// <see cref="InvoiceSyncRules.ShouldSkipValidRefresh"/> — the fix for the staging 429 storm,
    /// where long-unchanged Valid invoices were re-polled on every UI tick because the persisted
    /// LastUpdated (which only advances on data change) never aged the cooldown.
    /// These tests share static state, so each starts from <see cref="InvoicePollAttemptTracker.Reset"/>.
    /// </summary>
    public class InvoicePollAttemptTrackerTests
    {
        private static readonly DateTime Now = new(2026, 7, 15, 10, 0, 0);

        public InvoicePollAttemptTrackerTests()
        {
            InvoicePollAttemptTracker.Reset();
        }

        [Fact]
        public void GetEffectiveLastChecked_NoAttemptRecorded_ReturnsPersistedTime()
        {
            var persisted = Now.AddDays(-3);
            Assert.Equal(persisted, InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100291", persisted));
        }

        [Fact]
        public void GetEffectiveLastChecked_AttemptNewerThanPersisted_ReturnsAttempt()
        {
            var persisted = Now.AddDays(-3);
            InvoicePollAttemptTracker.RecordAttempt("EINV100291", Now);

            Assert.Equal(Now, InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100291", persisted));
        }

        [Fact]
        public void GetEffectiveLastChecked_PersistedNewerThanAttempt_ReturnsPersisted()
        {
            // A real data change (LastUpdated bump) after our last attempt must win.
            InvoicePollAttemptTracker.RecordAttempt("EINV100291", Now.AddMinutes(-30));
            var persisted = Now.AddMinutes(-1);

            Assert.Equal(persisted, InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100291", persisted));
        }

        [Fact]
        public void GetEffectiveLastChecked_DifferentInvoice_IsUnaffected()
        {
            InvoicePollAttemptTracker.RecordAttempt("EINV100291", Now);
            var persisted = Now.AddDays(-3);

            Assert.Equal(persisted, InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100360", persisted));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RecordAttempt_BlankInvoiceNo_IsIgnored(string? invoiceNo)
        {
            InvoicePollAttemptTracker.RecordAttempt(invoiceNo!, Now);
            var persisted = Now.AddDays(-1);

            Assert.Equal(persisted, InvoicePollAttemptTracker.GetEffectiveLastChecked(invoiceNo!, persisted));
        }

        // ── Interplay with the cooldown rule: the scenario from the staging logs ─────────────────

        [Fact]
        public void UiSession_UnchangedValidInvoice_SecondPollWithinCooldown_IsSkipped()
        {
            // Invoice validated days ago, LHDN data never changed since → persisted clock is stale.
            var persistedLastUpdated = Now.AddDays(-5);

            // Before the fix: cooldown keyed on the stale persisted time → never skipped (the 429 storm).
            var before = InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100291", persistedLastUpdated);
            Assert.False(InvoiceSyncRules.ShouldSkipValidRefresh("Valid", hasLongId: true, before, Now, "UISession"));

            // First poll records its attempt; the next UI tick 30s later must now be skipped.
            InvoicePollAttemptTracker.RecordAttempt("EINV100291", Now);
            var nextTick = Now.AddSeconds(30);
            var effective = InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100291", persistedLastUpdated);

            Assert.True(InvoiceSyncRules.ShouldSkipValidRefresh("Valid", hasLongId: true, effective, nextTick, "UISession"));
        }

        [Fact]
        public void UiSession_AfterCooldownExpires_PollsAgain()
        {
            InvoicePollAttemptTracker.RecordAttempt("EINV100291", Now);
            var afterCooldown = Now.AddSeconds(61);
            var effective = InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100291", Now.AddDays(-5));

            Assert.False(InvoiceSyncRules.ShouldSkipValidRefresh("Valid", hasLongId: true, effective, afterCooldown, "UISession"));
        }

        [Fact]
        public void BackgroundService_UnchangedValidInvoiceWithLongId_SkippedForAnHour()
        {
            InvoicePollAttemptTracker.RecordAttempt("EINV100291", Now);
            var effective = InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100291", Now.AddDays(-5));

            Assert.True(InvoiceSyncRules.ShouldSkipValidRefresh("Valid", hasLongId: true, effective, Now.AddMinutes(59), "BackgroundService"));
            Assert.False(InvoiceSyncRules.ShouldSkipValidRefresh("Valid", hasLongId: true, effective, Now.AddMinutes(61), "BackgroundService"));
        }

        [Fact]
        public void NonValidStatus_NeverSkipped_EvenWithRecentAttempt()
        {
            // A Submitted/Pending document must keep polling regardless of attempt recency.
            InvoicePollAttemptTracker.RecordAttempt("EINV100380", Now);
            var effective = InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100380", Now.AddDays(-5));

            Assert.False(InvoiceSyncRules.ShouldSkipValidRefresh("Submitted", hasLongId: false, effective, Now.AddSeconds(5), "UISession"));
        }

        [Fact]
        public void RecordAttempt_FailedPollSemantics_CooldownStartsEvenWithoutSuccess()
        {
            // The attempt is recorded BEFORE the LHDN call, so a 429-failed poll still starts the
            // cooldown and the invoice is not hammered again on the next tick.
            InvoicePollAttemptTracker.RecordAttempt("EINV100367", Now);
            var effective = InvoicePollAttemptTracker.GetEffectiveLastChecked("EINV100367", Now.AddDays(-2));

            Assert.True(InvoiceSyncRules.ShouldSkipValidRefresh("Valid", hasLongId: true, effective, Now.AddSeconds(45), "UISession"));
        }
    }
}
