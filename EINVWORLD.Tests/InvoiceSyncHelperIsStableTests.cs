using System;
using eInvWorld.Models.Document;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Pure tests for InvoiceSyncHelper.IsStable — the 72h skip-the-raw-fetch rule used by
    /// RunFullImportFromLhdnAsync to avoid re-hitting LHDN's rate-limited GetDocument endpoint for
    /// invoices that can no longer change status. Getting this wrong either wastes rate-limit budget
    /// (false negative) or silently misses a real status change within the cancel/reject window
    /// (false positive) — both are worth a direct check.
    /// </summary>
    public class InvoiceSyncHelperIsStableTests
    {
        private static DocumentSummary Summary(string status, string? longId = null, DateTime? validatedAt = null) =>
            new() { status = status, longId = longId!, dateTimeValidated = validatedAt };

        [Theory]
        [InlineData("Rejected")]
        [InlineData("Cancelled")]
        [InlineData("Invalid")]
        public void Terminal_status_is_always_stable(string status) =>
            Assert.True(InvoiceSyncHelper.IsStable(Summary(status)));

        [Fact]
        public void Valid_with_longId_validated_over_72h_ago_is_stable() =>
            Assert.True(InvoiceSyncHelper.IsStable(
                Summary("Valid", longId: "ABC123", validatedAt: DateTime.UtcNow.AddHours(-73))));

        [Fact]
        public void Valid_with_longId_validated_under_72h_ago_is_not_stable() =>
            Assert.False(InvoiceSyncHelper.IsStable(
                Summary("Valid", longId: "ABC123", validatedAt: DateTime.UtcNow.AddHours(-1))));

        [Fact]
        public void Valid_missing_longId_is_not_stable_regardless_of_age() =>
            Assert.False(InvoiceSyncHelper.IsStable(
                Summary("Valid", longId: null, validatedAt: DateTime.UtcNow.AddDays(-30))));

        [Fact]
        public void Valid_missing_dateTimeValidated_is_not_stable() =>
            Assert.False(InvoiceSyncHelper.IsStable(
                Summary("Valid", longId: "ABC123", validatedAt: null)));

        [Theory]
        [InlineData("Submitted")]
        [InlineData("")]
        public void Non_terminal_non_valid_status_is_not_stable(string status) =>
            Assert.False(InvoiceSyncHelper.IsStable(Summary(status)));
    }
}
