using System;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// The v1.50.79 data migration puts LHDN's internal ID back into PrefixedID only for synced invoices whose InvoiceNo
    /// got sync's "(n)" collision suffix, so credit/debit/refund notes against them reference the real ID (DR313).
    /// </summary>
    public class BackfillSyncedLhdnInternalIdTests : IClassFixture<SqlServerFixture>
    {
        private const string SyncMarker = "System (Skipped for Sync)";
        private readonly SqlServerFixture _fx;
        public BackfillSyncedLhdnInternalIdTests(SqlServerFixture fx) => _fx = fx;

        [Fact]
        public async Task Backfill_Strips_Only_Collision_Suffixes_On_Synced_Rows()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            if (!await ctx.Set<Status>().AnyAsync(s => s.StatusCode == "Valid"))
                ctx.Set<Status>().Add(new Status { StatusCode = "Valid", StatusType = "Internal", Name = "Valid" });

            var b = $"BF{Guid.NewGuid():N}"[..14];
            void Add(string no, string? marker, string? prefixed = null) => ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = no, PrefixedID = prefixed ?? no, DocTypeCode = "01", Currency = "MYR", CreatedDate = DateTime.Now,
                CreatedBy = "integration-test", InternalStatusId = "Valid", ValidationEmailSentTo = marker,
            });
            Add($"{b}A", null);                                // the row a synced invoice collided with
            Add($"{b}A(1)", SyncMarker);                       // collided: LHDN ID is {b}A
            Add($"{b}A(12)", SyncMarker);                      // second-digit counter
            Add($"{b}B(1)", SyncMarker);                       // no {b}B here: LHDN's own ID ends in "(1)"
            Add($"{b}A(x)", SyncMarker);                       // not a sync counter
            Add($"{b}C", null); Add($"{b}C(1)", null);         // created here, not synced: never touched
            Add($"{b}A(2)", SyncMarker, prefixed: "LHDN-ID");  // status sync already set LHDN's ID
            await ctx.SaveChangesAsync();

            await ctx.Database.ExecuteSqlRawAsync(EINVWORLD.Migrations.BackfillSyncedLhdnInternalId.BackfillSql);
            await ctx.Database.ExecuteSqlRawAsync(EINVWORLD.Migrations.BackfillSyncedLhdnInternalId.BackfillSql); // re-run is a no-op

            async Task<string> Prefixed(string no) => await ctx.InvoiceHeaders.AsNoTracking().Where(i => i.InvoiceNo == no).Select(i => i.PrefixedID).SingleAsync();
            Assert.Equal($"{b}A", await Prefixed($"{b}A(1)"));
            Assert.Equal($"{b}A", await Prefixed($"{b}A(12)"));
            Assert.Equal($"{b}B(1)", await Prefixed($"{b}B(1)"));
            Assert.Equal($"{b}A(x)", await Prefixed($"{b}A(x)"));
            Assert.Equal($"{b}C(1)", await Prefixed($"{b}C(1)"));
            Assert.Equal("LHDN-ID", await Prefixed($"{b}A(2)"));
        }
    }
}
