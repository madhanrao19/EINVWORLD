using System;
using System.Threading.Tasks;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// SupplierBasePage's company gate, as one table: it must authorise against the company a page acts on,
    /// never a query string. Before v1.50.19 the gate checked the company in "?id=", so a user who was view-only
    /// on their primary company A but an editor of company B could add "?id=B" and write to A.
    /// Every actor has company A as primary; "Other" is a second company B.
    /// </summary>
    public class SupplierPageGateTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public SupplierPageGateTests(SqlServerFixture fx) => _fx = fx;

        [Theory]
        //  actor (memberships)          target  path                    allowed
        // Acting-company pages (Items, Buyers, Invoices, Templates): target = null.
        [InlineData("EditorOnA",            null,    "/Items/Create",         true)]
        [InlineData("ViewOnlyOnA_EditorOnB", null,   "/Items/Create",         false)]   // the old "?id=B" bypass
        [InlineData("ViewOnlyOnA_EditorOnB", null,   "/Dashboard/Dashboard",  true)]    // view-only: dashboard only
        [InlineData("BuyerNoAccessOnA",     null,    "/PublicCustomer/Create", false)]
        [InlineData("NoCompany",            null,    "/Items/Create",         false)]
        [InlineData("Admin",                null,    "/Items/Create",         true)]
        // Company pages (Suppliers/*): target = the company named by the handler's own argument.
        [InlineData("ViewOnlyOnA_EditorOnB", "B",    "/Suppliers/Users",      true)]
        [InlineData("ViewOnlyOnA_EditorOnB", "A",    "/Suppliers/Users",      false)]
        [InlineData("EditorOnA",            "B",     "/Suppliers/Users",      false)]   // not a member of B
        [InlineData("Outsider",             "A",     "/Suppliers/Details",    false)]
        [InlineData("Admin",                "B",     "/Suppliers/Details",    true)]
        public async Task Gate_Uses_The_Target_Company(string actor, string? target, string path, bool allowed)
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var a = await CreateValidPartyInfoAsync(ctx, "Gate Co A");
            var b = await CreateValidPartyInfoAsync(ctx, "Gate Co B");
            var outsiderCo = await CreateValidPartyInfoAsync(ctx, "Gate Co Outsider");
            ctx.PartyInfos.AddRange(a, b, outsiderCo);
            await ctx.SaveChangesAsync();

            var userId = await CreateUserAsync(ctx, $"gate-{actor}");
            void Member(int company, bool primary, bool access, bool viewOnly) => ctx.UserCompanies.Add(new UserCompany
            {
                UserId = userId, PartyInfoId = company, IsPrimaryCompany = primary, HasCompanyAccess = access, IsViewOnly = viewOnly
            });
            switch (actor)
            {
                case "EditorOnA": Member(a.PartyInfoId, true, true, false); break;
                case "ViewOnlyOnA_EditorOnB": Member(a.PartyInfoId, true, false, true); Member(b.PartyInfoId, false, true, false); break;
                case "BuyerNoAccessOnA": Member(a.PartyInfoId, false, false, false); break;
                case "Outsider": Member(outsiderCo.PartyInfoId, true, true, false); break;
                case "NoCompany": case "Admin": break;
                default: throw new ArgumentOutOfRangeException(nameof(actor), actor, null);
            }
            await ctx.SaveChangesAsync();

            int? targetId = target switch { "A" => a.PartyInfoId, "B" => b.PartyInfoId, _ => null };
            Assert.Equal(allowed, await SupplierBasePage.MayEnterAsync(ctx, userId, actor == "Admin", targetId, path));
        }

        [Fact]
        public async Task ActingCompany_IsPrimaryFirst_ThenOldestMembership()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var x = await CreateValidPartyInfoAsync(ctx, "Gate Order X");
            var y = await CreateValidPartyInfoAsync(ctx, "Gate Order Y");
            ctx.PartyInfos.AddRange(x, y);
            await ctx.SaveChangesAsync();
            var userId = await CreateUserAsync(ctx, "gate-order");
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = y.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync(); // y first: the older membership
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = x.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            // No primary: the oldest membership (y was assigned first), every time; the gate and the handlers run this query.
            var first = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(SupplierBasePage.ActingCompany(ctx, userId));
            Assert.Equal(y.PartyInfoId, first.PartyInfoId);
        }
    }
}
