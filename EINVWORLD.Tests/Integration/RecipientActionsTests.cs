using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Services;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// v1.50.72: who may act on a received invoice and what the buyer is told. B is the supplier, A the buyer;
    /// self-billed 11–14 flip the issuer to the customer. Runs on SQL Server (the rules are EF expressions).
    /// </summary>
    public class RecipientActionsTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public RecipientActionsTests(SqlServerFixture fx) => _fx = fx;

        private static async Task<(PartyInfo a, PartyInfo b)> PartiesAsync(eInvWorld.Data.ApplicationDbContext ctx, string label)
        {
            var a = await CreateValidPartyInfoAsync(ctx, $"RA {label} A");
            var b = await CreateValidPartyInfoAsync(ctx, $"RA {label} B");
            ctx.PartyInfos.AddRange(a, b);
            await ctx.SaveChangesAsync();
            return (a, b);
        }

        private static InvoiceHeader Add(eInvWorld.Data.ApplicationDbContext ctx, string type, int supplierId, int customerId,
            string internalStatus, string? lhdn, bool uuid = true, decimal total = 10m)
        {
            var no = $"INV-RA-{Guid.NewGuid():N}"[..20];
            var inv = new InvoiceHeader
            {
                InvoiceNo = no, PrefixedID = no, DocTypeCode = type, Currency = "MYR", CreatedBy = "integration-test",
                CreatedDate = DateTime.Now, LastUpdated = DateTime.Now, InternalStatusId = internalStatus, LHDNStatusId = lhdn,
                UUID = uuid ? $"R{Guid.NewGuid():N}"[..26] : null, SupplierId = supplierId, CustomerId = customerId, TotalAmountIncTax = total,
            };
            ctx.InvoiceHeaders.Add(inv);
            return inv;
        }

        [Fact]
        public async Task Only_The_Issuing_Company_May_Send_To_Buyers()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b) = await PartiesAsync(ctx, "send");
            var normal = Add(ctx, "01", b.PartyInfoId, a.PartyInfoId, "Valid", "Valid");      // B issues to A
            var creditNote = Add(ctx, "02", b.PartyInfoId, a.PartyInfoId, "Valid", "Valid");
            var selfBilled = Add(ctx, "11", b.PartyInfoId, a.PartyInfoId, "Valid", "Valid");  // A issues (self-billed)
            var selfBilledRefund = Add(ctx, "14", b.PartyInfoId, a.PartyInfoId, "Valid", "Valid");
            await ctx.SaveChangesAsync();
            var uuids = new[] { normal, creditNote, selfBilled, selfBilledRefund }.Select(i => i.UUID!).ToList();

            List<string> Scope(PartyInfo p) => eInvWorld.Pages.Invoices.InvoiceListsModel
                .SendToBuyersScope(ctx.InvoiceHeaders, uuids, new List<int> { p.PartyInfoId }).Select(i => i.InvoiceNo).OrderBy(x => x).ToList();

            Assert.Equal(new[] { normal.InvoiceNo, creditNote.InvoiceNo }.OrderBy(x => x), Scope(b));
            Assert.Equal(new[] { selfBilled.InvoiceNo, selfBilledRefund.InvoiceNo }.OrderBy(x => x), Scope(a));
        }

        [Fact]
        public async Task Buyer_Dashboard_Totals_Are_Exclusive_And_Match_The_Received_Tab()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b) = await PartiesAsync(ctx, "dash");
            Add(ctx, "01", b.PartyInfoId, a.PartyInfoId, "Valid", "Valid", total: 100m);
            Add(ctx, "01", b.PartyInfoId, a.PartyInfoId, "RequestReject", "Valid", total: 20m);  // A asked to reject: still LHDN Valid
            Add(ctx, "02", b.PartyInfoId, a.PartyInfoId, "Cancelled", "Cancelled", total: 3m);
            Add(ctx, "01", b.PartyInfoId, a.PartyInfoId, "Submitted", "Submitted", total: 1000m); // pending: not received yet
            Add(ctx, "01", b.PartyInfoId, a.PartyInfoId, "Invalid", "Invalid", total: 1000m);     // never received
            Add(ctx, "01", b.PartyInfoId, a.PartyInfoId, "Draft", null, uuid: false, total: 1000m);
            Add(ctx, "11", b.PartyInfoId, a.PartyInfoId, "Valid", "Valid", total: 1000m);         // A issued it (self-billed)
            Add(ctx, "11", a.PartyInfoId, b.PartyInfoId, "Valid", "Valid", total: 7m);            // B issued self-billed to A
            await ctx.SaveChangesAsync();

            var tins = new List<string> { a.TIN! };
            var mine = ctx.InvoiceHeaders.Where(i => i.SupplierId == a.PartyInfoId || i.SupplierId == b.PartyInfoId);
            var received = mine.Where(UserExtensions.ReceivedByTins(tins));
            var t = eInvWorld.Pages.Invoices.DashboardModel.BuyerTotalsOf(received);

            Assert.Equal(4, t.TotalCount);
            Assert.Equal(130m, t.TotalAmount);
            Assert.Equal((2, 107m), (t.ValidCount, t.ValidAmount));
            Assert.Equal((1, 20m), (t.RejectCount, t.RejectAmount));
            Assert.Equal((1, 3m), (t.CancelledCount, t.CancelledAmount));
            Assert.Equal(t.TotalCount, t.ValidCount + t.RejectCount + t.CancelledCount);
            Assert.Equal(t.TotalCount, await eInvWorld.Pages.Invoices.InvoiceListsModel.ApplyDirectionFilter(mine, "Received", tins).CountAsync());
        }

        private sealed class RecordingNotifications : IEInvoiceNotificationService
        {
            public readonly List<string> Received = new();
            public readonly List<string> Validated = new();
            public readonly Dictionary<string, int?> Recipient = new();
            public Task SendValidatedNotificationEmail(string recipientName, PartyInfo? buyer, PartyInfo? supplier, string documentId, DateTime issueDate, DateTime validatedTimestamp, PublicCustomer? publicCustomer = null)
            { Validated.Add(documentId); return Task.CompletedTask; }
            public Task<bool> SendRejectionNotificationEmail(PartyInfo? buyer, PartyInfo? supplier, string documentId, string rejectionReason, DateTime rejectedTimestamp) => Task.FromResult(true);
            public Task<bool> SendCancellationNotificationEmail(PartyInfo? buyer, PartyInfo? supplier, string documentId, string cancellationReason, DateTime cancelledTimestamp) => Task.FromResult(true);
            public Task<bool> SendNewInvoiceReceivedNotificationEmail(PartyInfo? buyer, PartyInfo? supplier, string documentId, DateTime issueDate, PublicCustomer? publicCustomer = null)
            { Received.Add(documentId); Recipient[documentId] = buyer?.PartyInfoId; return Task.FromResult(true); }
        }

        [Fact]
        public async Task Received_Invoice_Email_Is_Sent_Once_And_Only_When_Lhdn_Validated_It()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b) = await PartiesAsync(ctx, "mail");
            InvoiceHeader Synced(string lhdn, string type = "01")
            {
                var inv = Add(ctx, type, b.PartyInfoId, a.PartyInfoId, lhdn, lhdn);
                inv.IsNewInvoiceReceivedEmailSent = false; // what InvoiceFullSyncHelper sets for a newly synced received document
                inv.DateTimeReceived = DateTime.Now;
                if (lhdn == "Valid") inv.DateTimeValidated = DateTime.Now;
                return inv;
            }
            var pending = Synced("Submitted");
            pending.DateTimeReceived = DateTime.Now.AddDays(-5); // LHDN (or our import) is slow: must not expire while pending
            var valid = Synced("Valid");
            var invalid = Synced("Invalid");
            var cancelled = Synced("Cancelled");
            var selfBilled = Synced("Valid", "11");              // A (customer) issued it, B (supplier) receives it
            await ctx.SaveChangesAsync();

            var mail = new RecordingNotifications();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailConfiguration:Notifications:EnableNewInvoiceReceivedEmails"] = "true",
            }).Build();
            var finalizer = new InvoiceFinalizer(ctx, null!, mail, Options.Create(new FilePathConfig()), config, NullLogger<InvoiceFinalizer>.Instance);

            foreach (var inv in new[] { pending, valid, invalid, cancelled, selfBilled })
                await finalizer.SendNewInvoiceReceivedEmailAsync(inv.InvoiceNo);
            await finalizer.SendNewInvoiceReceivedEmailAsync(valid.InvoiceNo); // a second pass must not send again

            Assert.Equal(new[] { valid.InvoiceNo, selfBilled.InvoiceNo }, mail.Received);
            Assert.Equal(a.PartyInfoId, mail.Recipient[valid.InvoiceNo]);      // normal: the customer receives
            Assert.Equal(b.PartyInfoId, mail.Recipient[selfBilled.InvoiceNo]); // self-billed: the supplier receives
            async Task<bool> Done(InvoiceHeader i) => await ctx.InvoiceHeaders.AsNoTracking().Where(x => x.InvoiceNo == i.InvoiceNo).Select(x => x.IsNewInvoiceReceivedEmailSent).SingleAsync();
            Assert.False(await Done(pending));   // still waiting for LHDN's verdict: a later pass decides
            Assert.True(await Done(invalid));    // closed without an email
            Assert.True(await Done(cancelled));

            // Once LHDN validates the (5-day-old) pending one, the next pass notifies exactly once.
            await ctx.InvoiceHeaders.Where(x => x.InvoiceNo == pending.InvoiceNo)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LHDNStatusId, "Valid").SetProperty(x => x.InternalStatusId, "Valid")
                    .SetProperty(x => x.DateTimeValidated, DateTime.Now));
            await finalizer.SendNewInvoiceReceivedEmailAsync(pending.InvoiceNo);
            await finalizer.SendNewInvoiceReceivedEmailAsync(pending.InvoiceNo);
            Assert.Equal(new[] { valid.InvoiceNo, selfBilled.InvoiceNo, pending.InvoiceNo }, mail.Received);
        }
    }
}
