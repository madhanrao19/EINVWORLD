using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Background;
using eInvWorld.Models.Settings;
using eInvWorld.Models.Webhooks;
using eInvWorld.Services.Security;
using eInvWorld.Services.Webhooks;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// Integration tests against a REAL SQL Server (LocalDB in CI) — the runtime layer the unit tests and
    /// the in-memory provider cannot exercise (raw SQL, migrations, FKs, HasData seeds). They run only when
    /// the <c>INTEGRATION_SQLSERVER</c> environment variable is set (CI sets it); otherwise every test
    /// no-ops so the suite still passes anywhere without a database. Each run gets its own throwaway
    /// database, created via <c>Migrate()</c> and dropped on dispose.
    /// </summary>
    public sealed class SqlServerFixture : IAsyncLifetime
    {
        public string? ConnectionString { get; private set; }
        public bool Available => !string.IsNullOrWhiteSpace(ConnectionString);

        public async Task InitializeAsync()
        {
            var baseConn = Environment.GetEnvironmentVariable("INTEGRATION_SQLSERVER");
            if (string.IsNullOrWhiteSpace(baseConn)) return; // no DB in this environment → tests no-op

            // Unique throwaway catalog so parallel CI jobs (push + pull_request) never collide.
            var builder = new SqlConnectionStringBuilder(baseConn)
            {
                InitialCatalog = $"einv_it_{Guid.NewGuid():N}",
                TrustServerCertificate = true,
            };
            ConnectionString = builder.ConnectionString;

            await using var ctx = CreateContext();
            await ctx.Database.MigrateAsync(); // creates the DB and applies every migration + HasData seed
        }

        public async Task DisposeAsync()
        {
            if (!Available) return;
            await using var ctx = CreateContext();
            await ctx.Database.EnsureDeletedAsync(); // drop the throwaway DB
        }

        public ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new ApplicationDbContext(options);
        }
    }

    public class SqlServerIntegrationTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public SqlServerIntegrationTests(SqlServerFixture fx) => _fx = fx;

        // ── Schema / migrations ─────────────────────────────────────────────────────────────────
        [Fact]
        public async Task Migrations_Apply_And_Schema_Is_Queryable()
        {
            if (!_fx.Available) return; // skipped where no SQL Server is available
            await using var ctx = _fx.CreateContext();

            Assert.True(await ctx.Database.CanConnectAsync());
            Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());   // Migrate() left nothing pending
            // Prove the core table exists and is queryable. Do NOT assert emptiness — the class fixture
            // database is shared with the sibling SubmissionGuard tests, which insert rows, and xUnit's
            // run order within a class is not guaranteed.
            Assert.True(await ctx.InvoiceHeaders.CountAsync() >= 0);
        }

        // ── InvoiceSubmissionGuard: the atomic claim is raw SQL, so it needs a real database ──────
        [Fact]
        public async Task SubmissionGuard_ClaimsOnce_BlocksSecond_ReclaimsAfterRelease()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-IT-{Guid.NewGuid():N}".Substring(0, 20);
            ctx.InvoiceHeaders.Add(new eInvWorld.Models.InputModel.InvoiceHeader
            {
                InvoiceNo = invoiceNo,
                PrefixedID = invoiceNo,
                DocTypeCode = "01",
                Currency = "MYR",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test",
                InternalStatusId = status,
                // UUID + SubmissionClaimedAtUtc left null → claimable
            });
            await ctx.SaveChangesAsync();

            // First claim wins.
            Assert.True(await InvoiceSubmissionGuard.TryClaimAsync(ctx, invoiceNo));
            // Second claim is blocked while the first is fresh (not stale).
            Assert.False(await InvoiceSubmissionGuard.TryClaimAsync(ctx, invoiceNo));
            // After release, it can be claimed again.
            await InvoiceSubmissionGuard.ReleaseAsync(ctx, invoiceNo);
            Assert.True(await InvoiceSubmissionGuard.TryClaimAsync(ctx, invoiceNo));
        }

        [Fact]
        public async Task SubmissionGuard_DoesNotClaim_WhenAlreadySubmitted()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-IT-{Guid.NewGuid():N}".Substring(0, 20);
            ctx.InvoiceHeaders.Add(new eInvWorld.Models.InputModel.InvoiceHeader
            {
                InvoiceNo = invoiceNo,
                PrefixedID = invoiceNo,
                DocTypeCode = "01",
                Currency = "MYR",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test",
                InternalStatusId = status,
                UUID = "ALREADY-SUBMITTED-UUID", // has a UUID → must never be claimed
            });
            await ctx.SaveChangesAsync();

            Assert.False(await InvoiceSubmissionGuard.TryClaimAsync(ctx, invoiceNo));
        }

        // ── PII encryption backfill: raw SQL read+update, so it needs a real database ─────────────
        [Fact]
        public async Task PiiBackfill_EncryptsExistingPlaintext_InPlace_AndIsIdempotent()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext(); // no protector → stores/reads raw values

            var status = await SeedStatusAsync(ctx);
            var invoiceNo = $"INV-PII-{Guid.NewGuid():N}".Substring(0, 20);
            const string plaintext = "ACCT-PLAINTEXT-12345";
            ctx.InvoiceHeaders.Add(new eInvWorld.Models.InputModel.InvoiceHeader
            {
                InvoiceNo = invoiceNo,
                PrefixedID = invoiceNo,
                DocTypeCode = "01",
                Currency = "MYR",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "integration-test",
                InternalStatusId = status,
                BankAccountNo = plaintext, // stored as raw plaintext (this context has no converter)
            });
            await ctx.SaveChangesAsync();

            // Same provider instance for both the backfill and verification (ephemeral keys are per-instance).
            var provider = new EphemeralDataProtectionProvider();
            var protector = provider.CreateProtector(ApplicationDbContext.PiiProtectionPurpose);
            var backfill = new PiiEncryptionBackfillService(
                ctx, provider, NullLogger<PiiEncryptionBackfillService>.Instance);

            var first = await backfill.RunAsync();
            Assert.True(first.TotalEncrypted >= 1); // at least our row was encrypted

            // Read the raw stored value back (no converter on this context): it must be ciphertext now,
            // and must decrypt to the original plaintext.
            var stored = await ctx.InvoiceHeaders.AsNoTracking()
                .Where(h => h.InvoiceNo == invoiceNo)
                .Select(h => h.BankAccountNo)
                .FirstAsync();
            Assert.NotNull(stored);
            Assert.NotEqual(plaintext, stored);
            Assert.Equal(plaintext, protector.Unprotect(stored!));

            // Second run is a no-op for our row (already encrypted) — proves idempotency.
            var storedBefore = stored;
            var second = await backfill.RunAsync();
            var storedAfter = await ctx.InvoiceHeaders.AsNoTracking()
                .Where(h => h.InvoiceNo == invoiceNo)
                .Select(h => h.BankAccountNo)
                .FirstAsync();
            Assert.Equal(storedBefore, storedAfter); // untouched on the second pass
            Assert.Equal(plaintext, protector.Unprotect(storedAfter!));
        }

        // ── Webhook delivery handler: loads the subscription, signs, POSTs (raw HTTP stubbed) ─────
        [Fact]
        public async Task WebhookDelivery_Success_MarksSubscription_AndReturnsDelivered()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var sub = new WebhookSubscription
            {
                Tin = $"C{Guid.NewGuid():N}".Substring(0, 12),
                CallbackUrl = "https://receiver.example.com/hook",
                Secret = "test-secret",
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.WebhookSubscriptions.Add(sub);
            await ctx.SaveChangesAsync();

            var handler = BuildWebhookHandler(ctx, HttpStatusCode.OK, out var seenSignature);
            var job = WebhookJob(sub.Id, "INV-WH-1", "Valid");

            var msg = await handler.ExecuteAsync(job, CancellationToken.None);

            Assert.Contains("Delivered", msg);
            Assert.StartsWith("sha256=", seenSignature.Value); // the receiver got a signature header
            var reloaded = await ctx.WebhookSubscriptions.AsNoTracking().FirstAsync(s => s.Id == sub.Id);
            Assert.Equal("200 OK", reloaded.LastDeliveryResult);
            Assert.NotNull(reloaded.LastDeliveryAtUtc);
        }

        [Fact]
        public async Task WebhookDelivery_Non2xx_Throws_ForRetry()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var sub = new WebhookSubscription
            {
                Tin = $"C{Guid.NewGuid():N}".Substring(0, 12),
                CallbackUrl = "https://receiver.example.com/hook",
                Secret = "test-secret",
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.WebhookSubscriptions.Add(sub);
            await ctx.SaveChangesAsync();

            var handler = BuildWebhookHandler(ctx, HttpStatusCode.InternalServerError, out _);
            var job = WebhookJob(sub.Id, "INV-WH-2", "Valid");

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(job, CancellationToken.None));
        }

        [Fact]
        public async Task WebhookDelivery_DisabledSubscription_IsNoOp()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var sub = new WebhookSubscription
            {
                Tin = $"C{Guid.NewGuid():N}".Substring(0, 12),
                CallbackUrl = "https://receiver.example.com/hook",
                Secret = "test-secret",
                IsEnabled = false, // disabled → nothing should be sent
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.WebhookSubscriptions.Add(sub);
            await ctx.SaveChangesAsync();

            var handler = BuildWebhookHandler(ctx, HttpStatusCode.OK, out var seenSignature, out var sendCount);
            var job = WebhookJob(sub.Id, "INV-WH-3", "Valid");

            var msg = await handler.ExecuteAsync(job, CancellationToken.None);

            Assert.Contains("disabled", msg, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, sendCount.Value); // the receiver was never contacted
        }

        private static SyncJob WebhookJob(int subId, string invoiceNo, string status) => new()
        {
            Id = 1,
            Tin = "C-test",
            JobType = SyncJobType.WebhookDelivery,
            PayloadJson = EINVWORLD.Services.Background.SyncJobPayload.CreateForWebhook(subId, invoiceNo, status, "uuid-1")
        };

        private static WebhookDeliveryJobHandler BuildWebhookHandler(
            ApplicationDbContext ctx, HttpStatusCode code, out StrongBox<string> seenSignature) =>
            BuildWebhookHandler(ctx, code, out seenSignature, out _);

        private static WebhookDeliveryJobHandler BuildWebhookHandler(
            ApplicationDbContext ctx, HttpStatusCode code, out StrongBox<string> seenSignature, out StrongBox<int> sendCount)
        {
            var sig = new StrongBox<string>("");
            var count = new StrongBox<int>(0);
            seenSignature = sig;
            sendCount = count;

            var factory = new StubHttpClientFactory(req =>
            {
                count.Value++;
                sig.Value = req.Headers.TryGetValues("X-EInvWorld-Signature", out var v)
                    ? string.Join("", v) : "";
                return new HttpResponseMessage(code);
            });

            // BlockPrivateNetworks=false keeps the test hermetic (no DNS resolution of example.com).
            var settings = Options.Create(new WebhookSettings { BlockPrivateNetworks = false, RequireHttps = false });
            return new WebhookDeliveryJobHandler(ctx, factory, settings, NullLogger<WebhookDeliveryJobHandler>.Instance);
        }

        private sealed class StrongBox<T>
        {
            public T Value;
            public StrongBox(T value) => Value = value;
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
            public HttpClient CreateClient(string name) => new(new StubHandler(_responder));

            private sealed class StubHandler : HttpMessageHandler
            {
                private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
                public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                    => Task.FromResult(_responder(request));
            }
        }

        /// <summary>
        /// Returns a valid Status key (StatusCode) to satisfy the InternalStatus FK. Migrations seed
        /// Status via HasData, so an existing code is reused; a throwaway is inserted only if none exist.
        /// </summary>
        private static async Task<string> SeedStatusAsync(ApplicationDbContext ctx)
        {
            var existing = await ctx.Set<eInvWorld.Models.Status>().Select(s => s.StatusCode).FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(existing)) return existing;

            var status = new eInvWorld.Models.Status
            {
                StatusCode = $"IT{Guid.NewGuid():N}".Substring(0, 20), // StatusCode is the [Key], max length 20
                StatusType = "Internal",
                Name = "Draft",
            };
            ctx.Set<eInvWorld.Models.Status>().Add(status);
            await ctx.SaveChangesAsync();
            return status.StatusCode;
        }
    }
}
