using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Services.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// v1.50.47: a non-Admin got 404 for a company or buyer id that doesn't exist but Access Denied for one that
    /// belongs to another company, so they could map which ids exist (12 Nov 2026 Staging QA: /Suppliers/Edit?id=).
    /// A non-Admin must now get the same answer for both; an Admin still gets 404 for a missing id.
    /// </summary>
    public class IdEnumerationTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public IdEnumerationTests(SqlServerFixture fx) => _fx = fx;

        private const int MissingId = int.MaxValue - 7;

        private sealed class NoTempData : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static T As<T>(T page, ClaimsPrincipal user) where T : PageModel
        {
            var http = new DefaultHttpContext { User = user };
            page.PageContext = new PageContext { HttpContext = http };
            page.TempData = new TempDataDictionary(http, new NoTempData());
            return page;
        }

        private static UserManager<ApplicationUser> Users(eInvWorld.Data.ApplicationDbContext ctx) =>
            new(new UserStore<ApplicationUser>(ctx), Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
                null!, null!, null!, null!, null!, NullLogger<UserManager<ApplicationUser>>.Instance);

        private static eInvWorld.Pages.Suppliers.EditModel CompanyEdit(eInvWorld.Data.ApplicationDbContext ctx, ClaimsPrincipal user) =>
            As(new eInvWorld.Pages.Suppliers.EditModel(ctx, null!, new eInvWorld.Helpers.DropdownHelper(ctx),
                NullLogger<eInvWorld.Pages.Suppliers.EditModel>.Instance, Options.Create(new FilePathConfig()), null!, Users(ctx),
                new CompanyAuthorizationService(ctx), null!), user);

        /// <summary>Company "mine" (the supplier is a member), company "other" with one buyer record of its own.</summary>
        private static async Task<(PartyInfo other, int otherBuyerId, ClaimsPrincipal supplier, ClaimsPrincipal admin)> SeedAsync(eInvWorld.Data.ApplicationDbContext ctx)
        {
            var mine = await CreateValidPartyInfoAsync(ctx, "Enum Mine Co");
            var other = await CreateValidPartyInfoAsync(ctx, "Enum Other Co");
            ctx.PartyInfos.AddRange(mine, other);
            await ctx.SaveChangesAsync();

            var buyer = new eInvWorld.Models.InputModel.PublicCustomer
            {
                CompanyName = "Enum Other Buyer", IndustryClassificationCode = "01111", TIN = other.TIN, RegTypeCode = other.RegTypeCode,
                RegNo = other.RegNo, Addr1 = other.Addr1, CityName = other.CityName, StateCode = other.StateCode,
                CountryCode = other.CountryCode, PhoneNo = other.PhoneNo, CreatedBy = "test", CreatedByCompanyId = other.PartyInfoId,
            };
            ctx.PublicCustomers.Add(buyer);

            var supplierId = await CreateUserAsync(ctx, "enum-supplier");
            ctx.UserCompanies.Add(new UserCompany { UserId = supplierId, PartyInfoId = mine.PartyInfoId, HasCompanyAccess = true, IsPrimaryCompany = true });
            await ctx.SaveChangesAsync();
            var adminId = await CreateUserAsync(ctx, "enum-admin");

            return (other, buyer.PublicCustomerId,
                PrincipalFor(supplierId, (await ctx.Users.FindAsync(supplierId))!.UserName!, "Supplier"),
                PrincipalFor(adminId, (await ctx.Users.FindAsync(adminId))!.UserName!, "Admin"));
        }

        private static string Shape(IActionResult r) => r switch
        {
            RedirectResult x => $"redirect {x.Url}",
            _ => r.GetType().Name,
        };

        [Fact]
        public async Task Company_Edit_Gives_A_Supplier_The_Same_Answer_For_Missing_And_Foreign_Ids()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (other, _, supplier, admin) = await SeedAsync(ctx);

            var foreign = await CompanyEdit(ctx, supplier).OnGetAsync(other.PartyInfoId);
            var missing = await CompanyEdit(ctx, supplier).OnGetAsync(MissingId);
            Assert.IsType<ForbidResult>(foreign);
            Assert.Equal(Shape(foreign), Shape(missing));

            // POST: the posted PartyInfoId is the target; a missing one must not answer differently either.
            var post = CompanyEdit(ctx, supplier);
            post.PartyInfo = new PartyInfo { PartyInfoId = MissingId, CountryCode = other.CountryCode, StateCode = other.StateCode };
            post.ModelState.Clear();
            Assert.IsType<ForbidResult>(await post.OnPostAsync());

            Assert.IsType<NotFoundResult>(await CompanyEdit(ctx, admin).OnGetAsync(MissingId));
        }

        [Fact]
        public async Task Buyer_Details_And_Edit_Give_A_Supplier_The_Same_Answer_For_Missing_And_Foreign_Ids()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (_, otherBuyerId, supplier, admin) = await SeedAsync(ctx);
            var dropdowns = new eInvWorld.Helpers.DropdownHelper(ctx);
            eInvWorld.Pages.PublicCustomer.DetailsModel Details(ClaimsPrincipal u) => As(new eInvWorld.Pages.PublicCustomer.DetailsModel(ctx, dropdowns), u);
            eInvWorld.Pages.PublicCustomer.EditModel Edit(ClaimsPrincipal u) => As(new eInvWorld.Pages.PublicCustomer.EditModel(ctx, dropdowns, Options.Create(new FilePathConfig())), u);

            var detailsForeign = await Details(supplier).OnGetAsync(otherBuyerId);
            Assert.Equal("redirect /Identity/Account/AccessDenied", Shape(detailsForeign));
            Assert.Equal(Shape(detailsForeign), Shape(await Details(supplier).OnGetAsync(MissingId)));

            var editForeign = await Edit(supplier).OnGetAsync(otherBuyerId);
            Assert.Equal("redirect /Identity/Account/AccessDenied", Shape(editForeign));
            Assert.Equal(Shape(editForeign), Shape(await Edit(supplier).OnGetAsync(MissingId)));

            var post = Edit(supplier);
            post.PublicCustomer = new eInvWorld.Models.InputModel.PublicCustomer { PublicCustomerId = MissingId };
            Assert.Equal("redirect /Identity/Account/AccessDenied", Shape(await post.OnPostAsync()));

            Assert.IsType<NotFoundResult>(await Details(admin).OnGetAsync(MissingId));
            Assert.IsType<NotFoundResult>(await Edit(admin).OnGetAsync(MissingId));
        }
    }
}
