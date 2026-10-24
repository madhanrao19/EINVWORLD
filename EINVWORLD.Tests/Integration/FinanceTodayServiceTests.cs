using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.Recurring;
using eInvWorld.Models.SmartCapture;
using eInvWorld.Models.Templates;
using eInvWorld.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>The Dashboard's Today card: every group, scoped to the user's own companies only.</summary>
    public class FinanceTodayServiceTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public FinanceTodayServiceTests(SqlServerFixture fx) => _fx = fx;

        private static async Task EnsureStatusesAsync(eInvWorld.Data.ApplicationDbContext ctx, params string[] codes)
        {
            foreach (var code in codes)
                if (!await ctx.Set<Status>().AnyAsync(s => s.StatusCode == code))
                    ctx.Set<Status>().Add(new Status { StatusCode = code, StatusType = "Internal", Name = code });
            if (!await ctx.UnitTypes.AnyAsync(u => u.Code == "C62"))
                ctx.UnitTypes.Add(new UnitType { Code = "C62", Name = "One", IsActive = true });
            await ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task Today_Shows_Each_Group_For_The_Users_Companies_Only()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            await EnsureStatusesAsync(ctx, "Draft", "Submitted", "Valid", "Invalid", "RequestReject");

            var own = await CreateValidPartyInfoAsync(ctx, "Today Own Co");
            own.BizDescription = "Trading"; own.PostalCode = "50000";
            var buyer = await CreateValidPartyInfoAsync(ctx, "Today Buyer Co");
            var other = await CreateValidPartyInfoAsync(ctx, "Today Other Tenant");
            ctx.PartyInfos.AddRange(own, buyer, other);
            var general = await ctx.PartyInfos.FirstOrDefaultAsync(pi => pi.TIN == "EI00000000010");
            if (general == null)
            {
                general = await CreateValidPartyInfoAsync(ctx, "General Public");
                general.TIN = "EI00000000010";
                ctx.PartyInfos.Add(general);
            }
            await ctx.SaveChangesAsync();

            var editorId = await CreateUserAsync(ctx, "today-editor");
            var viewerId = await CreateUserAsync(ctx, "today-viewer");
            ctx.UserCompanies.Add(new UserCompany { UserId = editorId, PartyInfoId = own.PartyInfoId, HasCompanyAccess = true });
            ctx.UserCompanies.Add(new UserCompany { UserId = editorId, PartyInfoId = general.PartyInfoId }); // must not widen the scope
            ctx.UserCompanies.Add(new UserCompany { UserId = viewerId, PartyInfoId = own.PartyInfoId, IsViewOnly = true });

            var nowMyt = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
            string Add(int supplierId, int? customerId, string internalStatus, string? lhdn = null, DateTime? validated = null, bool lines = false)
            {
                var no = $"INV-TD-{Guid.NewGuid():N}"[..20];
                var inv = new InvoiceHeader
                {
                    InvoiceNo = no, PrefixedID = no, DocTypeCode = "01", Currency = "MYR", UUID = lhdn == null ? null : $"TD{Guid.NewGuid():N}"[..26],
                    CreatedDate = DateTime.Now, CreatedBy = "integration-test", InternalStatusId = internalStatus, LHDNStatusId = lhdn,
                    SupplierId = supplierId, CustomerId = customerId, IssueDate = nowMyt.AddHours(-1), DateTimeValidated = validated,
                    TotalAmountExclTax = lines ? 100m : null, TotalTaxAmount = lines ? 8m : null,
                };
                if (lines)
                    inv.InvoiceLines.Add(new InvoiceLine
                    {
                        LineNumber = 1, ItemDescription = "Service", Quantity = 1, UnitPrice = 100m, ClassificationCode = "022",
                        UnitOfMeasure = "C62", AmountExclTax = 100m,
                        InvoiceTaxes = new List<InvoiceTax> { new() { TaxCategory = "02", TaxPercentage = 8m, TaxAmount = 8m } },
                    });
                ctx.InvoiceHeaders.Add(inv);
                return no;
            }
            var invalid = Add(own.PartyInfoId, buyer.PartyInfoId, "Invalid", "Invalid");
            var rejectAsked = Add(own.PartyInfoId, buyer.PartyInfoId, "RequestReject", "Valid");
            var notReady = Add(own.PartyInfoId, buyer.PartyInfoId, "Draft");                       // no lines
            Add(own.PartyInfoId, buyer.PartyInfoId, "Draft", lines: true);                           // ready
            Add(own.PartyInfoId, buyer.PartyInfoId, "Submitted", "Submitted");
            Add(own.PartyInfoId, buyer.PartyInfoId, "Valid", "Valid", validated: nowMyt.AddMinutes(-5));
            Add(own.PartyInfoId, buyer.PartyInfoId, "Valid", "Valid", validated: nowMyt.Date.AddMinutes(-1)); // yesterday
            var foreign = Add(other.PartyInfoId, buyer.PartyInfoId, "Invalid", "Invalid");            // another tenant
            var viaGeneral = Add(other.PartyInfoId, general.PartyInfoId, "Invalid", "Invalid");       // to General Public

            var template = new InvoiceTemplate { TemplateName = "today", DocTypeCode = "01", SupplierId = own.PartyInfoId, CustomerId = buyer.PartyInfoId };
            ctx.InvoiceTemplates.Add(template);
            await ctx.SaveChangesAsync();
            RecurringProfile Profile(string name) => new()
            {
                ProfileName = name, InvoiceTemplateId = template.Id, SupplierId = own.PartyInfoId, CustomerId = buyer.PartyInfoId,
                NextRunDate = nowMyt.AddDays(1), CreatedByUserId = editorId,
            };
            var failing = Profile($"Failing {Guid.NewGuid():N}"[..20]);
            var recovered = Profile($"Recovered {Guid.NewGuid():N}"[..20]);
            ctx.RecurringProfiles.AddRange(failing, recovered);
            await ctx.SaveChangesAsync();
            ctx.RecurringRunHistories.AddRange(
                new RecurringRunHistory { RecurringProfileId = failing.Id, RunTimestamp = nowMyt.AddHours(-2), RunStatus = "Failed_Internal" },
                new RecurringRunHistory { RecurringProfileId = recovered.Id, RunTimestamp = nowMyt.AddHours(-3), RunStatus = "Failed_Internal" },
                new RecurringRunHistory { RecurringProfileId = recovered.Id, RunTimestamp = nowMyt.AddHours(-1), RunStatus = "Success_Draft" });
            ctx.SmartCaptureDocuments.Add(new SmartCaptureDocument
            {
                CompanyPartyInfoId = own.PartyInfoId, UploadedByUserId = editorId, OriginalFileName = "supplier-bill.pdf",
                InternalStorageReference = "irrelevant.pdf", ContentType = "application/pdf", FileSize = 1,
                Status = SmartCaptureDocumentStatus.ReviewRequired, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();

            var editorName = (await ctx.Users.FindAsync(editorId))!.UserName!;
            var today = await new FinanceTodayService(ctx).GetAsync(PrincipalFor(editorId, editorName));
            var titles = today.Items.Select(i => i.Title).ToList();

            Assert.Contains(invalid, titles);
            Assert.Contains(rejectAsked, titles);
            Assert.Contains(notReady, titles);
            Assert.DoesNotContain(foreign, titles);
            Assert.DoesNotContain(viaGeneral, titles);
            Assert.Contains(titles, t => t == $"Recurring: {failing.ProfileName}");
            Assert.DoesNotContain(titles, t => t == $"Recurring: {recovered.ProfileName}");
            Assert.Contains("Smart Capture: supplier-bill.pdf", titles);
            Assert.Equal(1, today.ReadyToSubmit);
            Assert.Equal(1, today.ProcessingWithLhdn);
            Assert.Equal(1, today.ValidatedToday);

            var invalidItem = today.Items.Single(i => i.Title == invalid);
            Assert.StartsWith("/Invoices/InvoiceDetails2/", invalidItem.Url);
            Assert.Equal("Review", invalidItem.ActionLabel);
            var draftItem = today.Items.Single(i => i.Title == notReady);
            Assert.Equal($"/Invoices/InvoiceEdit/{notReady}", draftItem.Url);
            Assert.StartsWith("Not ready for LHDN:", draftItem.Reason);
            Assert.Equal("Fix invoice", draftItem.ActionLabel);

            // A view-only member sees the invoices but can only open them; recurring is for people who can change it.
            var viewerName = (await ctx.Users.FindAsync(viewerId))!.UserName!;
            var viewer = await new FinanceTodayService(ctx).GetAsync(PrincipalFor(viewerId, viewerName));
            Assert.All(viewer.Items, i => Assert.Equal("View", i.ActionLabel));
            Assert.DoesNotContain(viewer.Items, i => i.Title.StartsWith("Recurring:"));
        }

        [Fact]
        public async Task A_User_With_No_Companies_Gets_An_Empty_Today()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var userId = await CreateUserAsync(ctx, "today-nobody");
            var name = (await ctx.Users.FindAsync(userId))!.UserName!;
            var today = await new FinanceTodayService(ctx).GetAsync(PrincipalFor(userId, name));
            Assert.Empty(today.Items);
            Assert.Equal(0, today.NeedsAttention);
        }
    }
}
