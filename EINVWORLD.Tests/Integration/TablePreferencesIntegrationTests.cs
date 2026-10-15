using System;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Helpers;
using eInvWorld.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Real-SQL-Server tests for the table-preference compare-and-swap (ExecuteUpdate with an
    /// nvarchar(max) equality guard), which the in-memory provider cannot run.
    /// </summary>
    public class TablePreferencesIntegrationTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public TablePreferencesIntegrationTests(SqlServerFixture fx) => _fx = fx;

        private static async Task<ApplicationUser> CreateUserAsync(eInvWorld.Data.ApplicationDbContext ctx, string? prefs)
        {
            var name = $"tp{Guid.NewGuid():N}@example.test";
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = name,
                NormalizedUserName = name.ToUpperInvariant(),
                Email = name,
                NormalizedEmail = name.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                FullName = "Table Prefs Test",
                UserType = "Supplier",
                UserPreferences = prefs,
            };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
            return user;
        }

        [Fact]
        public async Task Save_WritesOnlyThePreferencesColumn_AndKeepsOtherKeys()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var user = await CreateUserAsync(ctx, "{\"invoiceListColumns\":{\"col-uuid\":false}}");
            var stamp = user.ConcurrencyStamp;

            var result = await TablePreferences.SaveAsync(ctx, user.Id, "items",
                new TableColumnPreference { Hidden = new() { "UUID" }, Order = new() { "Total", "Name" } });
            Assert.Equal(TablePreferenceSaveResult.Saved, result);

            await using var check = _fx.CreateContext();
            var row = await check.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
            var root = JObject.Parse(row.UserPreferences!);
            Assert.False((bool)root["invoiceListColumns"]!["col-uuid"]!);
            Assert.Equal("UUID", (string)root["tableColumns:items"]!["hidden"]![0]!);
            Assert.Equal(stamp, row.ConcurrencyStamp); // identity row otherwise untouched

            var loaded = await TablePreferences.LoadAsync(check, user.Id, new[] { "items" });
            Assert.Equal(new[] { "Total", "Name" }, loaded["items"].Order);
        }

        [Fact]
        public async Task Save_FromNull_AndUnchanged()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var user = await CreateUserAsync(ctx, null);
            var pref = new TableColumnPreference { Hidden = new() { "A" } };
            Assert.Equal(TablePreferenceSaveResult.Saved, await TablePreferences.SaveAsync(ctx, user.Id, "items", pref));
            Assert.Equal(TablePreferenceSaveResult.Unchanged, await TablePreferences.SaveAsync(ctx, user.Id, "items", pref));
        }

        [Fact]
        public async Task Save_ConflictsWhenPreferencesChangedAfterTheRead()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var user = await CreateUserAsync(ctx, "{}");

            // Simulate a concurrent writer between our read and our update: the guarded update must not apply.
            await using (var other = _fx.CreateContext())
                await other.Users.Where(u => u.Id == user.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.UserPreferences, "{\"theirs\":1}"));
            var current = await ctx.Users.Where(u => u.Id == user.Id).Select(u => u.UserPreferences).SingleAsync();
            Assert.Equal("{\"theirs\":1}", current);

            // A save that read the old "{}" state would be rejected by the guard; SaveAsync re-reads, so it merges.
            Assert.Equal(TablePreferenceSaveResult.Saved,
                await TablePreferences.SaveAsync(ctx, user.Id, "items", new TableColumnPreference { Hidden = new() { "A" } }));
            var stale = await ctx.Users.Where(u => u.Id == user.Id && u.UserPreferences == "{}")
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.UserPreferences, "{\"lost\":1}"));
            Assert.Equal(0, stale);

            await using var check = _fx.CreateContext();
            var root = JObject.Parse((await check.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).UserPreferences!);
            Assert.Equal(1, (int)root["theirs"]!);
            Assert.NotNull(root["tableColumns:items"]);
            Assert.Null(root["lost"]);
        }
    }
}
