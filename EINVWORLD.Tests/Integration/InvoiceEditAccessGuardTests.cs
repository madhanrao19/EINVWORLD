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
    /// OnPostSubmitDocumentsAsync on the same page, and InvoiceDetails2/CreateInvoice/
    /// InvoiceLists elsewhere — so these tests exercise that shared helper directly against real
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

        [Fact]
        public async Task CanManageInvoiceAsync_Allows_Only_The_Issuer_Not_The_Counterparty()
        {
            // Write access (delete from list, later save-draft) must be issuer-only: the buyer can SEE a
            // supplier's draft through CanAccessInvoiceAsync but must not delete it. For self-billed (11-14)
            // the issuer is the customer, so the roles flip.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var supplierCo = await CreateValidPartyInfoAsync(ctx, "Manage Supplier Co");
            var buyerCo = await CreateValidPartyInfoAsync(ctx, "Manage Buyer Co");
            ctx.PartyInfos.AddRange(supplierCo, buyerCo);
            await ctx.SaveChangesAsync();

            var supplierUserId = await CreateUserAsync(ctx, "manage-supplier");
            var buyerUserId = await CreateUserAsync(ctx, "manage-buyer");
            ctx.UserCompanies.Add(new UserCompany { UserId = supplierUserId, PartyInfoId = supplierCo.PartyInfoId, HasCompanyAccess = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = buyerUserId, PartyInfoId = buyerCo.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var status = await SeedStatusAsync(ctx);
            string NewNo() => $"INV-MNG-{Guid.NewGuid():N}"[..20];
            var normalNo = NewNo();
            var selfBilledNo = NewNo();
            foreach (var (no, doc) in new[] { (normalNo, "01"), (selfBilledNo, "11") })
                ctx.InvoiceHeaders.Add(new InvoiceHeader
                {
                    InvoiceNo = no, PrefixedID = no, DocTypeCode = doc, Currency = "MYR",
                    CreatedDate = DateTime.UtcNow, CreatedBy = "integration-test", InternalStatusId = status,
                    SupplierId = supplierCo.PartyInfoId, CustomerId = buyerCo.PartyInfoId,
                });
            await ctx.SaveChangesAsync();

            var supplier = PrincipalFor(supplierUserId, "manage-supplier");
            var buyer = PrincipalFor(buyerUserId, "manage-buyer");

            Assert.True(await UserExtensions.CanAccessInvoiceAsync(buyer, ctx, normalNo));   // can see it...
            Assert.False(await UserExtensions.CanManageInvoiceAsync(buyer, ctx, normalNo));  // ...but not change it
            Assert.True(await UserExtensions.CanManageInvoiceAsync(supplier, ctx, normalNo));

            Assert.True(await UserExtensions.CanManageInvoiceAsync(buyer, ctx, selfBilledNo));
            Assert.False(await UserExtensions.CanManageInvoiceAsync(supplier, ctx, selfBilledNo));
        }

        [Fact]
        public async Task CanManageInvoiceAsync_Never_Grants_Write_Through_A_Shared_General_TIN_Party()
        {
            // General TIN parties (e.g. EI00000000030 Foreign Supplier) are shared by every tenant. A user who
            // happens to be linked to one must not gain write access to every invoice issued under it.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var general = await ctx.PartyInfos.FirstOrDefaultAsync(p => p.TIN == "EI00000000030");
            if (general == null)
            {
                general = await CreateValidPartyInfoAsync(ctx, "Foreign Supplier (general)");
                general.TIN = "EI00000000030";
                ctx.PartyInfos.Add(general);
                await ctx.SaveChangesAsync();
            }
            var userId = await CreateUserAsync(ctx, "general-member");
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = general.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-GEN-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo, PrefixedID = invoiceNo, DocTypeCode = "01", Currency = "MYR",
                CreatedDate = DateTime.UtcNow, CreatedBy = "integration-test", InternalStatusId = status,
                SupplierId = general.PartyInfoId,
            });
            await ctx.SaveChangesAsync();

            var member = PrincipalFor(userId, "general-member");
            Assert.False(await UserExtensions.CanManageInvoiceAsync(member, ctx, invoiceNo));
            Assert.False(await UserExtensions.CanAccessInvoiceAsync(member, ctx, invoiceNo)); // nor read it
            Assert.False(await UserExtensions.OwnsTinAsync(member, ctx, "EI00000000030"));     // nor submit as it
        }

        [Fact]
        public async Task View_Only_Member_Can_See_The_Issuers_Invoice_But_Not_Change_Submit_Or_Cancel_It()
        {
            // Business rule (2026-10-23): view-only members may only view, print and download.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var issuer = await CreateValidPartyInfoAsync(ctx, "ViewOnly Issuer Co");
            ctx.PartyInfos.Add(issuer);
            await ctx.SaveChangesAsync();

            var viewerId = await CreateUserAsync(ctx, "view-only");
            var editorId = await CreateUserAsync(ctx, "full-access");
            ctx.UserCompanies.Add(new UserCompany { UserId = viewerId, PartyInfoId = issuer.PartyInfoId, IsViewOnly = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = editorId, PartyInfoId = issuer.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-VO-{Guid.NewGuid():N}"[..20];
            var uuid = $"VO{Guid.NewGuid():N}"[..26];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo, PrefixedID = invoiceNo, DocTypeCode = "01", Currency = "MYR", UUID = uuid,
                CreatedDate = DateTime.UtcNow, CreatedBy = "integration-test", InternalStatusId = status,
                SupplierId = issuer.PartyInfoId,
            });
            await ctx.SaveChangesAsync();

            var viewer = PrincipalFor(viewerId, "view-only");
            var editor = PrincipalFor(editorId, "full-access");

            Assert.True(await UserExtensions.CanAccessInvoiceAsync(viewer, ctx, invoiceNo));            // view / print / download
            Assert.True(await UserExtensions.CanAccessInvoiceByUuidAsync(viewer, ctx, uuid));
            Assert.False(await UserExtensions.CanManageInvoiceAsync(viewer, ctx, invoiceNo));           // edit / delete / submit
            Assert.False(await UserExtensions.OwnsTinAsync(viewer, ctx, issuer.TIN));                   // submit as the issuer
            Assert.False(await UserExtensions.CanAccessInvoiceByUuidAsync(viewer, ctx, uuid, forWrite: true)); // cancel / reject
            Assert.NotNull(await InvoiceWriteGuard.CheckAsync(viewer, ctx, null!, null, "01", issuer.PartyInfoId, null, null)); // create

            Assert.True(await UserExtensions.CanManageInvoiceAsync(editor, ctx, invoiceNo));
            Assert.True(await UserExtensions.OwnsTinAsync(editor, ctx, issuer.TIN));
            Assert.True(await UserExtensions.CanAccessInvoiceByUuidAsync(editor, ctx, uuid, forWrite: true));
            Assert.Null(await InvoiceWriteGuard.CheckAsync(editor, ctx, null!, null, "01", issuer.PartyInfoId, null, null));
        }

        [Fact]
        public async Task InvoiceWriteGuard_Refuses_Foreign_Ids_And_Allows_The_Users_Own_Draft()
        {
            // Create e-Invoice / Invoice Edit POSTs used to trust the posted InvoiceNo (Save Draft overwrote
            // any invoice with that number), SupplierId and buyer id. InvoiceWriteGuard checks all three.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var own = await CreateValidPartyInfoAsync(ctx, "Guard Own Co");
            var other = await CreateValidPartyInfoAsync(ctx, "Guard Other Co");
            var linkedBuyer = await CreateValidPartyInfoAsync(ctx, "Guard Linked Buyer");
            var strangerBuyer = await CreateValidPartyInfoAsync(ctx, "Guard Stranger Buyer");
            ctx.PartyInfos.AddRange(own, other, linkedBuyer, strangerBuyer);
            await ctx.SaveChangesAsync();
            ctx.SupplierBuyers.Add(new SupplierBuyer { SupplierId = own.PartyInfoId, BuyerId = linkedBuyer.PartyInfoId });

            var userId = await CreateUserAsync(ctx, "guard-user");
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = own.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            if (!await ctx.Set<Status>().AnyAsync(s => s.StatusCode == "Draft"))
                ctx.Set<Status>().Add(new Status { StatusCode = "Draft", StatusType = "Internal", Name = "Draft" });
            if (!await ctx.Set<Status>().AnyAsync(s => s.StatusCode == "Submitted"))
                ctx.Set<Status>().Add(new Status { StatusCode = "Submitted", StatusType = "Internal", Name = "Submitted" });
            await ctx.SaveChangesAsync();

            string NewNo() => $"GRD{Guid.NewGuid():N}"[..18];
            var ownDraft = NewNo(); var ownSubmitted = NewNo(); var otherDraft = NewNo();
            foreach (var (no, supplier, status) in new[] { (ownDraft, own, "Draft"), (ownSubmitted, own, "Submitted"), (otherDraft, other, "Draft") })
                ctx.InvoiceHeaders.Add(new InvoiceHeader
                {
                    InvoiceNo = no, PrefixedID = no, DocTypeCode = "01", Currency = "MYR", CreatedDate = DateTime.UtcNow,
                    CreatedBy = "integration-test", InternalStatusId = status, SupplierId = supplier.PartyInfoId,
                });
            await ctx.SaveChangesAsync();

            var user = PrincipalFor(userId, "guard-user");
            var buyers = new eInvWorld.Services.BuyerService(ctx);
            Task<string?> Check(string? no, int supplierId, int? customerId) =>
                InvoiceWriteGuard.CheckAsync(user, ctx, buyers, no, "01", supplierId, customerId, null);

            Assert.Null(await Check(ownDraft, own.PartyInfoId, linkedBuyer.PartyInfoId));          // own draft, linked buyer
            Assert.Null(await Check(NewNo(), own.PartyInfoId, linkedBuyer.PartyInfoId));           // brand-new number
            Assert.NotNull(await Check(otherDraft, own.PartyInfoId, linkedBuyer.PartyInfoId));     // another company's invoice
            Assert.NotNull(await Check(ownSubmitted, own.PartyInfoId, linkedBuyer.PartyInfoId));   // own, but no longer a draft
            Assert.NotNull(await Check(@"..\..\x", own.PartyInfoId, linkedBuyer.PartyInfoId));     // path traversal
            Assert.NotNull(await Check(NewNo(), other.PartyInfoId, linkedBuyer.PartyInfoId));      // someone else's supplier
            Assert.NotNull(await Check(NewNo(), own.PartyInfoId, strangerBuyer.PartyInfoId));      // buyer not linked
        }

        [Fact]
        public async Task PdfTemplate_Forbids_Another_Companys_Invoice_And_Renders_The_Owners()
        {
            // /Invoices/PdfTemplate?InvoiceNo=... loaded any invoice by its (sequential) number for any Supplier.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var owner = await CreateValidPartyInfoAsync(ctx, "Pdf Owner Co");
            var outsiderCo = await CreateValidPartyInfoAsync(ctx, "Pdf Outsider Co");
            ctx.PartyInfos.AddRange(owner, outsiderCo);
            await ctx.SaveChangesAsync();
            var ownerUserId = await CreateUserAsync(ctx, "pdf-owner");
            var outsiderUserId = await CreateUserAsync(ctx, "pdf-outsider");
            ctx.UserCompanies.Add(new UserCompany { UserId = ownerUserId, PartyInfoId = owner.PartyInfoId, HasCompanyAccess = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = outsiderUserId, PartyInfoId = outsiderCo.PartyInfoId, HasCompanyAccess = true });
            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-PDF-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo, PrefixedID = invoiceNo, DocTypeCode = "01", Currency = "MYR",
                CreatedDate = DateTime.UtcNow, CreatedBy = "integration-test", InternalStatusId = status,
                SupplierId = owner.PartyInfoId,
            });
            await ctx.SaveChangesAsync();

            eInvWorld.Pages.Invoices.PdfTemplateModel PageFor(string userId)
            {
                var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
                var page = new eInvWorld.Pages.Invoices.PdfTemplateModel(ctx,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<eInvWorld.Pages.Invoices.PdfTemplateModel>.Instance,
                    config, new eInvWorld.Services.QRCodeGeneratorService(config), new eInvWorld.Helpers.DropdownHelper(ctx));
                page.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = PrincipalFor(userId, ctx.Users.Find(userId)!.UserName!) }
                };
                return page;
            }

            Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await PageFor(outsiderUserId).OnGetAsync(invoiceNo));

            var ownerPage = PageFor(ownerUserId);
            Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await ownerPage.OnGetAsync(invoiceNo));
            Assert.Equal(invoiceNo, ownerPage.InvoiceDetail.InvoiceNo);
        }

        [Fact]
        public async Task CompanyAuthorization_Refuses_ManageUsers_On_A_Company_The_User_Is_Not_In()
        {
            // Suppliers/Details write handlers now call this with the POSTED company id; before, a supplier
            // could post another company's id to join it or change its members.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var own = await CreateValidPartyInfoAsync(ctx, "Own Co");
            var victim = await CreateValidPartyInfoAsync(ctx, "Victim Co");
            ctx.PartyInfos.AddRange(own, victim);
            await ctx.SaveChangesAsync();
            var userId = await CreateUserAsync(ctx, "company-manager");
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = own.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var auth = new EINVWORLD.Services.Authorization.CompanyAuthorizationService(ctx);
            Assert.True(await auth.HasPermissionAsync(userId, own.PartyInfoId, EINVWORLD.Services.Authorization.CompanyPermission.ManageUsers));
            Assert.False(await auth.HasPermissionAsync(userId, victim.PartyInfoId, EINVWORLD.Services.Authorization.CompanyPermission.ManageUsers));
        }

        [Fact]
        public async Task CompanyAuthorization_View_Only_Wins_Over_A_Role_That_Grants_Edit()
        {
            // Before v1.47.49 a membership with a CompanyRole was judged by the role's flags alone, so a
            // view-only member holding an Editor-type role could still edit the profile and bank accounts.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "Role ViewOnly Co");
            ctx.PartyInfos.Add(company);
            var role = new CompanyRole { Name = $"Editor-{Guid.NewGuid():N}"[..20], CanEditProfile = true, CanViewAudit = true };
            ctx.CompanyRoles.Add(role);
            await ctx.SaveChangesAsync();
            var userId = await CreateUserAsync(ctx, "role-view-only");
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = company.PartyInfoId, HasCompanyAccess = true, IsViewOnly = true, CompanyRoleId = role.CompanyRoleId });
            await ctx.SaveChangesAsync();

            var auth = new EINVWORLD.Services.Authorization.CompanyAuthorizationService(ctx);
            Assert.False(await auth.HasPermissionAsync(userId, company.PartyInfoId, EINVWORLD.Services.Authorization.CompanyPermission.EditProfile));
            Assert.True(await auth.HasPermissionAsync(userId, company.PartyInfoId, EINVWORLD.Services.Authorization.CompanyPermission.ViewAudit));
        }

        [Fact]
        public async Task Cancel_Belongs_To_The_Issuer_And_Reject_To_The_Recipient()
        {
            // InvoiceLists/InvoiceDetails2 cancel used to let the buyer side through (and send the supplier's TIN),
            // and reject used the user's first membership TIN. Both now resolve the parties from the document.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var supplier = await CreateValidPartyInfoAsync(ctx, "Cancel Supplier Co");
            var buyer = await CreateValidPartyInfoAsync(ctx, "Cancel Buyer Co");
            ctx.PartyInfos.AddRange(supplier, buyer);
            await ctx.SaveChangesAsync();
            var supplierUserId = await CreateUserAsync(ctx, "cancel-supplier");
            var buyerUserId = await CreateUserAsync(ctx, "cancel-buyer");
            ctx.UserCompanies.Add(new UserCompany { UserId = supplierUserId, PartyInfoId = supplier.PartyInfoId, HasCompanyAccess = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = buyerUserId, PartyInfoId = buyer.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var status = await SeedStatusAsync(ctx);
            string Add(string docType)
            {
                var no = $"INV-CR-{Guid.NewGuid():N}"[..20];
                ctx.InvoiceHeaders.Add(new InvoiceHeader
                {
                    InvoiceNo = no, PrefixedID = no, DocTypeCode = docType, Currency = "MYR", UUID = $"CR{Guid.NewGuid():N}"[..26],
                    CreatedDate = DateTime.UtcNow, CreatedBy = "integration-test", InternalStatusId = status,
                    SupplierId = supplier.PartyInfoId, CustomerId = buyer.PartyInfoId,
                });
                return no;
            }
            var normalNo = Add("01");
            var selfBilledNo = Add("11");
            await ctx.SaveChangesAsync();
            var normalUuid = await ctx.InvoiceHeaders.Where(i => i.InvoiceNo == normalNo).Select(i => i.UUID!).SingleAsync();
            var selfBilledUuid = await ctx.InvoiceHeaders.Where(i => i.InvoiceNo == selfBilledNo).Select(i => i.UUID!).SingleAsync();

            var supplierUser = PrincipalFor(supplierUserId, "cancel-supplier");
            var buyerUser = PrincipalFor(buyerUserId, "cancel-buyer");

            var normal = (await UserExtensions.GetDocumentPartiesAsync(ctx, normalUuid))!.Value;
            Assert.Equal(supplier.TIN, normal.IssuerTin);
            Assert.Equal(buyer.TIN, normal.RecipientTin);
            Assert.True(await UserExtensions.CanManageInvoiceAsync(supplierUser, ctx, normal.InvoiceNo));  // supplier cancels
            Assert.False(await UserExtensions.CanManageInvoiceAsync(buyerUser, ctx, normal.InvoiceNo));    // buyer may not cancel
            Assert.True(await UserExtensions.OwnsTinAsync(buyerUser, ctx, normal.RecipientTin));           // buyer rejects
            Assert.False(await UserExtensions.OwnsTinAsync(supplierUser, ctx, normal.RecipientTin));       // supplier may not reject

            // Self-billed: the buyer issues, so the roles swap.
            var selfBilled = (await UserExtensions.GetDocumentPartiesAsync(ctx, selfBilledUuid))!.Value;
            Assert.Equal(buyer.TIN, selfBilled.IssuerTin);
            Assert.Equal(supplier.TIN, selfBilled.RecipientTin);
            Assert.True(await UserExtensions.CanManageInvoiceAsync(buyerUser, ctx, selfBilled.InvoiceNo));
            Assert.False(await UserExtensions.CanManageInvoiceAsync(supplierUser, ctx, selfBilled.InvoiceNo));

            Assert.Null(await UserExtensions.GetDocumentPartiesAsync(ctx, "no-such-uuid"));
        }

        [Fact]
        public async Task Recurring_Writes_Are_Refused_For_A_View_Only_Member()
        {
            // The Recurring pages checked view-only on GET only; every POST (toggle/resume, bulk, delete, create, edit)
            // now calls IndexModel.CanWriteAsync.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "Recurring Co");
            ctx.PartyInfos.Add(company);
            await ctx.SaveChangesAsync();
            var viewerId = await CreateUserAsync(ctx, "recurring-viewer");
            var editorId = await CreateUserAsync(ctx, "recurring-editor");
            ctx.UserCompanies.Add(new UserCompany { UserId = viewerId, PartyInfoId = company.PartyInfoId, HasCompanyAccess = true, IsViewOnly = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = editorId, PartyInfoId = company.PartyInfoId, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            Assert.False(await eInvWorld.Pages.RecurringInvoices.IndexModel.CanWriteAsync(ctx, viewerId));
            Assert.True(await eInvWorld.Pages.RecurringInvoices.IndexModel.CanWriteAsync(ctx, editorId));
            Assert.False(await eInvWorld.Pages.RecurringInvoices.IndexModel.CanWriteAsync(ctx, "no-such-user"));
        }
    }
}
