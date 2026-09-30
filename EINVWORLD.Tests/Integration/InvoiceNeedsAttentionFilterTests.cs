using System;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Real-SQL-Server tests for the "Needs Attention" resolution rules: the manual resolve marker and
    /// the "Valid Resend copy exists" correlated subquery (which must translate on SQL Server, and
    /// runs against a schema built by the AddNeedsAttentionResolution migration).
    /// </summary>
    public class InvoiceNeedsAttentionFilterTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public InvoiceNeedsAttentionFilterTests(SqlServerFixture fx) => _fx = fx;

        private static async Task<int> CreateSupplierAsync(eInvWorld.Data.ApplicationDbContext ctx)
        {
            const string regTypeCode = "TSTREG";
            const string stateCodeValue = "TSTSTATE";
            const string countryCodeValue = "TSTCOUNTRY";
            if (!await ctx.RegistrationTypes.AnyAsync(r => r.Code == regTypeCode))
                ctx.RegistrationTypes.Add(new RegistrationType { Code = regTypeCode, Name = "Test Registration Type" });
            if (!await ctx.StateCodes.AnyAsync(s => s.Code == stateCodeValue))
                ctx.StateCodes.Add(new StateCode { Code = stateCodeValue, State = "Test State", IsActive = true });
            if (!await ctx.CountryCodes.AnyAsync(c => c.Code == countryCodeValue))
                ctx.CountryCodes.Add(new CountryCode { Code = countryCodeValue, Country = "Testland", IsActive = true, UpdatedBy = "test" });

            var party = new PartyInfo
            {
                CompanyName = "Needs Attention Co",
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
            ctx.PartyInfos.Add(party);
            await ctx.SaveChangesAsync();
            return party.PartyInfoId;
        }

        private static InvoiceHeader Invoice(int supplierId, string internalStatus, string? lhdnStatus,
            DateTime? resolvedAt = null, string? resentFrom = null, DateTime? created = null)
        {
            var no = $"NA{Guid.NewGuid():N}"[..20];
            return new InvoiceHeader
            {
                InvoiceNo = no,
                PrefixedID = no,
                DocTypeCode = "01",
                Currency = "MYR",
                CreatedDate = created ?? DateTime.Now,
                CreatedBy = "integration-test",
                InternalStatusId = internalStatus,
                LHDNStatusId = lhdnStatus,
                SupplierId = supplierId,
                AttentionResolvedAt = resolvedAt,
                ResentFromInvoiceNo = resentFrom,
            };
        }

        [Fact]
        public async Task Apply_Excludes_Resolved_And_Validly_Resent_Invoices_Only()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var supplierId = await CreateSupplierAsync(ctx);

            var open = Invoice(supplierId, "Invalid", "Invalid");
            var resolved = Invoice(supplierId, "Invalid", "Invalid", resolvedAt: DateTime.Now);
            var resentValid = Invoice(supplierId, "Invalid", "Invalid");
            var resentPending = Invoice(supplierId, "Invalid", "Invalid");
            var rejectRequested = Invoice(supplierId, "RequestReject", "Valid");
            var agingDraft = Invoice(supplierId, "Draft", null, created: DateTime.Now.AddDays(-10));
            ctx.InvoiceHeaders.AddRange(open, resolved, resentValid, resentPending, rejectRequested, agingDraft);
            await ctx.SaveChangesAsync();

            // Copies: one reached Valid (clears its original), one is still only Submitted (does not).
            var validCopy = Invoice(supplierId, "Valid", "Valid", resentFrom: resentValid.InvoiceNo);
            var pendingCopy = Invoice(supplierId, "Submitted", "Submitted", resentFrom: resentPending.InvoiceNo);
            ctx.InvoiceHeaders.AddRange(validCopy, pendingCopy);
            await ctx.SaveChangesAsync();

            var scoped = ctx.InvoiceHeaders.Where(i => i.SupplierId == supplierId);
            var flagged = await InvoiceNeedsAttentionFilter.Apply(scoped, ctx.InvoiceHeaders)
                .Select(i => i.InvoiceNo).ToListAsync();

            Assert.Contains(open.InvoiceNo, flagged);
            Assert.Contains(resentPending.InvoiceNo, flagged);
            Assert.Contains(rejectRequested.InvoiceNo, flagged);
            Assert.Contains(agingDraft.InvoiceNo, flagged);
            Assert.DoesNotContain(resolved.InvoiceNo, flagged);
            Assert.DoesNotContain(resentValid.InvoiceNo, flagged);
            Assert.DoesNotContain(validCopy.InvoiceNo, flagged);
            Assert.Equal(4, flagged.Count);
        }

        [Fact]
        public void CanResolve_Allows_Only_Invalid_And_RequestReject()
        {
            Assert.True(InvoiceNeedsAttentionFilter.CanResolve(new InvoiceHeader { InternalStatusId = "Invalid", LHDNStatusId = "Invalid" }));
            Assert.True(InvoiceNeedsAttentionFilter.CanResolve(new InvoiceHeader { InternalStatusId = "RequestReject", LHDNStatusId = "Valid" }));
            Assert.False(InvoiceNeedsAttentionFilter.CanResolve(new InvoiceHeader { InternalStatusId = "Draft", LHDNStatusId = "Invalid" }));
            Assert.False(InvoiceNeedsAttentionFilter.CanResolve(new InvoiceHeader { InternalStatusId = "TransmissionError" }));
            Assert.False(InvoiceNeedsAttentionFilter.CanResolve(new InvoiceHeader { InternalStatusId = "Valid", LHDNStatusId = "Valid" }));
        }
    }
}
