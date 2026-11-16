using System;
using System.Collections.Generic;
using System.Linq;
using eInvWorld.Models.Audit;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>v1.50.73: a recipient sees the invoice's LHDN lifecycle, never the issuer's internal activity.</summary>
    public class RecipientInvoiceHistoryTests
    {
        private static InvoiceHistory H(string action, string by = "alice@issuer.test", string? remarks = "internal") =>
            new() { InvoiceNo = "INV1", Action = action, PerformedBy = by, Remarks = remarks, Timestamp = new DateTime(2026, 11, 16) };

        [Fact]
        public void Internal_Actions_Are_Hidden_And_Staff_Usernames_Masked()
        {
            var all = new List<InvoiceHistory>
            {
                H("Created"), H("Edited", remarks: "Total: 10 -> 12"), H("Submitted", remarks: "Submitted to LHDN - SubmissionUID: X"),
                H("StatusSync", remarks: "Updated status: Valid"), H("DateTimeValidated", remarks: "2026-11-16 10:00:00 AM"),
                H("PDFGenerated"), H("EmailSent", remarks: "buyer@x.test; cc@issuer.test"), H("EmailResent"), H("Attention Resolved"),
                H("LHDN Validation Failed"), H("Transmission Failed"), H("Deleted"), H("FileMoved"), H("Some Future Action"),
                H("RequestReject", by: "bob@recipient.test", remarks: "Rejection requested. Reason: wrong qty"),
                H("Cancelled", remarks: "Cancellation requested. Reason: duplicate"),
            };

            var seen = RecipientInvoiceHistory.For(all);

            Assert.Equal(new[] { "Submitted", "StatusSync", "DateTimeValidated", "RequestReject", "Cancelled" }, seen.Select(h => h.Action));
            Assert.DoesNotContain(seen, h => h.PerformedBy == "alice@issuer.test");            // no issuer staff username survives
            Assert.Equal(RecipientInvoiceHistory.Issuer, seen.Single(h => h.Action == "Submitted").PerformedBy);
            Assert.Null(seen.Single(h => h.Action == "Submitted").Remarks);                    // internal submission id dropped
            Assert.Equal(RecipientInvoiceHistory.Lhdn, seen.Single(h => h.Action == "StatusSync").PerformedBy);
            Assert.Equal("Updated status: Valid", seen.Single(h => h.Action == "StatusSync").Remarks);
            Assert.Equal("bob@recipient.test", seen.Single(h => h.Action == "RequestReject").PerformedBy); // the recipient's own action
            Assert.Equal("Cancellation requested. Reason: duplicate", seen.Single(h => h.Action == "Cancelled").Remarks);
            Assert.Equal("alice@issuer.test", all[2].PerformedBy);                             // the input is not modified
        }

        [Theory]
        [InlineData("POST", "BulkSendToBuyers", true)]
        [InlineData("POST", "bulksendtobuyers", true)]
        [InlineData("GET", "BulkSendToBuyers", false)]
        [InlineData("POST", "BulkValidate", false)]
        [InlineData("POST", null, false)]
        public void Send_To_Buyers_Rate_Limit_Applies_Only_To_Its_Handler(string method, string? handler, bool expected)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = method;
            if (handler != null) ctx.Request.QueryString = new QueryString($"?handler={handler}&handler=BulkValidate");
            Assert.Equal(expected, SendToBuyersRateLimit.Applies(ctx));
        }

        [Theory]
        [InlineData(null, 50)]
        [InlineData("10", 10)]
        [InlineData("0", 1)]
        [InlineData("100000", 500)]
        public void Send_To_Buyers_Batch_Cap_Is_Configurable_And_Bounded(string? configured, int expected)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Invoices:SendToBuyersMaxBatch"] = configured }).Build();
            Assert.Equal(expected, eInvWorld.Pages.Invoices.InvoiceListsModel.SendToBuyersMaxBatch(config));
        }
    }
}
