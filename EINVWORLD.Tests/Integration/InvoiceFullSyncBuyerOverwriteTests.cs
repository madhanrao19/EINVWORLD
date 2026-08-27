using System;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Real-SQL-Server regression tests for a data-integrity bug found on staging: invoice EINV100506
    /// was created for Buyer "PT Kustodian Sentral Efek Indonesia" (a PublicCustomer carrying LHDN's
    /// shared general TIN EI00000000020), submitted, reached Valid — then its Bill To silently flipped
    /// to the generic placeholder "Foreign Buyer / Shipping Recipient" (a different PartyInfo row that
    /// happens to share the same general TIN), with no Activity Log entry. Root cause:
    /// InvoiceFullSyncHelper.SyncAllFromApiAsync re-resolved the buyer on every background/manual "full
    /// sync from LHDN" pass via an unscoped, TIN-only PartyInfos lookup — general TINs are shared by
    /// design, so FirstOrDefault nondeterministically matched whichever PartyInfo row happened to carry
    /// that TIN first, then unconditionally overwrote the invoice's correct PublicCustomerId with the
    /// wrong CustomerId. These tests exercise the real helper (not a mock) against a real database.
    /// </summary>
    public class InvoiceFullSyncBuyerOverwriteTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public InvoiceFullSyncBuyerOverwriteTests(SqlServerFixture fx) => _fx = fx;

        private const string GeneralTin = "EI00000000020"; // Foreign Buyer's / Foreign Shipping Recipient's TIN

        private static async Task SeedLookupsAsync(eInvWorld.Data.ApplicationDbContext ctx)
        {
            if (!await ctx.RegistrationTypes.AnyAsync(r => r.Code == "TSTREG"))
                ctx.RegistrationTypes.Add(new RegistrationType { Code = "TSTREG", Name = "Test Registration Type" });
            if (!await ctx.StateCodes.AnyAsync(s => s.Code == "TSTSTATE"))
                ctx.StateCodes.Add(new eInvWorld.Models.StateCode { Code = "TSTSTATE", State = "Test State", IsActive = true });
            if (!await ctx.CountryCodes.AnyAsync(c => c.Code == "TSTCOUNTRY"))
                ctx.CountryCodes.Add(new eInvWorld.Models.CountryCode { Code = "TSTCOUNTRY", Country = "Testland", IsActive = true, UpdatedBy = "test" });
            await ctx.SaveChangesAsync();
        }

        private static PartyInfo NewPartyInfo(string name, string tin) => new()
        {
            CompanyName = name,
            IndustryClassificationCode = "01111",
            TIN = tin,
            RegTypeCode = "TSTREG",
            RegNo = $"REG{Guid.NewGuid():N}"[..12],
            Addr1 = "1 Test Street",
            CityName = "Test City",
            StateCode = "TSTSTATE",
            CountryCode = "TSTCOUNTRY",
            PhoneNo = "+60123456789",
            CreatedBy = "test",
        };

        private static PublicCustomer NewPublicCustomer(string name, string tin, int ownerCompanyId) => new()
        {
            CompanyName = name,
            IndustryClassificationCode = "01111",
            TIN = tin,
            RegTypeCode = "TSTREG",
            RegNo = $"REG{Guid.NewGuid():N}"[..12],
            Addr1 = "1 Foreign Street",
            CityName = "Foreign City",
            StateCode = "TSTSTATE",
            CountryCode = "TSTCOUNTRY",
            PhoneNo = "+60123456789",
            CreatedBy = "test",
            CreatedByCompanyId = ownerCompanyId,
        };

        private static string DocumentJson(string supplierTin, string supplierName, string customerTin, string customerName) => $$"""
        {
          "Invoice": [
            {
              "DocumentCurrencyCode": [{ "_": "MYR" }],
              "AccountingSupplierParty": [
                { "Party": [ {
                  "PartyIdentification": [ { "ID": [ { "schemeID": "TIN", "_": "{{supplierTin}}" } ] } ],
                  "PartyLegalEntity": [ { "RegistrationName": [ { "_": "{{supplierName}}" } ] } ]
                } ] }
              ],
              "AccountingCustomerParty": [
                { "Party": [ {
                  "PartyIdentification": [ { "ID": [ { "schemeID": "TIN", "_": "{{customerTin}}" } ] } ],
                  "PartyLegalEntity": [ { "RegistrationName": [ { "_": "{{customerName}}" } ] } ]
                } ] }
              ]
            }
          ]
        }
        """;

        [Fact]
        public async Task SyncAllFromApiAsync_NeverOverwritesAnAlreadySetBuyer_OnReSyncOfAnExistingInvoice()
        {
            if (!_fx.Available) return;

            var services = new ServiceCollection();
            services.AddScoped(_ => _fx.CreateContext());
            await using var provider = services.BuildServiceProvider();

            string realBuyerCompanyId;
            string uuid = $"UUID-{Guid.NewGuid():N}";
            string invoiceNo = $"INV-SYNC-{Guid.NewGuid():N}"[..20];
            int realBuyerId;

            await using (var seed = _fx.CreateContext())
            {
                await SeedLookupsAsync(seed);

                var supplier = NewPartyInfo("Sync Test Supplier Sdn Bhd", $"C{Guid.NewGuid():N}"[..14]);
                // The pre-seeded system placeholder that shares the general TIN with the real buyer —
                // this is exactly what an unscoped TIN lookup would wrongly match.
                var placeholder = NewPartyInfo("Foreign Buyer / Shipping Recipient", GeneralTin);
                seed.PartyInfos.AddRange(supplier, placeholder);
                await seed.SaveChangesAsync();

                var realBuyer = NewPublicCustomer("PT Kustodian Sentral Efek Indonesia", GeneralTin, supplier.PartyInfoId);
                seed.PublicCustomers.Add(realBuyer);
                await seed.SaveChangesAsync();
                realBuyerId = realBuyer.PublicCustomerId;

                var status = await seed.Set<Status>().Select(s => s.StatusCode).FirstOrDefaultAsync()
                    ?? throw new InvalidOperationException("No seeded Status row found.");

                // Simulates the invoice as it existed right after creation via the UI: correctly linked
                // to the real buyer (PublicCustomerId), CustomerId null — the state PR #196 guarantees.
                seed.InvoiceHeaders.Add(new InvoiceHeader
                {
                    InvoiceNo = invoiceNo,
                    PrefixedID = invoiceNo,
                    DocTypeCode = "01",
                    Currency = "MYR",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "test",
                    InternalStatusId = status,
                    LHDNStatusId = status,
                    UUID = uuid,
                    SupplierId = supplier.PartyInfoId,
                    PublicCustomerId = realBuyerId,
                    CustomerId = null,
                });
                await seed.SaveChangesAsync();

                realBuyerCompanyId = supplier.PartyInfoId.ToString();
            }

            var helper = new InvoiceFullSyncHelper(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<InvoiceFullSyncHelper>.Instance);

            int supplierPartyInfoId = int.Parse(realBuyerCompanyId);
            string supplierTin;
            await using (var readSupplier = _fx.CreateContext())
            {
                supplierTin = (await readSupplier.PartyInfos.FindAsync(supplierPartyInfoId))!.TIN;
            }

            // Simulate a background "full sync" re-check of the now-Valid invoice — LHDN's own response
            // carries only the buyer's TIN (general/shared), same as the real staging payload.
            var summary = new DocumentSummary
            {
                uuid = uuid,
                internalId = invoiceNo,
                submissionUid = "SUB-1",
                longId = "LONG-1",
                typeName = "Invoice",
                issuerTin = supplierTin,
                receiverTin = GeneralTin,
                status = "Valid",
                document = DocumentJson(supplierTin, "Sync Test Supplier Sdn Bhd", GeneralTin, "Foreign Buyer / Shipping Recipient"),
            };

            var ok = await helper.SyncAllFromApiAsync(summary);
            Assert.True(ok);

            await using var verify = _fx.CreateContext();
            var reloaded = await verify.InvoiceHeaders.FirstAsync(i => i.UUID == uuid);

            // The bug: this used to become CustomerId = placeholder.PartyInfoId, PublicCustomerId = null.
            Assert.Equal(realBuyerId, reloaded.PublicCustomerId);
            Assert.Null(reloaded.CustomerId);
        }

        [Fact]
        public async Task SyncAllFromApiAsync_NewInvoice_ResolvesGeneralTinBuyer_AgainstCompanyScopedPublicCustomer_NotGlobalPlaceholder()
        {
            if (!_fx.Available) return;

            var services = new ServiceCollection();
            services.AddScoped(_ => _fx.CreateContext());
            await using var provider = services.BuildServiceProvider();

            string uuid = $"UUID-{Guid.NewGuid():N}";
            string invoiceNo = $"INV-SYNC-NEW-{Guid.NewGuid():N}"[..20];
            int supplierPartyInfoId;
            int realBuyerId;
            string supplierTin;

            await using (var seed = _fx.CreateContext())
            {
                await SeedLookupsAsync(seed);

                var supplier = NewPartyInfo("Sync Test Supplier 2 Sdn Bhd", $"C{Guid.NewGuid():N}"[..14]);
                var placeholder = NewPartyInfo("Foreign Buyer / Shipping Recipient 2", GeneralTin);
                seed.PartyInfos.AddRange(supplier, placeholder);
                await seed.SaveChangesAsync();
                supplierPartyInfoId = supplier.PartyInfoId;
                supplierTin = supplier.TIN;

                var realBuyer = NewPublicCustomer("PT Kustodian Sentral Efek Indonesia 2", GeneralTin, supplier.PartyInfoId);
                seed.PublicCustomers.Add(realBuyer);
                await seed.SaveChangesAsync();
                realBuyerId = realBuyer.PublicCustomerId;
            }

            var helper = new InvoiceFullSyncHelper(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<InvoiceFullSyncHelper>.Instance);

            // Invoice does not exist locally yet — SyncAllFromApiAsync's "create" branch runs.
            var summary = new DocumentSummary
            {
                uuid = uuid,
                internalId = invoiceNo,
                submissionUid = "SUB-2",
                longId = "LONG-2",
                typeName = "Invoice",
                issuerTin = supplierTin,
                receiverTin = GeneralTin,
                status = "Valid",
                document = DocumentJson(supplierTin, "Sync Test Supplier 2 Sdn Bhd", GeneralTin, "Foreign Buyer / Shipping Recipient 2"),
            };

            var ok = await helper.SyncAllFromApiAsync(summary);
            Assert.True(ok);

            await using var verify = _fx.CreateContext();
            var created = await verify.InvoiceHeaders.FirstAsync(i => i.UUID == uuid);

            // Must land on the company-scoped real buyer, never the global placeholder PartyInfo.
            Assert.Equal(realBuyerId, created.PublicCustomerId);
            Assert.Null(created.CustomerId);
        }
    }
}
