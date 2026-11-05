using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>v1.50.42: approving a company lead records the TIN masked in the user activity log.</summary>
    public class LeadApprovalMaskingTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public LeadApprovalMaskingTests(SqlServerFixture fx) => _fx = fx;

        [Fact]
        public async Task Approving_a_lead_logs_the_tin_masked()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            const string reg = "TSTREG", state = "TSTSTATE", country = "TSTCOUNTRY";
            if (!await ctx.RegistrationTypes.AnyAsync(r => r.Code == reg)) ctx.RegistrationTypes.Add(new RegistrationType { Code = reg, Name = "Test" });
            if (!await ctx.StateCodes.AnyAsync(s => s.Code == state)) ctx.StateCodes.Add(new StateCode { Code = state, State = "Test", IsActive = true });
            if (!await ctx.CountryCodes.AnyAsync(c => c.Code == country)) ctx.CountryCodes.Add(new CountryCode { Code = country, Country = "Testland", IsActive = true, UpdatedBy = "test" });

            string tin = "IG" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999); // individual TIN shape
            var lead = new PartyInfo
            {
                CompanyName = "Lead " + Guid.NewGuid().ToString("N")[..6], IndustryClassificationCode = "01111", TIN = tin,
                RegTypeCode = reg, RegNo = $"REG{Guid.NewGuid():N}"[..12], Addr1 = "1 Test Street", CityName = "Test City",
                StateCode = state, CountryCode = country, PhoneNo = "+60123456789", CreatedBy = "test", IsApproved = false,
            };
            ctx.PartyInfos.Add(lead);
            await ctx.SaveChangesAsync();

            var page = new eInvWorld.Pages.Lead.ListModel(ctx)
            {
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "lead-admin") }, "test"))
                    }
                }
            };
            await page.OnPostApproveAsync(lead.PartyInfoId);

            var entry = await ctx.UserActivityLogs.AsNoTracking()
                .Where(a => a.Action == "Approved Company Lead" && a.Data!.Contains(lead.CompanyName))
                .SingleAsync();
            Assert.DoesNotContain(tin, entry.Data);
            Assert.Contains(LogSanitizer.MaskTin(tin), entry.Data);
        }
    }
}
