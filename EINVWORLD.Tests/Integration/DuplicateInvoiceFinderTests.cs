using System;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>Possible-duplicate warning: same supplier, buyer, type, currency and total, plus PO, reference or issue date.</summary>
    public class DuplicateInvoiceFinderTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public DuplicateInvoiceFinderTests(SqlServerFixture fx) => _fx = fx;

        [Fact]
        public async Task Finds_Probable_Duplicates_And_Ignores_Everything_Else()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            foreach (var code in new[] { "Draft", "Valid", "Cancelled", "Invalid" })
                if (!await ctx.Set<Status>().AnyAsync(s => s.StatusCode == code))
                    ctx.Set<Status>().Add(new Status { StatusCode = code, StatusType = "Internal", Name = code });

            var own = await CreateValidPartyInfoAsync(ctx, "Dup Own Co");
            var buyer = await CreateValidPartyInfoAsync(ctx, "Dup Buyer Co");
            var otherSupplier = await CreateValidPartyInfoAsync(ctx, "Dup Other Supplier");
            ctx.PartyInfos.AddRange(own, buyer, otherSupplier);
            await ctx.SaveChangesAsync();

            var day = new DateTime(2026, 10, 20, 10, 0, 0);
            string Add(decimal total, string? po = null, string? reference = null, DateTime? issue = null,
                       string status = "Draft", string? lhdn = null, int? supplierId = null, string currency = "MYR")
            {
                var no = $"INV-DP-{Guid.NewGuid():N}"[..20];
                ctx.InvoiceHeaders.Add(new InvoiceHeader
                {
                    InvoiceNo = no, PrefixedID = no, DocTypeCode = "01", Currency = currency, CreatedDate = DateTime.Now,
                    CreatedBy = "integration-test", InternalStatusId = status, LHDNStatusId = lhdn,
                    SupplierId = supplierId ?? own.PartyInfoId, CustomerId = buyer.PartyInfoId,
                    TotalPayableAmount = total, PoDoNo = po, RefDocumentNo = reference, IssueDate = issue ?? day.AddDays(-30),
                });
                return no;
            }
            var samePo = Add(1080m, po: "PO-1");
            var sameRef = Add(1080.01m, reference: "REF-9");
            var sameDay = Add(1080m, issue: day.AddHours(3));
            Add(1080m, po: "PO-1", status: "Cancelled", lhdn: "Cancelled");   // cancelled: ignored
            Add(1080m, po: "PO-1", status: "Invalid", lhdn: "Invalid");        // LHDN rejected: ignored
            Add(999m, po: "PO-1");                                              // different total
            Add(1080m, po: "PO-1", supplierId: otherSupplier.PartyInfoId);      // another supplier (tenant)
            Add(1080m, po: "PO-1", currency: "USD");                            // different currency
            Add(1080m, po: "PO-2");                                             // nothing else in common
            var candidate = Add(1080m, po: "PO-1", reference: "REF-9", issue: day);
            await ctx.SaveChangesAsync();

            var found = await DuplicateInvoiceFinder.FindForSavedAsync(ctx, candidate);
            Assert.Equal(new[] { samePo, sameRef, sameDay }.OrderBy(x => x), found.Select(f => f.InvoiceNo).OrderBy(x => x));
            Assert.Equal("PO/DO number", found.Single(f => f.InvoiceNo == samePo).Reason);
            Assert.Equal("reference number", found.Single(f => f.InvoiceNo == sameRef).Reason);
            Assert.Equal("issue date", found.Single(f => f.InvoiceNo == sameDay).Reason);
            Assert.All(DuplicateInvoiceFinder.Warnings(found), w => Assert.StartsWith("Possible duplicate: ", w));

            Assert.Empty(await DuplicateInvoiceFinder.FindForSavedAsync(ctx, "no-such-invoice"));

            // Import preview: the same CSV invoice number for the same parties, whatever the total.
            Assert.Contains(await DuplicateInvoiceFinder.FindImportedAsync(ctx, own.PartyInfoId, buyer.PartyInfoId, null, "REF-9"), new[] { sameRef, candidate }); // both carry REF-9
            Assert.Null(await DuplicateInvoiceFinder.FindImportedAsync(ctx, otherSupplier.PartyInfoId, buyer.PartyInfoId, null, "REF-9"));
            Assert.Null(await DuplicateInvoiceFinder.FindImportedAsync(ctx, own.PartyInfoId, buyer.PartyInfoId, null, " "));
        }
    }
}
