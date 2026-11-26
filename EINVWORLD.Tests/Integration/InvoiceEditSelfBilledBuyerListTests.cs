using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.ViewModels;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Reopening a self-billed draft in Invoice Edit must list the user's own companies as buyers, so the saved buyer
    /// (the own company) shows as selected. It used to list the general-TIN seller's buyers and showed "Select Buyer".
    /// </summary>
    public class InvoiceEditSelfBilledBuyerListTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public InvoiceEditSelfBilledBuyerListTests(SqlServerFixture fx) => _fx = fx;

        [Fact]
        public async Task Self_Billed_Draft_Lists_The_Own_Company_As_Buyer()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var own = await CreateValidPartyInfoAsync(ctx, "SB Own Co");
            var seller = await CreateValidPartyInfoAsync(ctx, "SB General Seller");
            ctx.PartyInfos.AddRange(own, seller);
            await ctx.SaveChangesAsync();
            var userId = await CreateUserAsync(ctx, "sb-buyer-list");
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = own.PartyInfoId, HasCompanyAccess = true, IsPrimaryCompany = true });
            await ctx.SaveChangesAsync();

            var page = InvoiceEditLockedRedirectTests.Page(ctx, PrincipalFor(userId, "sb-buyer-list", "Supplier"), "GET");
            page.Invoice = new InvoiceHeaderView { DocTypeCode = "11", SupplierId = seller.PartyInfoId, CustomerId = own.PartyInfoId };

            await page.LoadSuppliersAndCustomers();

            Assert.Equal(new[] { $"PI_{own.PartyInfoId}" }, page.Customers.Select(c => c.Value));
        }
    }
}
