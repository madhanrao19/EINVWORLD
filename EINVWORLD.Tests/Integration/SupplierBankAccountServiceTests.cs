using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using EINVWORLD.Services;
using EINVWORLD.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Real-SQL tests for company bank accounts: the SupplierBankAccounts table from the
    /// AddSupplierBankAccounts migration, the "exactly one default, mirrored to PartyInfo" rules in
    /// SupplierBankAccountService, and the PartyBankAccess guard used by the invoice screens.
    /// </summary>
    public class SupplierBankAccountServiceTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public SupplierBankAccountServiceTests(SqlServerFixture fx) => _fx = fx;

        private sealed class NoAudit : IAuditService
        {
            public Task WriteAsync(string action, AuditEntry entry, CancellationToken ct = default) => Task.CompletedTask;
            public Task<AuditVerificationResult> VerifyChainAsync(CancellationToken ct = default) =>
                Task.FromResult(new AuditVerificationResult(true, 0, null, ""));
        }

        private static async Task<PartyInfo> CreatePartyAsync(eInvWorld.Data.ApplicationDbContext ctx, string? bankName = null, string? bankNo = null)
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
                CompanyName = "Bank Test " + Guid.NewGuid().ToString("N")[..6],
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
                BankName = bankName,
                BankAccountNo = bankNo,
            };
            ctx.PartyInfos.Add(party);
            await ctx.SaveChangesAsync();
            return party;
        }

        private static async Task<(PartyInfo Party, SupplierBankAccount[] Accounts)> ReloadAsync(eInvWorld.Data.ApplicationDbContext ctx, int partyId)
        {
            var party = await ctx.PartyInfos.AsNoTracking().FirstAsync(p => p.PartyInfoId == partyId);
            var accounts = await ctx.SupplierBankAccounts.AsNoTracking().Where(a => a.PartyInfoId == partyId).ToArrayAsync();
            return (party, accounts);
        }

        [Fact]
        public async Task FirstAccountBecomesDefault_AndNewDefaultMovesTheMirror()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var party = await CreatePartyAsync(ctx);
            var svc = new SupplierBankAccountService(ctx, new NoAudit());

            var first = await svc.AddAsync(party.PartyInfoId, "Maybank", "1111222233", makeDefault: false, "test");
            var second = await svc.AddAsync(party.PartyInfoId, "CIMB", "9999888877", makeDefault: true, "test");

            await using var check = _fx.CreateContext();
            var (p, accounts) = await ReloadAsync(check, party.PartyInfoId);
            Assert.Equal(2, accounts.Length);
            Assert.Single(accounts, a => a.IsDefault);
            Assert.True(accounts.Single(a => a.Id == second.Id).IsDefault);
            Assert.Equal("CIMB", p.BankName);
            Assert.Equal("9999888877", p.BankAccountNo);

            Assert.True(await svc.SetDefaultAsync(party.PartyInfoId, first.Id, "test"));
            await using var check2 = _fx.CreateContext();
            var (p2, accounts2) = await ReloadAsync(check2, party.PartyInfoId);
            Assert.Single(accounts2, a => a.IsDefault);
            Assert.Equal("1111222233", p2.BankAccountNo);
        }

        [Fact]
        public async Task DeletingDefault_PromotesAnother_AndDeletingLast_ClearsMirror()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var party = await CreatePartyAsync(ctx);
            var svc = new SupplierBankAccountService(ctx, new NoAudit());

            var a = await svc.AddAsync(party.PartyInfoId, "Maybank", "1111", false, "test");
            var b = await svc.AddAsync(party.PartyInfoId, "CIMB", "2222", false, "test");

            Assert.True(await svc.DeleteAsync(party.PartyInfoId, a.Id, "test"));
            await using (var check = _fx.CreateContext())
            {
                var (p, accounts) = await ReloadAsync(check, party.PartyInfoId);
                Assert.True(Assert.Single(accounts).IsDefault);
                Assert.Equal("2222", p.BankAccountNo);
            }

            Assert.True(await svc.DeleteAsync(party.PartyInfoId, b.Id, "test"));
            await using (var check = _fx.CreateContext())
            {
                var (p, accounts) = await ReloadAsync(check, party.PartyInfoId);
                Assert.Empty(accounts);
                Assert.Null(p.BankAccountNo);
                Assert.Null(p.BankName);
            }
        }

        [Fact]
        public async Task AccountOfAnotherCompany_CannotBeChanged()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var mine = await CreatePartyAsync(ctx);
            var theirs = await CreatePartyAsync(ctx);
            var svc = new SupplierBankAccountService(ctx, new NoAudit());
            var theirAccount = await svc.AddAsync(theirs.PartyInfoId, "RHB", "5555", false, "test");

            Assert.False(await svc.UpdateAsync(mine.PartyInfoId, theirAccount.Id, "Hacked", "0000", "test"));
            Assert.False(await svc.SetDefaultAsync(mine.PartyInfoId, theirAccount.Id, "test"));
            Assert.False(await svc.DeleteAsync(mine.PartyInfoId, theirAccount.Id, "test"));

            await using var check = _fx.CreateContext();
            var stored = await check.SupplierBankAccounts.AsNoTracking().FirstAsync(x => x.Id == theirAccount.Id);
            Assert.Equal("RHB", stored.BankName);
            Assert.Equal("5555", stored.BankAccountNo);
        }

        [Fact]
        public async Task SyncFromCompany_CreatesDefault_ThenUpdatesItInPlace()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var party = await CreatePartyAsync(ctx, "Public Bank", "7777");
            var svc = new SupplierBankAccountService(ctx, new NoAudit());

            await svc.SyncDefaultFromCompanyAsync(party.PartyInfoId, "test");
            party.BankName = "Public Bank Berhad";
            party.BankAccountNo = "7778";
            await ctx.SaveChangesAsync();
            await svc.SyncDefaultFromCompanyAsync(party.PartyInfoId, "test");

            await using var check = _fx.CreateContext();
            var (_, accounts) = await ReloadAsync(check, party.PartyInfoId);
            var only = Assert.Single(accounts);
            Assert.True(only.IsDefault);
            Assert.Equal("Public Bank Berhad", only.BankName);
            Assert.Equal("7778", only.BankAccountNo);
        }

        [Fact]
        public async Task PartyBankAccess_OnlyMembersSeeTheirCompanysBank()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var mine = await CreatePartyAsync(ctx);
            var theirs = await CreatePartyAsync(ctx);

            var userId = Guid.NewGuid().ToString();
            ctx.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = $"bank-{userId}@test.local",
                NormalizedUserName = $"BANK-{userId}@TEST.LOCAL",
                Email = $"bank-{userId}@test.local",
                FullName = "Bank Tester",
                UserType = "Supplier",
            });
            ctx.UserCompanies.Add(new UserCompany { UserId = userId, PartyInfoId = mine.PartyInfoId, IsPrimaryCompany = true, HasCompanyAccess = true });
            await ctx.SaveChangesAsync();

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Supplier") }, "test"));

            Assert.True(await PartyBankAccess.CanSeeBankAsync(user, ctx, "PI", mine.PartyInfoId));
            Assert.False(await PartyBankAccess.CanSeeBankAsync(user, ctx, "PI", theirs.PartyInfoId));

            // Party details: own company, a linked buyer and a general-TIN party yes; an unrelated company no.
            var buyer = await CreatePartyAsync(ctx);
            var general = await CreatePartyAsync(ctx);
            general.TIN = "EI00000000010";
            ctx.SupplierBuyers.Add(new SupplierBuyer { SupplierId = mine.PartyInfoId, BuyerId = buyer.PartyInfoId });
            await ctx.SaveChangesAsync();

            Assert.True(await PartyBankAccess.CanViewPartyAsync(user, ctx, "PI", mine.PartyInfoId));
            Assert.True(await PartyBankAccess.CanViewPartyAsync(user, ctx, "PI", buyer.PartyInfoId));
            Assert.True(await PartyBankAccess.CanViewPartyAsync(user, ctx, "PI", general.PartyInfoId));
            Assert.False(await PartyBankAccess.CanViewPartyAsync(user, ctx, "PI", theirs.PartyInfoId));
            Assert.False(await PartyBankAccess.CanSeeBankAsync(user, ctx, "PI", buyer.PartyInfoId)); // buyer's bank stays private
        }
    }
}
