using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;

using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Http;
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
    /// v1.50.46: opening Invoice Edit by URL for an invoice Save Draft would refuse (Valid, Submitted, Cancelled, ...)
    /// goes to Invoice Details instead of showing a form that can never be saved. Runs the real OnGetAsync on SQL Server.
    /// The save/submit guards themselves are unchanged (InvoiceWriteGuard, the UUID double-submit check).
    /// </summary>
    public class InvoiceEditLockedRedirectTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public InvoiceEditLockedRedirectTests(SqlServerFixture fx) => _fx = fx;

        private sealed class NoTempData : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static eInvWorld.Pages.Invoices.InvoiceEditModel Page(eInvWorld.Data.ApplicationDbContext ctx, ClaimsPrincipal user, string method)
        {
            // Only what OnGetAsync touches before the status check is real; the rest is never reached on a redirect.
            var page = new eInvWorld.Pages.Invoices.InvoiceEditModel(null!, ctx, null!, ImportLogMaskingTests.NullProxy.Create<ILHDNApiService>(),
                NullLogger<EINVWORLD.Pages.Invoices.CreateInvoiceModel>.Instance, Options.Create(new FilePathConfig()), null!, null!, null!, null!,
                null!, null!, null!, null!, null!, null!);
            var http = new DefaultHttpContext { User = user };
            http.Request.Method = method;
            page.PageContext = new PageContext { HttpContext = http };
            page.TempData = new TempDataDictionary(http, new NoTempData());
            return page;
        }

        private static async Task EnsureStatusAsync(eInvWorld.Data.ApplicationDbContext ctx, string code)
        {
            if (await ctx.Set<Status>().AnyAsync(s => s.StatusCode == code)) return;
            ctx.Set<Status>().Add(new Status { StatusCode = code, StatusType = "Internal", Name = code });
            await ctx.SaveChangesAsync();
        }

        private static async Task<(string invoiceNo, ClaimsPrincipal owner, ClaimsPrincipal outsider)> SeedAsync(
            eInvWorld.Data.ApplicationDbContext ctx, string status, string docType, string? uuid)
        {
            await EnsureStatusAsync(ctx, status);
            var mine = await CreateValidPartyInfoAsync(ctx, "Edit Lock Owner Co");
            var other = await CreateValidPartyInfoAsync(ctx, "Edit Lock Other Co");
            ctx.PartyInfos.AddRange(mine, other);
            await ctx.SaveChangesAsync();

            var ownerId = await CreateUserAsync(ctx, "edit-lock-owner");
            var outsiderId = await CreateUserAsync(ctx, "edit-lock-outsider");
            ctx.UserCompanies.Add(new UserCompany { UserId = ownerId, PartyInfoId = mine.PartyInfoId, HasCompanyAccess = true, IsPrimaryCompany = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = outsiderId, PartyInfoId = other.PartyInfoId, HasCompanyAccess = true, IsPrimaryCompany = true });

            var invoiceNo = $"ELK{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo,
                PrefixedID = invoiceNo,
                DocTypeCode = docType,
                Currency = "MYR",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test",
                InternalStatusId = status,
                UUID = uuid,
                SupplierId = mine.PartyInfoId,
            });
            await ctx.SaveChangesAsync();

            return (invoiceNo,
                PrincipalFor(ownerId, (await ctx.Users.FindAsync(ownerId))!.UserName!, "Supplier"),
                PrincipalFor(outsiderId, (await ctx.Users.FindAsync(outsiderId))!.UserName!, "Supplier"));
        }

        [Theory]
        // status             docType  (01 invoice, 02 credit note, 03 debit note, 04 refund note)
        [InlineData("Valid",     "01")]
        [InlineData("Invalid",   "01")]
        [InlineData("Cancelled", "01")]
        [InlineData("Submitted", "01")]
        [InlineData("Valid",     "02")]
        [InlineData("Valid",     "03")]
        [InlineData("Valid",     "04")]
        [InlineData("Cancelled", "02")]
        public async Task Locked_Status_Get_Redirects_The_Owner_To_Details(string status, string docType)
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var uuid = $"U{Guid.NewGuid():N}"[..26].ToUpperInvariant();
            var (invoiceNo, owner, _) = await SeedAsync(ctx, status, docType, uuid);

            var result = await Page(ctx, owner, "GET").OnGetAsync(invoiceNo);

            var redirect = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("/Invoices/InvoiceDetails2", redirect.PageName);
            Assert.Equal(uuid, redirect.RouteValues!["uuid"]);
        }

        [Fact]
        public async Task Locked_Invoice_Without_A_Uuid_Redirects_To_Details_By_Invoice_Number()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (invoiceNo, owner, _) = await SeedAsync(ctx, "Invalid", "01", uuid: null);

            var redirect = Assert.IsType<RedirectToPageResult>(await Page(ctx, owner, "GET").OnGetAsync(invoiceNo));
            Assert.Equal(invoiceNo, redirect.RouteValues!["uuid"]);
        }

        [Theory]
        [InlineData("Draft", "01")]
        [InlineData("TransmissionError", "01")]
        [InlineData("Draft", "02")]
        [InlineData("Draft", "04")]
        public async Task Editable_Status_Stays_On_The_Editor(string status, string docType)
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (invoiceNo, owner, _) = await SeedAsync(ctx, status, docType, uuid: null);

            // The editor then loads its dropdowns (not wired here); what matters is that it is not sent away.
            var result = await Page(ctx, owner, "GET").OnGetAsync(invoiceNo);
            Assert.IsNotType<RedirectToPageResult>(result);
            Assert.IsNotType<ForbidResult>(result);
            Assert.True(InvoiceWriteGuard.IsEditableStatus(status));
        }

        [Theory]
        [InlineData("Valid")]
        [InlineData("Draft")]
        public async Task Another_Company_Is_Forbidden_Before_The_Status_Is_Revealed(string status)
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (invoiceNo, _, outsider) = await SeedAsync(ctx, status, "01", uuid: $"U{Guid.NewGuid():N}"[..26]);

            Assert.IsType<ForbidResult>(await Page(ctx, outsider, "GET").OnGetAsync(invoiceNo));
        }

        [Fact]
        public async Task Post_Rerender_Of_A_Locked_Invoice_Is_Not_Redirected()
        {
            // OnPostAsync calls OnGetAsync to re-render after InvoiceWriteGuard refuses a save; that path must keep
            // rendering the page with the refusal message, not silently redirect.
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var (invoiceNo, owner, _) = await SeedAsync(ctx, "Valid", "01", uuid: $"U{Guid.NewGuid():N}"[..26]);

            Assert.IsNotType<RedirectToPageResult>(await Page(ctx, owner, "POST").OnGetAsync(invoiceNo));
        }

        [Theory]
        [InlineData("Draft", true)]
        [InlineData("TransmissionError", true)]
        [InlineData("Valid", false)]
        [InlineData("Invalid", false)]
        [InlineData("Submitted", false)]
        [InlineData("Cancelled", false)]
        [InlineData("Rejected", false)]
        [InlineData("RequestReject", false)]
        [InlineData("Deleted", false)]
        [InlineData(null, false)]
        public void IsEditableStatus_Matches_The_Save_Guard(string? status, bool editable) =>
            Assert.Equal(editable, InvoiceWriteGuard.IsEditableStatus(status));
    }
}
