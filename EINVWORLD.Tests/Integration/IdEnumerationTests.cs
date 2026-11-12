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
using Microsoft.EntityFrameworkCore;
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
            (await ctx.Users.FindAsync(adminId))!.UserType = "Admin"; // a real Admin: role and UserType (UserExtensions.IsAdmin)
            await ctx.SaveChangesAsync();

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
        // ── v1.50.50: Items and invoice pages ───────────────────────────────────────────────────────────
        private static async Task<(int itemId, string invoiceNo, ClaimsPrincipal supplier, ClaimsPrincipal admin)> SeedForeignItemAndInvoiceAsync(eInvWorld.Data.ApplicationDbContext ctx)
        {
            var (other, _, supplier, admin) = await SeedAsync(ctx);
            var item = new ItemDescription { ClassificationCode = "022", ItemCode = "ENUM-1", Description = "other's item", CreatedByCompanyId = other.PartyInfoId };
            ctx.ItemDescriptions.Add(item);
            if (!await ctx.Set<Status>().AnyAsync(s => s.StatusCode == "Draft"))
                ctx.Set<Status>().Add(new Status { StatusCode = "Draft", StatusType = "Internal", Name = "Draft" });
            var invoiceNo = $"ENM{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo, PrefixedID = invoiceNo, DocTypeCode = "01", Currency = "MYR", CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test", InternalStatusId = "Draft", SupplierId = other.PartyInfoId,
            });
            await ctx.SaveChangesAsync();
            return (item.Id, invoiceNo, supplier, admin);
        }

        [Fact]
        public async Task Item_Edit_And_Delete_Give_A_Supplier_The_Same_Answer_For_Missing_And_Foreign_Ids()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (itemId, _, supplier, admin) = await SeedForeignItemAndInvoiceAsync(ctx);
            var dropdowns = new eInvWorld.Helpers.DropdownHelper(ctx);
            eInvWorld.Pages.Items.EditModel Edit(ClaimsPrincipal u) => As(new eInvWorld.Pages.Items.EditModel(ctx, dropdowns), u);
            eInvWorld.Pages.Items.DeleteModel Delete(ClaimsPrincipal u) => As(new eInvWorld.Pages.Items.DeleteModel(ctx), u);
            const string denied = "redirect /Identity/Account/AccessDenied";

            Assert.Equal(denied, Shape(await Edit(supplier).OnGetAsync(itemId)));
            Assert.Equal(denied, Shape(await Edit(supplier).OnGetAsync(MissingId)));
            Assert.Equal(denied, Shape(await Delete(supplier).OnGetAsync(itemId)));
            Assert.Equal(denied, Shape(await Delete(supplier).OnGetAsync(MissingId)));

            var postForeign = Edit(supplier);
            postForeign.ItemDescription = new ItemDescription { Id = itemId, ClassificationCode = "022", ItemCode = "X", Description = "overwrite" };
            Assert.Equal(denied, Shape(await postForeign.OnPostAsync()));
            var postMissing = Edit(supplier);
            postMissing.ItemDescription = new ItemDescription { Id = MissingId, ClassificationCode = "022", ItemCode = "X", Description = "x" };
            Assert.Equal(denied, Shape(await postMissing.OnPostAsync()));

            Assert.Equal(denied, Shape(await Delete(supplier).OnPostAsync(itemId)));
            Assert.Equal(denied, Shape(await Delete(supplier).OnPostAsync(MissingId)));
            Assert.Equal("other's item", (await ctx.ItemDescriptions.AsNoTracking().SingleAsync(i => i.Id == itemId)).Description); // untouched

            // Admin: missing is still 404 on the pages, and deleting a missing id still just returns to the list.
            Assert.IsType<NotFoundResult>(await Edit(admin).OnGetAsync(MissingId));
            Assert.IsType<NotFoundResult>(await Delete(admin).OnGetAsync(MissingId));
            Assert.IsType<RedirectToPageResult>(await Delete(admin).OnPostAsync(MissingId));
            Assert.IsType<PageResult>(await Edit(admin).OnGetAsync(itemId));
        }

        [Fact]
        public async Task Invoice_Edit_And_Details_Give_A_Supplier_The_Same_Answer_For_Missing_And_Foreign_Invoices()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (_, invoiceNo, supplier, admin) = await SeedForeignItemAndInvoiceAsync(ctx);
            var missingNo = $"NOPE{Guid.NewGuid():N}"[..20];
            EINVWORLD.Pages.Invoices.InvoiceDetails2Model Details(ClaimsPrincipal u) => As(new EINVWORLD.Pages.Invoices.InvoiceDetails2Model(
                ctx, NullLogger<eInvWorld.Pages.Invoices.InvoiceDetailsModel>.Instance, null!, null!, null!, null!, null!, null!, null!, null!, null!), u);

            Assert.IsType<ForbidResult>(await Details(supplier).OnGetAsync(invoiceNo));
            Assert.IsType<ForbidResult>(await Details(supplier).OnGetAsync(missingNo));
            Assert.IsType<ForbidResult>(await InvoiceEditLockedRedirectTests.Page(ctx, supplier, "GET").OnGetAsync(invoiceNo));
            Assert.IsType<ForbidResult>(await InvoiceEditLockedRedirectTests.Page(ctx, supplier, "GET").OnGetAsync(missingNo));

            // Admin role without UserType "Admin" is not an Admin to CanAccessInvoiceAsync, so it must not get 404 either.
            var roleOnlyId = await CreateUserAsync(ctx, "enum-role-only-admin");
            var roleOnly = PrincipalFor(roleOnlyId, (await ctx.Users.FindAsync(roleOnlyId))!.UserName!, "Admin");
            Assert.IsType<ForbidResult>(await Details(roleOnly).OnGetAsync(missingNo));
            Assert.IsType<ForbidResult>(await InvoiceEditLockedRedirectTests.Page(ctx, roleOnly, "GET").OnGetAsync(missingNo));

            Assert.IsType<NotFoundResult>(await Details(admin).OnGetAsync(missingNo));
            Assert.IsType<NotFoundObjectResult>(await InvoiceEditLockedRedirectTests.Page(ctx, admin, "GET").OnGetAsync(missingNo));
        }
    }
}

