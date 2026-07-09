using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models;
using EINVWORLD.Services.Background;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Exercises <see cref="CodeTableSyncWorker"/> against a real SQL Server (same gating as the other
    /// integration tests: no-ops without INTEGRATION_SQLSERVER). HTTP is stubbed with canned SDK JSON so
    /// the test never touches the internet; what's verified is the sync policy against a real database:
    /// inserts are additive, renamed descriptions are updated, admin IsActive choices are preserved, and
    /// rows absent from the feed are never deleted.
    /// </summary>
    public class CodeTableSyncTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public CodeTableSyncTests(SqlServerFixture fx) => _fx = fx;

        private sealed class StubHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var file = request.RequestUri!.Segments.Last();
                string body = file switch
                {
                    "UnitTypes.json" => "[{\"Code\":\"IT1\",\"Name\":\"integration unit\"},{\"Code\":\"IT2\",\"Name\":\"renamed unit\"}]",
                    "CurrencyCodes.json" => "[{\"Code\":\"ITC\",\"Currency\":\"Integration Dollar\"}]",
                    "CountryCodes.json" => "[{\"Code\":\"ITX\",\"Country\":\"INTEGRATIONLAND\"}]",
                    "StateCodes.json" => "[{\"Code\":\"98\",\"State\":\"Integration State\"}]",
                    "TaxTypes.json" => "[{\"Code\":\"97\",\"Description\":\"Integration Tax\"}]",
                    "PaymentMethods.json" => "[{\"Code\":\"96\",\"Payment Method\":\"Integration Pay\"}]",
                    "ClassificationCodes.json" => "[{\"Code\":\"995\",\"Description\":\"Integration Class\"}]",
                    "EInvoiceTypes.json" => "[{\"Code\":\"94\",\"Description\":\"Integration Doc\"}]",
                    "MSICSubCategoryCodes.json" => "[{\"Code\":\"99998\",\"Description\":\"Integration MSIC\",\"MSIC Category Reference\":\"Z\"}]",
                    _ => "[]",
                };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
            }
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new HttpClient(new StubHandler());
        }

        [Fact]
        public async Task CodeTableSync_Inserts_Updates_PreservesIsActive_NeverDeletes()
        {
            if (!_fx.Available) return; // skipped where no SQL Server is available

            // Seed: IT2 exists with an outdated name and an admin's IsActive=false choice;
            // KEEP exists locally but is absent from the SDK feed and must survive.
            await using (var seed = _fx.CreateContext())
            {
                seed.UnitTypes.Add(new UnitType { Code = "IT2", Name = "old name", IsActive = false, UpdatedBy = "admin" });
                seed.UnitTypes.Add(new UnitType { Code = "KEEP", Name = "keep me", IsActive = true, UpdatedBy = "admin" });
                await seed.SaveChangesAsync();
            }

            var services = new ServiceCollection();
            services.AddScoped(_ => _fx.CreateContext());
            await using var provider = services.BuildServiceProvider();

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CodeTableSync:BaseUrl"] = "https://stub.invalid/files/",
            }).Build();

            var worker = new CodeTableSyncWorker(
                provider.GetRequiredService<IServiceScopeFactory>(),
                new StubHttpClientFactory(),
                config,
                NullLogger<CodeTableSyncWorker>.Instance);

            await worker.SyncAllTablesAsync(CancellationToken.None);

            await using var ctx = _fx.CreateContext();

            // Inserted, active, attributed to the sync.
            var it1 = await ctx.UnitTypes.FindAsync("IT1");
            Assert.NotNull(it1);
            Assert.True(it1!.IsActive);
            Assert.Equal("sdk-sync", it1.UpdatedBy);

            // Renamed by the feed; the admin's IsActive=false choice is preserved.
            var it2 = await ctx.UnitTypes.FindAsync("IT2");
            Assert.Equal("renamed unit", it2!.Name);
            Assert.False(it2.IsActive);
            Assert.Equal("sdk-sync", it2.UpdatedBy);

            // Absent from the feed — must never be deleted or deactivated.
            var keep = await ctx.UnitTypes.FindAsync("KEEP");
            Assert.NotNull(keep);
            Assert.True(keep!.IsActive);

            // The quirky JSON property names parse correctly.
            Assert.Equal("Integration Pay", (await ctx.PaymentMethods.FindAsync("96"))!.PaymentMethod);
            var msic = await ctx.MSICSubCategoryCodes.FindAsync("99998");
            Assert.Equal("Z", msic!.MSICCategoryReference);

            // Every simple table received its row.
            Assert.NotNull(await ctx.CurrencyCodes.FindAsync("ITC"));
            Assert.NotNull(await ctx.CountryCodes.FindAsync("ITX"));
            Assert.NotNull(await ctx.StateCodes.FindAsync("98"));
            Assert.NotNull(await ctx.TaxTypes.FindAsync("97"));
            Assert.NotNull(await ctx.ClassificationCodes.FindAsync("995"));
            Assert.NotNull(await ctx.EInvoiceTypes.FindAsync("94"));
        }
    }
}
