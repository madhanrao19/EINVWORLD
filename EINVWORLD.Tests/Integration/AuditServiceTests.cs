using System.Threading.Tasks;
using EINVWORLD.Services.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Exercises AuditService.VerifyChainAsync against a real SQL Server (same gating as the other
    /// integration tests: no-ops without INTEGRATION_SQLSERVER). Written specifically to catch a real
    /// DateTimeKind round-trip bug found on staging.einvworld.com: CreatedAtUtc is written with
    /// Kind=Utc (DateTime.UtcNow) but SQL Server's datetime2 has no Kind concept, so EF Core reads it
    /// back as Kind=Unspecified — without normalizing before ToString("O"), the recomputed hash never
    /// matches the stored one and "Verify chain integrity" falsely reports the very first row as
    /// tampered, on every single row, every time (the chain never reads past its first row).
    /// </summary>
    public class AuditServiceTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public AuditServiceTests(SqlServerFixture fx) => _fx = fx;

        private AuditService CreateService()
        {
            var services = new ServiceCollection();
            services.AddScoped(_ => _fx.CreateContext());
            var provider = services.BuildServiceProvider();

            return new AuditService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                new HttpContextAccessor(), // no ambient HttpContext — mirrors a background-job caller
                NullLogger<AuditService>.Instance);
        }

        [Fact]
        public async Task VerifyChainAsync_AfterMultipleWrites_ReportsChainIntact()
        {
            if (!_fx.Available) return; // skipped where no SQL Server is available

            var audit = CreateService();

            await audit.WriteAsync("Test.RowOne", new AuditEntry { InvoiceNo = "EINV-TEST-1" });
            await audit.WriteAsync("Test.RowTwo", new AuditEntry { InvoiceNo = "EINV-TEST-2" });
            await audit.WriteAsync("Test.RowThree", new AuditEntry { InvoiceNo = "EINV-TEST-3" });

            var result = await audit.VerifyChainAsync();

            Assert.True(result.Ok, result.Message);
            Assert.Null(result.FirstBrokenId);
            Assert.True(result.RowsChecked >= 3);
        }
    }
}
