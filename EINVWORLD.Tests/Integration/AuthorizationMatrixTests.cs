using System;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.Templates;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// The invoice authorization matrix as one table, so a change that reopens a hole fails by actor and action.
    /// Each row seeds an issuer and a recipient company and checks the shared helpers every handler calls:
    /// View/Download = CanAccessInvoiceAsync; Edit/Submit/Cancel/Resolve = CanManageInvoiceAsync;
    /// Reject = OwnsTinAsync(recipient TIN); Recurring write = RecurringInvoices.IndexModel.CanWriteTemplateAsync.
    /// For self-billed type 11 the customer issues, so the parties are seeded the other way round and the
    /// expected row is the same. Shared general-TIN parties are covered in InvoiceEditAccessGuardTests.
    /// shortcut: asserts the helpers, not each Razor handler's wiring; add handler tests if a handler stops calling its helper.
    /// </summary>
    public class AuthorizationMatrixTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public AuthorizationMatrixTests(SqlServerFixture fx) => _fx = fx;

        [Theory]
        //           actor                docType  view   manage reject recurring
        [InlineData("IssuerEditor",      "01",    true,  true,  false, true)]
        [InlineData("IssuerViewOnly",    "01",    true,  false, false, false)]
        [InlineData("RecipientEditor",   "01",    true,  false, true,  false)]
        [InlineData("RecipientViewOnly", "01",    true,  false, false, false)]
        [InlineData("Outsider",          "01",    false, false, false, false)]
        [InlineData("Admin",             "01",    true,  true,  true,  null)]
        [InlineData("IssuerEditor",      "11",    true,  true,  false, true)]
        [InlineData("IssuerViewOnly",    "11",    true,  false, false, false)]
        [InlineData("RecipientEditor",   "11",    true,  false, true,  false)]
        [InlineData("RecipientViewOnly", "11",    true,  false, false, false)]
        [InlineData("Outsider",          "11",    false, false, false, false)]
        [InlineData("Admin",             "11",    true,  true,  true,  null)]
        public async Task Invoice_Permissions_Follow_The_Matrix(string actor, string docType, bool view, bool manage, bool reject, bool? recurring)
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var issuer = await CreateValidPartyInfoAsync(ctx, "Matrix Issuer Co");
            var recipient = await CreateValidPartyInfoAsync(ctx, "Matrix Recipient Co");
            var outsiderCo = await CreateValidPartyInfoAsync(ctx, "Matrix Outsider Co");
            ctx.PartyInfos.AddRange(issuer, recipient, outsiderCo);
            await ctx.SaveChangesAsync();

            var userId = await CreateUserAsync(ctx, $"matrix-{actor}");
            var user = (await ctx.Users.FindAsync(userId))!;
            var membership = actor switch
            {
                "IssuerEditor" => new UserCompany { PartyInfoId = issuer.PartyInfoId, HasCompanyAccess = true },
                "IssuerViewOnly" => new UserCompany { PartyInfoId = issuer.PartyInfoId, IsViewOnly = true },
                "RecipientEditor" => new UserCompany { PartyInfoId = recipient.PartyInfoId, HasCompanyAccess = true },
                "RecipientViewOnly" => new UserCompany { PartyInfoId = recipient.PartyInfoId, IsViewOnly = true },
                "Outsider" => new UserCompany { PartyInfoId = outsiderCo.PartyInfoId, HasCompanyAccess = true },
                "Admin" => null,
                _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, null)
            };
            if (membership != null) { membership.UserId = userId; ctx.UserCompanies.Add(membership); }
            if (actor == "Admin") user.UserType = "Admin";

            // Self-billed: the customer is the issuer, the supplier receives.
            bool selfBilled = TinHelper.IsSelfBilledDocType(docType);
            int supplierId = selfBilled ? recipient.PartyInfoId : issuer.PartyInfoId;
            int customerId = selfBilled ? issuer.PartyInfoId : recipient.PartyInfoId;

            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-MX-{Guid.NewGuid():N}"[..20];
            var uuid = $"MX{Guid.NewGuid():N}"[..26];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo, PrefixedID = invoiceNo, DocTypeCode = docType, Currency = "MYR", UUID = uuid,
                CreatedDate = DateTime.UtcNow, CreatedBy = "integration-test", InternalStatusId = status,
                SupplierId = supplierId, CustomerId = customerId,
            });
            var template = new InvoiceTemplate { TemplateName = "matrix", DocTypeCode = docType, SupplierId = supplierId, CustomerId = customerId };
            ctx.InvoiceTemplates.Add(template);
            await ctx.SaveChangesAsync();

            var principal = PrincipalFor(userId, user.UserName!);
            var parties = (await UserExtensions.GetDocumentPartiesAsync(ctx, uuid))!.Value;

            // Cancel goes out under the issuer's TIN and reject under the recipient's, whoever clicks.
            Assert.Equal(issuer.TIN, parties.IssuerTin);
            Assert.Equal(recipient.TIN, parties.RecipientTin);

            Assert.Equal(view, await UserExtensions.CanAccessInvoiceAsync(principal, ctx, invoiceNo));
            Assert.Equal(manage, await UserExtensions.CanManageInvoiceAsync(principal, ctx, invoiceNo));
            Assert.Equal(reject, await UserExtensions.OwnsTinAsync(principal, ctx, parties.RecipientTin));
            // Admin's recurring bypass lives in the page handlers and the worker, not in this helper.
            if (recurring.HasValue)
                Assert.Equal(recurring.Value, await eInvWorld.Pages.RecurringInvoices.IndexModel.CanWriteTemplateAsync(ctx, userId, template.Id));
        }

        [Fact]
        public async Task Dashboard_Company_Scope_Ignores_A_Shared_General_Tin_Membership()
        {
            // Dashboard counts invoices by GetUserCompanyIds; a membership of a general-TIN party (on every tenant's
            // invoices) used to pull other tenants' invoices into the user's counts.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var own = await CreateValidPartyInfoAsync(ctx, "Scope Own Co");
            ctx.PartyInfos.Add(own);
            var general = await ctx.PartyInfos.FirstOrDefaultAsync(pi => pi.TIN == "EI00000000010");
            if (general == null)
            {
                general = await CreateValidPartyInfoAsync(ctx, "General Public");
                general.TIN = "EI00000000010";
                ctx.PartyInfos.Add(general);
            }
            await ctx.SaveChangesAsync();
            var userId = await CreateUserAsync(ctx, "scope-user");
            var user = (await ctx.Users.FindAsync(userId))!;
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = own.PartyInfoId, HasCompanyAccess = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = general.PartyInfoId });
            await ctx.SaveChangesAsync();

            var ids = PrincipalFor(userId, user.UserName!).GetUserCompanyIds(ctx);
            Assert.Equal(new[] { own.PartyInfoId }, ids);
        }

        [Fact]
        public async Task Background_Submit_Refuses_A_Draft_That_Is_Not_Ready_And_Is_Never_Retried()
        {
            // Job submits (retries, recurring and Smart Capture auto-submit) run the same Ready-for-LHDN check as
            // the Submit buttons, before any JSON, token or LHDN call; the failure is non-retryable.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var issuer = await CreateValidPartyInfoAsync(ctx, "Ready Issuer Co");
            ctx.PartyInfos.Add(issuer);
            if (!await ctx.Set<Status>().AnyAsync(s => s.StatusCode == "Draft"))
                ctx.Set<Status>().Add(new Status { StatusCode = "Draft", StatusType = "Internal", Name = "Draft" });
            await ctx.SaveChangesAsync();
            var invoiceNo = $"INV-RD-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo, PrefixedID = invoiceNo, DocTypeCode = "01", Currency = "MYR",
                CreatedDate = DateTime.UtcNow, CreatedBy = "integration-test", InternalStatusId = "Draft",
                SupplierId = issuer.PartyInfoId, // no buyer, no lines, no issue date
            });
            await ctx.SaveChangesAsync();

            var items = await InvoiceReadinessCheck.CheckAsync(ctx, invoiceNo);
            Assert.NotNull(items);
            Assert.Contains(items!, i => i.Section == InvoiceReadinessCheck.Buyer && i.Blocking);
            Assert.Contains(items!, i => i.Section == InvoiceReadinessCheck.Items && i.Blocking);
            Assert.Null(await InvoiceReadinessCheck.CheckAsync(ctx, "no-such-invoice"));

            var helper = new InvoiceSubmissionHelper(ctx, null!, null!, null!,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceSubmissionHelper>.Instance, null!);
            var ex = await Assert.ThrowsAsync<SubmissionBlockedException>(() => helper.SubmitInvoiceAsync(invoiceNo));
            Assert.Contains("Not ready for LHDN", ex.Message);
            Assert.False(EINVWORLD.Services.Background.DurableSyncJobWorker.ShouldRetry(1, 3, ex));
        }
    }
}
