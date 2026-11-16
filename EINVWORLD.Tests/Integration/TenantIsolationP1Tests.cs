using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.Auth;
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
    /// Security pass 1 (FINDINGS.md #3, #6–#8, #28): negative cross-tenant cases, run against the real page
    /// handlers on SQL Server. "Owner" is a full member (legacy HasCompanyAccess, so ManageUsers and EditProfile)
    /// of company A and no one's Admin; B and C are other tenants.
    /// </summary>
    public class TenantIsolationP1Tests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public TenantIsolationP1Tests(SqlServerFixture fx) => _fx = fx;

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

        private eInvWorld.Pages.Suppliers.DetailsModel Details(eInvWorld.Data.ApplicationDbContext ctx, ClaimsPrincipal user) =>
            As(new eInvWorld.Pages.Suppliers.DetailsModel(ctx, Users(ctx), NullLogger<eInvWorld.Pages.Suppliers.DetailsModel>.Instance,
                new CompanyAuthorizationService(ctx)), user);

        private async Task<(PartyInfo a, PartyInfo b, string ownerId, ClaimsPrincipal owner)> SeedAsync(eInvWorld.Data.ApplicationDbContext ctx, string label)
        {
            var a = await CreateValidPartyInfoAsync(ctx, $"Iso {label} A");
            var b = await CreateValidPartyInfoAsync(ctx, $"Iso {label} B");
            ctx.PartyInfos.AddRange(a, b);
            await ctx.SaveChangesAsync();
            var ownerId = await CreateUserAsync(ctx, $"iso-{label}-owner");
            ctx.UserCompanies.Add(new UserCompany { UserId = ownerId, PartyInfoId = a.PartyInfoId, HasCompanyAccess = true, IsPrimaryCompany = true });
            await ctx.SaveChangesAsync();
            var owner = PrincipalFor(ownerId, (await ctx.Users.FindAsync(ownerId))!.UserName!, "Supplier");
            return (a, b, ownerId, owner);
        }

        [Fact]
        public async Task Owner_Cannot_Link_Another_Tenant_As_Buyer_But_Admin_Can()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b, _, owner) = await SeedAsync(ctx, "link");

            var page = Details(ctx, owner);
            page.SelectedBuyerIds = new List<int> { b.PartyInfoId };
            Assert.IsType<ForbidResult>(await page.OnPostAssignBuyerAsync(a.PartyInfoId));
            Assert.False(await ctx.SupplierBuyers.AnyAsync(sb => sb.SupplierId == a.PartyInfoId && sb.BuyerId == b.PartyInfoId));

            var adminId = await CreateUserAsync(ctx, "iso-link-admin");
            var admin = Details(ctx, PrincipalFor(adminId, "iso-link-admin", "Admin"));
            admin.SelectedBuyerIds = new List<int> { b.PartyInfoId };
            await admin.OnPostAssignBuyerAsync(a.PartyInfoId);
            Assert.True(await ctx.SupplierBuyers.AnyAsync(sb => sb.SupplierId == a.PartyInfoId && sb.BuyerId == b.PartyInfoId));
        }

        [Fact]
        public async Task Owner_Cannot_Add_A_Member_By_Email_And_Learns_Nothing_About_The_Address()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, _, _, owner) = await SeedAsync(ctx, "add");
            var victimId = await CreateUserAsync(ctx, "iso-add-victim");
            var victimEmail = (await ctx.Users.FindAsync(victimId))!.Email ?? "victim@example.com";

            var existing = (JsonResult)await Details(ctx, owner).OnPostAssignUserAsync(a.PartyInfoId, null, victimEmail);
            var unknown = (JsonResult)await Details(ctx, owner).OnPostAssignUserAsync(a.PartyInfoId, null, "nobody-" + Guid.NewGuid().ToString("N") + "@example.com");

            Assert.False(await ctx.UserCompanies.AnyAsync(uc => uc.UserId == victimId && uc.PartyInfoId == a.PartyInfoId));
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(existing.Value), System.Text.Json.JsonSerializer.Serialize(unknown.Value));
        }

        [Fact]
        public async Task Manager_Cannot_Change_Another_Users_Primary_Company()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b, _, owner) = await SeedAsync(ctx, "primary");
            var memberId = await CreateUserAsync(ctx, "iso-primary-member");
            ctx.UserCompanies.Add(new UserCompany { UserId = memberId, PartyInfoId = b.PartyInfoId, HasCompanyAccess = true, IsPrimaryCompany = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = memberId, PartyInfoId = a.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            await Details(ctx, owner).OnPostSetPrimaryCompanyAsync(memberId, a.PartyInfoId, true);

            ctx.ChangeTracker.Clear();
            Assert.True(await ctx.UserCompanies.AnyAsync(uc => uc.UserId == memberId && uc.PartyInfoId == b.PartyInfoId && uc.IsPrimaryCompany));
            Assert.False(await ctx.UserCompanies.AnyAsync(uc => uc.UserId == memberId && uc.PartyInfoId == a.PartyInfoId && uc.IsPrimaryCompany));
        }

        [Fact]
        public async Task Owner_Cannot_Assign_Another_Companys_Custom_Role()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b, ownerId, owner) = await SeedAsync(ctx, "role");
            var foreignRole = new CompanyRole { Name = "B only " + Guid.NewGuid().ToString("N")[..6], PartyInfoId = b.PartyInfoId, CanManageUsers = true, CanEditProfile = true };
            ctx.CompanyRoles.Add(foreignRole);
            await ctx.SaveChangesAsync();
            var membership = await ctx.UserCompanies.FirstAsync(uc => uc.UserId == ownerId && uc.PartyInfoId == a.PartyInfoId);

            var page = As(new eInvWorld.Pages.Suppliers.RolesPermissionsModel(ctx, Users(ctx), new CompanyAuthorizationService(ctx), null!), owner);
            await page.OnPostAssignRoleAsync(a.PartyInfoId, membership.Id, foreignRole.CompanyRoleId);

            ctx.ChangeTracker.Clear();
            Assert.Null((await ctx.UserCompanies.FindAsync(membership.Id))!.CompanyRoleId);
        }

        [Fact]
        public async Task Only_An_Admin_May_Change_A_Company_TIN_And_Not_Once_It_Is_On_Any_Invoice()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b, _, _) = await SeedAsync(ctx, "tin");

            Assert.True(await eInvWorld.Pages.Suppliers.EditModel.IsTinLockedAsync(ctx, a.PartyInfoId, isAdmin: false));  // fresh company, member
            Assert.False(await eInvWorld.Pages.Suppliers.EditModel.IsTinLockedAsync(ctx, a.PartyInfoId, isAdmin: true));  // fresh company, Admin

            // A appears on B's invoice as the customer only: the old rule (supplier invoices) left it unlocked.
            var status = await SeedStatusAsync(ctx);
            var no = $"INV-ISO-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = no, PrefixedID = no, DocTypeCode = "01", Currency = "MYR", CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test", InternalStatusId = status, SupplierId = b.PartyInfoId, CustomerId = a.PartyInfoId,
            });
            await ctx.SaveChangesAsync();
            Assert.True(await eInvWorld.Pages.Suppliers.EditModel.IsTinLockedAsync(ctx, a.PartyInfoId, isAdmin: true));
        }

        [Fact]
        public async Task Invoice_Admin_Needs_Both_The_Admin_Role_And_The_Admin_UserType()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (_, b, _, _) = await SeedAsync(ctx, "admin");
            var status = await SeedStatusAsync(ctx);
            var no = $"INV-ADM-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = no, PrefixedID = no, DocTypeCode = "01", Currency = "MYR", CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test", InternalStatusId = status, SupplierId = b.PartyInfoId,
            });
            var typeOnlyId = await CreateUserAsync(ctx, "iso-admin-typeonly");
            (await ctx.Users.FindAsync(typeOnlyId))!.UserType = "Admin";
            var roleOnlyId = await CreateUserAsync(ctx, "iso-admin-roleonly");
            var bothId = await CreateUserAsync(ctx, "iso-admin-both");
            (await ctx.Users.FindAsync(bothId))!.UserType = "Admin";
            await ctx.SaveChangesAsync();

            // An outsider to B's invoice in every case except a real Admin (role and user type agree).
            Assert.False(await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(PrincipalFor(typeOnlyId, (await ctx.Users.FindAsync(typeOnlyId))!.UserName!), ctx, no));
            Assert.False(await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(PrincipalFor(roleOnlyId, (await ctx.Users.FindAsync(roleOnlyId))!.UserName!, "Admin"), ctx, no));
            Assert.True(await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(PrincipalFor(bothId, (await ctx.Users.FindAsync(bothId))!.UserName!, "Admin"), ctx, no));
        }

        [Fact]
        public async Task Outsider_Cannot_Base_An_Adjustment_On_Another_Tenants_Invoice()
        {
            // CreateInvoice's ?type=CN&uuid= path refuses unless CanManageInvoiceAsync; recipient and outsider are refused.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b, _, owner) = await SeedAsync(ctx, "adj");
            var status = await SeedStatusAsync(ctx);
            var no = $"INV-ADJ-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = no, PrefixedID = no, DocTypeCode = "01", Currency = "MYR", CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test", InternalStatusId = status, SupplierId = b.PartyInfoId, CustomerId = a.PartyInfoId,
                UUID = $"ADJ{Guid.NewGuid():N}"[..26],
            });
            await ctx.SaveChangesAsync();
            Assert.False(await EINVWORLD.Helpers.UserExtensions.CanManageInvoiceAsync(owner, ctx, no)); // A only receives it
        }

        [Fact]
        public async Task Membership_Of_A_General_Tin_Party_Or_A_View_Only_Membership_Owns_Nothing()
        {
            // CreateInvoice's GetInvoicesForReference relies on OwnsTinAsync: a (legacy) membership of the shared
            // "General Public" party must not list every tenant's invoices issued under it; view-only can't issue.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (a, b, ownerId, owner) = await SeedAsync(ctx, "gentin");
            b.TIN = "EI00000000010";
            ctx.UserCompanies.Add(new UserCompany { UserId = ownerId, PartyInfoId = b.PartyInfoId, HasCompanyAccess = true });
            var viewerId = await CreateUserAsync(ctx, "iso-gentin-viewer");
            ctx.UserCompanies.Add(new UserCompany { UserId = viewerId, PartyInfoId = a.PartyInfoId, IsViewOnly = true });
            await ctx.SaveChangesAsync();
            var viewer = PrincipalFor(viewerId, (await ctx.Users.FindAsync(viewerId))!.UserName!, "Supplier");

            Assert.False(await EINVWORLD.Helpers.UserExtensions.OwnsTinAsync(owner, ctx, b.TIN));
            Assert.True(await EINVWORLD.Helpers.UserExtensions.OwnsTinAsync(owner, ctx, a.TIN));
            Assert.False(await EINVWORLD.Helpers.UserExtensions.OwnsTinAsync(viewer, ctx, a.TIN));
        }

        [Fact]
        public async Task Supplier_Cannot_Open_Or_Post_Company_Create()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (_, _, _, owner) = await SeedAsync(ctx, "create");
            int before = await ctx.PartyInfos.CountAsync();

            var page = As(new eInvWorld.Pages.Suppliers.CreateModel(ctx, null!, null!, Options.Create(new FilePathConfig()), null!, null!,
                NullLogger<eInvWorld.Pages.Suppliers.CreateModel>.Instance, null!), owner);
            page.PartyInfo = new PartyInfo { CompanyName = "Reserved", TIN = "C9999999999", CreatedBy = "x" };

            Assert.IsType<ForbidResult>(page.OnGet("lead"));
            Assert.IsType<ForbidResult>(await page.OnPostAsync("lead"));
            Assert.Equal(before, await ctx.PartyInfos.CountAsync());
        }
    }
}
