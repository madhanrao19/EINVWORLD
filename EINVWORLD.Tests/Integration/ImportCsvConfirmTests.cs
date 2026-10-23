using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EINVWORLD.Pages.Invoices;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Import CSV's Confirm step used to deserialize the preview lines from a hidden field as-is, so a hand-edited
    /// post skipped every preview check (e.g. linking any company's TIN as the buyer). The field is now signed per
    /// user; anything else is refused before a single invoice is built.
    /// </summary>
    public class ImportCsvConfirmTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public ImportCsvConfirmTests(SqlServerFixture fx) => _fx = fx;

        private static ImportCSVModel PageFor(eInvWorld.Data.ApplicationDbContext ctx, IDataProtectionProvider dp, string userId)
        {
            var page = new ImportCSVModel(ctx, null!, null!, Options.Create(new eInvWorld.Models.FilePathConfig()),
                NullLogger<ImportCSVModel>.Instance, null!, null!, null!, dp);
            page.PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth")) }
            };
            return page;
        }

        private static string SignedFor(IDataProtectionProvider dp, string userId, string json) =>
            dp.CreateProtector("ImportCSV.ValidRecords.v1", userId).ToTimeLimitedDataProtector().Protect(json, TimeSpan.FromHours(2));

        [Fact]
        public async Task Confirm_Refuses_Unsigned_Or_Other_Users_Payload_And_Accepts_Its_Own()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var dp = new EphemeralDataProtectionProvider();
            var before = ctx.InvoiceHeaders.Count();

            var tampered = PageFor(ctx, dp, "user-a");
            tampered.ValidRecordsJson = "[{\"InvoiceNo\":\"X1\",\"CounterpartyTIN\":\"C00000000000\",\"IssueDate\":\"2026-10-23\"}]";
            Assert.IsType<PageResult>(await tampered.OnPostConfirmAsync());
            Assert.False(tampered.ModelState.IsValid);

            var replayed = PageFor(ctx, dp, "user-b");
            replayed.ValidRecordsJson = SignedFor(dp, "user-a", "[]");
            Assert.IsType<PageResult>(await replayed.OnPostConfirmAsync());
            Assert.False(replayed.ModelState.IsValid);

            var own = PageFor(ctx, dp, "user-a");
            own.ValidRecordsJson = SignedFor(dp, "user-a", "[]"); // accepted; nothing to import, back to the list
            Assert.IsType<RedirectToPageResult>(await own.OnPostConfirmAsync());
            Assert.True(own.ModelState.IsValid);

            Assert.Equal(before, ctx.InvoiceHeaders.Count());
        }
    }
}
