using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Real-SQL-Server tests for the IDOR guard added to Pages/Invoices/InvoiceEdit.cshtml.cs
    /// (OnGetAsync/OnPostAsync) — that page had no ownership check at all: SupplierBasePage's own
    /// authorization tries to parse "id" as a query-string int PartyInfoId, but InvoiceEdit's "id" is a
    /// route-value InvoiceNo string, so the parse always fails and it silently falls back to checking the
    /// CURRENT user's own company instead of the invoice actually being requested. The fix reuses
    /// EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync — the same helper already guarding
    /// OnPostSubmitDocumentsAsync on the same page, and InvoiceDetails2/CreateInvoice/CreateCN/CreateSBCN/
    /// CreateSBI/InvoiceLists elsewhere — so these tests exercise that shared helper directly against real
    /// seeded cross-tenant data, which is exactly what InvoiceEdit's new guards now call.
    /// </summary>
    public class InvoiceEditAccessGuardTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public InvoiceEditAccessGuardTests(SqlServerFixture fx) => _fx = fx;

        private static async Task<PartyInfo> CreateValidPartyInfoAsync(eInvWorld.Data.ApplicationDbContext ctx, string name)
        {
            const string regTypeCode = "TSTREG";
            const string stateCodeValue = "TSTSTATE";
            const string countryCodeValue = "TSTCOUNTRY";

            if (!await ctx.RegistrationTypes.AnyAsync(r => r.Code == regTypeCode))
                ctx.RegistrationTypes.Add(new RegistrationType { Code = regTypeCode, Name = "Test Registration Type" });
            if (!await ctx.StateCodes.AnyAsync(s => s.Code == stateCodeValue))
                ctx.StateCodes.Add(new eInvWorld.Models.StateCode { Code = stateCodeValue, State = "Test State", IsActive = true });
            if (!await ctx.CountryCodes.AnyAsync(c => c.Code == countryCodeValue))
                ctx.CountryCodes.Add(new eInvWorld.Models.CountryCode { Code = countryCodeValue, Country = "Testland", IsActive = true, UpdatedBy = "test" });
            await ctx.SaveChangesAsync();

            return new PartyInfo
            {
                CompanyName = name,
                IndustryClassificationCode = "01111",
                TIN = $"T{Guid.NewGuid():N}"[..14],
                RegTypeCode = regTypeCode,
                RegNo = $"REG{Guid.NewGuid():N}"[..12],
                Addr1 = "1 Test Street",
                CityName = "Test City",
                StateCode = stateCodeValue,
                CountryCode = countryCodeValue,
                PhoneNo = "+60123456789",
                CreatedBy = "test",
            };
        }

        private static async Task<string> CreateUserAsync(eInvWorld.Data.ApplicationDbContext ctx, string label)
        {
            var id = Guid.NewGuid().ToString();
            ctx.Users.Add(new ApplicationUser
            {
                Id = id,
                UserName = $"{label}-{id}@test.local",
                NormalizedUserName = $"{label}-{id}@test.local".ToUpperInvariant(),
                Email = $"{label}-{id}@test.local",
                NormalizedEmail = $"{label}-{id}@test.local".ToUpperInvariant(),
                FullName = label,
                UserType = "Supplier",
            });
            await ctx.SaveChangesAsync();
            return id;
        }

        private static ClaimsPrincipal PrincipalFor(string userId, string userName) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName),
            }, "TestAuth"));

        private static async Task<string> SeedStatusAsync(eInvWorld.Data.ApplicationDbContext ctx)
        {
            var existing = await ctx.Set<Status>().Select(s => s.StatusCode).FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(existing)) return existing;

            var status = new Status { StatusCode = $"IT{Guid.NewGuid():N}"[..20], StatusType = "Internal", Name = "Draft" };
            ctx.Set<Status>().Add(status);
            await ctx.SaveChangesAsync();
            return status.StatusCode;
        }

        [Fact]
        public async Task CanAccessInvoiceAsync_Denies_A_User_From_An_Unrelated_Company()
        {
            // Reproduces the exact exploit InvoiceEdit's missing guard allowed: a Supplier user who
            // belongs to Company B, given the InvoiceNo of a draft that belongs to Company A only,
            // must be denied — this is the check InvoiceEdit.OnGetAsync/OnPostAsync now call.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var companyA = await CreateValidPartyInfoAsync(ctx, "IDOR Owner Co");
            var companyB = await CreateValidPartyInfoAsync(ctx, "IDOR Outsider Co");
            ctx.PartyInfos.AddRange(companyA, companyB);
            await ctx.SaveChangesAsync();

            var outsiderUserId = await CreateUserAsync(ctx, "outsider");
            var outsiderUserName = (await ctx.Users.FindAsync(outsiderUserId))!.UserName!;
            ctx.UserCompanies.Add(new UserCompany { UserId = outsiderUserId, PartyInfoId = companyB.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-IDOR-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo,
                PrefixedID = invoiceNo,
                DocTypeCode = "01",
                Currency = "MYR",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test",
                InternalStatusId = status,
                SupplierId = companyA.PartyInfoId, // owned by Company A only
            });
            await ctx.SaveChangesAsync();

            var outsider = PrincipalFor(outsiderUserId, outsiderUserName);
            Assert.False(await UserExtensions.CanAccessInvoiceAsync(outsider, ctx, invoiceNo));
        }

        [Fact]
        public async Task CanAccessInvoiceAsync_Allows_The_Owning_Supplier_And_The_Self_Billed_Customer()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var supplierCo = await CreateValidPartyInfoAsync(ctx, "IDOR Supplier Co");
            var customerCo = await CreateValidPartyInfoAsync(ctx, "IDOR Customer Co");
            ctx.PartyInfos.AddRange(supplierCo, customerCo);
            await ctx.SaveChangesAsync();

            var supplierUserId = await CreateUserAsync(ctx, "supplier-owner");
            var supplierUserName = (await ctx.Users.FindAsync(supplierUserId))!.UserName!;
            ctx.UserCompanies.Add(new UserCompany { UserId = supplierUserId, PartyInfoId = supplierCo.PartyInfoId, HasCompanyAccess = true });

            // A self-billed invoice's effective issuer is the CUSTOMER party (mirrors CreateInvoice.cshtml.cs's
            // own DocTypeCode 11-14 TIN-selection logic) — CanAccessInvoiceAsync must grant that party access
            // too, which is exactly why InvoiceEdit reuses this helper rather than a supplier-only check.
            var customerUserId = await CreateUserAsync(ctx, "self-billed-customer");
            var customerUserName = (await ctx.Users.FindAsync(customerUserId))!.UserName!;
            ctx.UserCompanies.Add(new UserCompany { UserId = customerUserId, PartyInfoId = customerCo.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-IDOR-SB-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo,
                PrefixedID = invoiceNo,
                DocTypeCode = "11", // self-billed invoice
                Currency = "MYR",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test",
                InternalStatusId = status,
                SupplierId = supplierCo.PartyInfoId,
                CustomerId = customerCo.PartyInfoId,
            });
            await ctx.SaveChangesAsync();

            var supplierPrincipal = PrincipalFor(supplierUserId, supplierUserName);
            var customerPrincipal = PrincipalFor(customerUserId, customerUserName);
            Assert.True(await UserExtensions.CanAccessInvoiceAsync(supplierPrincipal, ctx, invoiceNo));
            Assert.True(await UserExtensions.CanAccessInvoiceAsync(customerPrincipal, ctx, invoiceNo));
        }

        [Fact]
        public async Task CanAccessInvoiceAsync_Returns_False_For_A_Not_Yet_Existing_InvoiceNo()
        {
            // Confirms InvoiceEdit.OnPostAsync's precondition is correct: the guard there only calls
            // CanAccessInvoiceAsync when an InvoiceHeader with this InvoiceNo already exists — because the
            // helper itself returns false for a non-existent invoice, applying it unconditionally would
            // have broken the legitimate "first save of a brand-new invoice" flow (a pre-generated
            // InvoiceNo that doesn't exist in the database yet).
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "IDOR New-Invoice Co");
            ctx.PartyInfos.Add(company);
            await ctx.SaveChangesAsync();
            var userId = await CreateUserAsync(ctx, "new-invoice-user");
            var userName = (await ctx.Users.FindAsync(userId))!.UserName!;
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = company.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var principal = PrincipalFor(userId, userName);
            var neverSavedInvoiceNo = $"INV-NEW-{Guid.NewGuid():N}"[..20];
            Assert.False(await UserExtensions.CanAccessInvoiceAsync(principal, ctx, neverSavedInvoiceNo));
        }
    }
}
