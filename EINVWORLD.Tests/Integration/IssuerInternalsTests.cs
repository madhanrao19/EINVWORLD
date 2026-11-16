using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.InputModel;
using eInvWorld.Services;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// v1.50.73: the issuer's internal records (activity history, staff usernames) stay with the issuer; issuer
    /// actions follow the issuing company RECORD, so a PublicCustomer sharing a TIN never makes another company the
    /// issuer; "Send to buyers" is capped per request. B issues to A unless stated. Runs on SQL Server.
    /// </summary>
    public class IssuerInternalsTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public IssuerInternalsTests(SqlServerFixture fx) => _fx = fx;

        private sealed record Seed(PartyInfo A, PartyInfo B, ClaimsPrincipal BOwner, ClaimsPrincipal BViewer, ClaimsPrincipal AOwner, ClaimsPrincipal Admin);

        private static async Task<ClaimsPrincipal> MemberAsync(eInvWorld.Data.ApplicationDbContext ctx, string label, PartyInfo? company, bool viewOnly = false, string role = "Supplier", string userType = "Supplier")
        {
            var id = await CreateUserAsync(ctx, label);
            var u = (await ctx.Users.FindAsync(id))!;
            u.UserType = userType;
            if (company != null)
                ctx.UserCompanies.Add(new UserCompany { UserId = id, PartyInfoId = company.PartyInfoId, HasCompanyAccess = true, IsViewOnly = viewOnly });
            await ctx.SaveChangesAsync();
            return PrincipalFor(id, u.UserName!, role);
        }

        private static async Task<Seed> SeedAsync(eInvWorld.Data.ApplicationDbContext ctx, string label)
        {
            var a = await CreateValidPartyInfoAsync(ctx, $"II {label} A");
            var b = await CreateValidPartyInfoAsync(ctx, $"II {label} B");
            ctx.PartyInfos.AddRange(a, b);
            await ctx.SaveChangesAsync();
            return new Seed(a, b,
                await MemberAsync(ctx, $"ii-{label}-b", b),
                await MemberAsync(ctx, $"ii-{label}-bview", b, viewOnly: true),
                await MemberAsync(ctx, $"ii-{label}-a", a),
                await MemberAsync(ctx, $"ii-{label}-admin", null, role: "Admin", userType: "Admin"));
        }

        private static InvoiceHeader Add(eInvWorld.Data.ApplicationDbContext ctx, string type, int supplierId, int? customerId, string lhdn, int? publicCustomerId = null)
        {
            var no = $"INV-II-{Guid.NewGuid():N}"[..20];
            var inv = new InvoiceHeader
            {
                InvoiceNo = no, PrefixedID = no, DocTypeCode = type, Currency = "MYR", CreatedBy = "b-staff@issuer.test",
                CreatedDate = DateTime.Now, LastUpdated = new DateTime(2000, 1, 1), InternalStatusId = lhdn, LHDNStatusId = lhdn,
                UUID = $"I{Guid.NewGuid():N}"[..26], LongId = "LONG", SupplierId = supplierId, CustomerId = customerId,
                PublicCustomerId = publicCustomerId, TotalAmountIncTax = 10m, DateTimeValidated = DateTime.Now,
            };
            ctx.InvoiceHeaders.Add(inv);
            return inv;
        }

        private static async Task<PublicCustomer> PublicCustomerWithTinAsync(eInvWorld.Data.ApplicationDbContext ctx, PartyInfo like, string tin)
        {
            var pc = new PublicCustomer
            {
                CompanyName = "Walk-in sharing a TIN", TIN = tin, RegTypeCode = like.RegTypeCode, RegNo = "PC-REG", IndustryClassificationCode = "01111",
                Addr1 = "1 Test Street", CityName = "Test City", StateCode = like.StateCode, CountryCode = like.CountryCode, PhoneNo = "+60123456789",
                CreatedBy = "test",
            };
            ctx.PublicCustomers.Add(pc);
            await ctx.SaveChangesAsync();
            return pc;
        }

        private sealed class NoTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static T As<T>(T page, ClaimsPrincipal user) where T : PageModel
        {
            var http = new DefaultHttpContext { User = user };
            page.PageContext = new PageContext { HttpContext = http };
            page.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new NoTempData());
            return page;
        }

        [Fact]
        public async Task Issuer_Is_The_Company_Record_And_A_Shared_Tin_Grants_Nothing()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var s = await SeedAsync(ctx, "tin");

            // Two company records can't share a TIN (unique index; only the shared LHDN general TINs are exempt).
            var dup = await CreateValidPartyInfoAsync(ctx, "II duplicate");
            dup.TIN = s.B.TIN;
            ctx.PartyInfos.Add(dup);
            await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
            ctx.ChangeTracker.Clear();

            // A walk-in record that merely carries B's TIN, as the issuer of a self-billed document (the write guard refuses
            // to create one; the rules must hold even if a row like this exists).
            var pc = await PublicCustomerWithTinAsync(ctx, s.A, s.B.TIN!);
            var foreignSelfBilled = Add(ctx, "11", s.A.PartyInfoId, null, "Valid", pc.PublicCustomerId);
            var bIssued = Add(ctx, "01", s.B.PartyInfoId, s.A.PartyInfoId, "Valid");
            await ctx.SaveChangesAsync();

            Assert.False(await s.BOwner.CanManageInvoiceAsync(ctx, foreignSelfBilled.InvoiceNo));
            Assert.False(await s.BOwner.IsIssuerOfInvoiceAsync(ctx, foreignSelfBilled.InvoiceNo));
            var bIds = await s.BOwner.OwnCompanyIdsAsync(ctx, forWrite: true);
            Assert.Empty(eInvWorld.Pages.Invoices.InvoiceListsModel.SendToBuyersScope(ctx.InvoiceHeaders, new List<string> { foreignSelfBilled.UUID! }, bIds));

            // The real issuer: owner manages and sees internals; a view-only member sees internals but may not act;
            // the recipient does neither; an Admin does both.
            Assert.True(await s.BOwner.CanManageInvoiceAsync(ctx, bIssued.InvoiceNo));
            Assert.True(await s.BOwner.IsIssuerOfInvoiceAsync(ctx, bIssued.InvoiceNo));
            Assert.False(await s.BViewer.CanManageInvoiceAsync(ctx, bIssued.InvoiceNo));
            Assert.True(await s.BViewer.IsIssuerOfInvoiceAsync(ctx, bIssued.InvoiceNo));
            Assert.Empty(await s.BViewer.OwnCompanyIdsAsync(ctx, forWrite: true));
            Assert.False(await s.AOwner.CanManageInvoiceAsync(ctx, bIssued.InvoiceNo));
            Assert.False(await s.AOwner.IsIssuerOfInvoiceAsync(ctx, bIssued.InvoiceNo));
            Assert.True(await s.Admin.IsIssuerOfInvoiceAsync(ctx, bIssued.InvoiceNo));
        }

        [Fact]
        public async Task Recipient_History_Export_Shows_Only_The_Lhdn_Lifecycle()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var s = await SeedAsync(ctx, "hist");
            var inv = Add(ctx, "01", s.B.PartyInfoId, s.A.PartyInfoId, "Valid");
            var t = DateTime.UtcNow.AddHours(-1);
            void Log(string action, string by, string remarks) =>
                ctx.InvoiceHistories.Add(new InvoiceHistory { InvoiceNo = inv.InvoiceNo, Action = action, PerformedBy = by, Remarks = remarks, Timestamp = t = t.AddMinutes(1) });
            Log("Created", "b-staff@issuer.test", "Invoice draft created");
            Log("Edited", "b-staff@issuer.test", "Unit price: 9 -> 10");
            Log("Submitted", "b-staff@issuer.test", "Submitted to LHDN - SubmissionUID: SECRETSUB");
            Log("StatusSync", "b-staff@issuer.test", "Updated status: Valid");
            Log("EmailSent", "Finalizer", "a@buyer.test; finance@issuer.test");
            await ctx.SaveChangesAsync();

            async Task<string> Csv(ClaimsPrincipal user)
            {
                var page = As(new EINVWORLD.Pages.Invoices.InvoiceDetails2Model(ctx, NullLogger<eInvWorld.Pages.Invoices.InvoiceDetailsModel>.Instance,
                    null!, null!, null!, null!, null!, null!, null!, null!, null!), user);
                var file = Assert.IsType<FileContentResult>(await page.OnGetExportHistoryAsync(inv.InvoiceNo));
                return Encoding.UTF8.GetString(file.FileContents);
            }

            var recipient = await Csv(s.AOwner);
            Assert.DoesNotContain("b-staff@issuer.test", recipient);
            Assert.DoesNotContain("SECRETSUB", recipient);
            Assert.DoesNotContain("finance@issuer.test", recipient);
            Assert.DoesNotContain("Unit price", recipient);
            Assert.Contains("Submitted,Issuer", recipient);
            Assert.Contains("StatusSync,LHDN MyInvois,Updated status: Valid", recipient);

            foreach (var issuer in new[] { s.BOwner, s.BViewer, s.Admin })
            {
                var full = await Csv(issuer);
                Assert.Contains("b-staff@issuer.test", full);
                Assert.Contains("SECRETSUB", full);
                Assert.Contains("Unit price", full);
            }
        }

        private sealed class RecordingNotifications : IEInvoiceNotificationService
        {
            public readonly List<string> Validated = new();
            public Task SendValidatedNotificationEmail(string recipientName, PartyInfo? buyer, PartyInfo? supplier, string documentId, DateTime issueDate, DateTime validatedTimestamp, PublicCustomer? publicCustomer = null)
            { Validated.Add(documentId); return Task.CompletedTask; }
            public Task<bool> SendRejectionNotificationEmail(PartyInfo? buyer, PartyInfo? supplier, string documentId, string rejectionReason, DateTime rejectedTimestamp) => Task.FromResult(true);
            public Task<bool> SendCancellationNotificationEmail(PartyInfo? buyer, PartyInfo? supplier, string documentId, string cancellationReason, DateTime cancelledTimestamp) => Task.FromResult(true);
            public Task<bool> SendNewInvoiceReceivedNotificationEmail(PartyInfo? buyer, PartyInfo? supplier, string documentId, DateTime issueDate, PublicCustomer? publicCustomer = null) => Task.FromResult(true);
        }

        [Fact]
        public async Task Send_To_Buyers_Handler_Caps_The_Batch_And_Sends_Only_For_The_Issuer()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var s = await SeedAsync(ctx, "send");
            var valid = Add(ctx, "01", s.B.PartyInfoId, s.A.PartyInfoId, "Valid");
            await ctx.SaveChangesAsync();

            var mail = new RecordingNotifications(); // a fake: no real email leaves the test
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Invoices:SendToBuyersMaxBatch"] = "3" }).Build();
            var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(ctx), Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
                null!, null!, null!, null!, null!, NullLogger<UserManager<ApplicationUser>>.Instance);
            eInvWorld.Pages.Invoices.InvoiceListsModel Page(ClaimsPrincipal u) => As(new eInvWorld.Pages.Invoices.InvoiceListsModel(ctx, config, null!,
                NullLogger<eInvWorld.Pages.Invoices.InvoiceListsModel>.Instance, users, mail, null!, null!, null!, null!, null!, null!, null!, null!,
                Options.Create(new FilePathConfig()), null!), u);
            static int Sent(JsonResult r) => (int)r.Value!.GetType().GetProperty("sentCount")!.GetValue(r.Value)!;

            var tooMany = await Page(s.BOwner).OnPostBulkSendToBuyersAsync(new List<string> { valid.UUID!, "X1", "X2", "X3" });
            Assert.Equal(400, tooMany.StatusCode);
            Assert.Empty(mail.Validated);

            Assert.Equal(0, Sent(await Page(s.AOwner).OnPostBulkSendToBuyersAsync(new List<string> { valid.UUID! })));  // recipient
            Assert.Equal(0, Sent(await Page(s.BViewer).OnPostBulkSendToBuyersAsync(new List<string> { valid.UUID! }))); // view-only
            Assert.Empty(mail.Validated);
            Assert.Equal(1, Sent(await Page(s.BOwner).OnPostBulkSendToBuyersAsync(new List<string> { valid.UUID!, valid.UUID!, " " })));
            Assert.Equal(new[] { valid.InvoiceNo }, mail.Validated);
        }

        private sealed class RecordingJobs : EINVWORLD.Services.Background.ISyncJobTracker
        {
            public readonly List<(string Tin, string Payload)> Created = new();
            public Task<int> CreateAsync(string tin, string jobType, string? triggeredBy, string? payloadJson = null)
            { Created.Add((tin, payloadJson ?? "")); return Task.FromResult(Created.Count); }
            public Task MarkRunningAsync(int jobId) => Task.CompletedTask;
            public Task MarkCompletedAsync(int jobId, string? message, int importedCount = 0, int errorCount = 0) => Task.CompletedTask;
            public Task MarkFailedAsync(int jobId, string? message) => Task.CompletedTask;
        }

        [Fact]
        public async Task Webhook_Reaches_A_Walk_In_Recipients_Company_But_Never_A_General_Tin()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var s = await SeedAsync(ctx, "hook");
            var pc = await PublicCustomerWithTinAsync(ctx, s.A, s.A.TIN!);              // B bills a walk-in record carrying A's TIN
            var general = await PublicCustomerWithTinAsync(ctx, s.A, "EI00000000010");  // General Public
            foreach (var tin in new[] { s.A.TIN!, s.B.TIN!, "EI00000000010" })
                ctx.Set<eInvWorld.Models.Webhooks.WebhookSubscription>().Add(new() { Tin = tin, CallbackUrl = "https://example.invalid/hook", Secret = "test", CreatedAtUtc = DateTime.UtcNow });
            var toWalkIn = Add(ctx, "01", s.B.PartyInfoId, null, "Valid", pc.PublicCustomerId);
            var toPublic = Add(ctx, "01", s.B.PartyInfoId, null, "Valid", general.PublicCustomerId);
            await ctx.SaveChangesAsync();

            var jobs = new RecordingJobs();
            await new eInvWorld.Services.Webhooks.WebhookDispatchService(jobs,
                Options.Create(new eInvWorld.Models.Settings.WebhookSettings { Enabled = true }),
                NullLogger<eInvWorld.Services.Webhooks.WebhookDispatchService>.Instance).ScanAndEnqueueAsync(ctx);

            List<string> TinsFor(InvoiceHeader i) => jobs.Created.Where(c => c.Payload.Contains(i.InvoiceNo)).Select(c => c.Tin).OrderBy(t => t).ToList();
            Assert.Equal(new[] { s.A.TIN!, s.B.TIN! }.OrderBy(t => t), TinsFor(toWalkIn));
            Assert.Equal(new[] { s.B.TIN! }, TinsFor(toPublic));
        }
    }
}
