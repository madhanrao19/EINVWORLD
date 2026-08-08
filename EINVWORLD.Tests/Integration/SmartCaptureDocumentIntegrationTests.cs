using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models.Background;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.SmartCapture;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Background;
using EINVWORLD.Services.Security;
using EINVWORLD.Services.SmartCapture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using eInvWorld.Models;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>No-op audit sink for tests that don't care about the audit trail itself, only the
    /// behaviour under test.</summary>
    internal sealed class NullAuditService : IAuditService
    {
        public Task WriteAsync(string action, AuditEntry entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task<AuditVerificationResult> VerifyChainAsync(CancellationToken ct = default) =>
            Task.FromResult(new AuditVerificationResult(true, 0, null, "n/a"));
    }

    /// <summary>Always reports "clean" — these tests exercise tenant scoping and quota/retention logic,
    /// not the ClamAV wire protocol (covered separately by not needing a live daemon in CI).</summary>
    internal sealed class AlwaysCleanMalwareScanner : IMalwareScanner
    {
        public bool IsConfigured => true;
        public Task<MalwareScanResult> ScanAsync(byte[] content, CancellationToken ct) =>
            Task.FromResult(new MalwareScanResult(MalwareScanOutcome.Clean));
    }

    /// <summary>
    /// Real-SQL-Server tests (via the shared SqlServerFixture — see SqlServerIntegrationTests.cs) for the
    /// tenant-isolation guarantee Smart Capture depends on: SmartCaptureDocumentService must never return a
    /// document to a user who isn't a member (via UserCompanies) of the owning company. This is exactly the
    /// class of bug the roast flagged as the top risk (no global EF tenant filter — isolation is per-query
    /// discipline), so it gets a real database, not the in-memory provider, to prove the LINQ translates
    /// and behaves correctly against actual SQL Server.
    /// </summary>
    public class SmartCaptureDocumentIntegrationTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public SmartCaptureDocumentIntegrationTests(SqlServerFixture fx) => _fx = fx;

        private static SmartCaptureDocumentService CreateService(eInvWorld.Data.ApplicationDbContext ctx, SmartCaptureOptions? options = null)
        {
            var filePathConfig = Options.Create(new FilePathConfig
            {
                SmartCaptureFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "einv-smartcapture-tests")
            });
            options ??= new SmartCaptureOptions { Enabled = true, MalwareScanRequired = false, MonthlyProcessedPageQuota = 0 };
            return new SmartCaptureDocumentService(
                ctx, filePathConfig, options, new AlwaysCleanMalwareScanner(), new NullAuditService(),
                NullLogger<SmartCaptureDocumentService>.Instance);
        }

        /// <summary>
        /// Builds a PartyInfo that satisfies every NOT NULL / FK constraint on the real table. The
        /// reference tables (RegistrationTypes/StateCodes/CountryCodes) turn out NOT to be migration-time
        /// HasData seeds — they're populated by a runtime seeder/sync job, so a freshly-migrated throwaway
        /// test database has zero rows in them. Insert one fixed row into each, idempotently (the
        /// SqlServerFixture database is shared across every test method in this class), rather than
        /// assuming seed data exists.
        /// </summary>
        private static async Task<PartyInfo> CreateValidPartyInfoAsync(eInvWorld.Data.ApplicationDbContext ctx, string name)
        {
            const string regTypeCode = "TSTREG";
            const string stateCodeValue = "TSTSTATE";
            const string countryCodeValue = "TSTCOUNTRY";

            if (!await ctx.RegistrationTypes.AnyAsync(r => r.Code == regTypeCode))
                ctx.RegistrationTypes.Add(new RegistrationType { Code = regTypeCode, Name = "Test Registration Type" });
            if (!await ctx.StateCodes.AnyAsync(s => s.Code == stateCodeValue))
                ctx.StateCodes.Add(new eInvWorld.Models.StateCode { Code = stateCodeValue, State = "Test State", IsActive = true });
            if (!await ctx.CountryCodes.AnyAsync(c => c.Code == countryCodeValue))
                ctx.CountryCodes.Add(new eInvWorld.Models.CountryCode { Code = countryCodeValue, Country = "Testland", IsActive = true, UpdatedBy = "test" });
            await ctx.SaveChangesAsync();

            return new PartyInfo
            {
                CompanyName = name,
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
        }

        /// <summary>UserCompanies.UserId has a real FK to AspNetUsers — a bare made-up string fails with a
        /// FOREIGN KEY constraint violation, so tests need an actual ApplicationUser row.</summary>
        private static async Task<string> CreateUserAsync(eInvWorld.Data.ApplicationDbContext ctx, string label)
        {
            var id = Guid.NewGuid().ToString();
            ctx.Users.Add(new eInvWorld.Models.ApplicationUser
            {
                Id = id,
                UserName = $"{label}-{id}@test.local",
                NormalizedUserName = $"{label}-{id}@test.local".ToUpperInvariant(),
                Email = $"{label}-{id}@test.local",
                NormalizedEmail = $"{label}-{id}@test.local".ToUpperInvariant(),
                FullName = label,
            });
            await ctx.SaveChangesAsync();
            return id;
        }

        [Fact]
        public async Task GetOwnedAsync_Returns_Null_For_A_User_Outside_The_Owning_Company()
        {
            if (!_fx.Available) return; // skipped where no SQL Server is available
            await using var ctx = _fx.CreateContext();

            var companyA = await CreateValidPartyInfoAsync(ctx, "Company A");
            var companyB = await CreateValidPartyInfoAsync(ctx, "Company B");
            ctx.PartyInfos.AddRange(companyA, companyB);
            await ctx.SaveChangesAsync();

            var userAId = await CreateUserAsync(ctx, "userA");
            var userBId = await CreateUserAsync(ctx, "userB");
            ctx.UserCompanies.Add(new UserCompany { UserId = userAId, PartyInfoId = companyA.PartyInfoId });
            ctx.UserCompanies.Add(new UserCompany { UserId = userBId, PartyInfoId = companyB.PartyInfoId });
            await ctx.SaveChangesAsync();

            var document = new SmartCaptureDocument
            {
                CompanyPartyInfoId = companyA.PartyInfoId,
                UploadedByUserId = userAId,
                OriginalFileName = "invoice.pdf",
                InternalStorageReference = "irrelevant.pdf",
                ContentType = "application/pdf",
                FileSize = 123,
                Status = SmartCaptureDocumentStatus.Uploaded,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            ctx.SmartCaptureDocuments.Add(document);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);

            var ownedByA = await service.GetOwnedAsync(document.Id, userAId, CancellationToken.None);
            var attemptByB = await service.GetOwnedAsync(document.Id, userBId, CancellationToken.None);

            Assert.NotNull(ownedByA);
            Assert.Equal(document.Id, ownedByA!.Id);
            Assert.Null(attemptByB); // cross-tenant access must be invisible, not just "forbidden"
        }

        [Fact]
        public async Task CheckQuotaAsync_Unlimited_When_Quota_Is_Zero()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "Quota Co");
            ctx.PartyInfos.Add(company);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx); // MonthlyProcessedPageQuota = 0 → unlimited
            Assert.True(await service.CheckQuotaAsync(company.PartyInfoId, CancellationToken.None));
        }

        [Fact]
        public async Task CheckQuotaAsync_Blocks_Once_Processed_Pages_Reach_The_Monthly_Limit()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "Quota Co 2");
            ctx.PartyInfos.Add(company);
            await ctx.SaveChangesAsync();

            ctx.SmartCaptureDocuments.Add(new SmartCaptureDocument
            {
                CompanyPartyInfoId = company.PartyInfoId,
                UploadedByUserId = "someone",
                OriginalFileName = "a.pdf",
                InternalStorageReference = "a.pdf",
                ContentType = "application/pdf",
                FileSize = 1,
                PageCount = 5,
                Status = SmartCaptureDocumentStatus.ReviewRequired,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx, new SmartCaptureOptions { Enabled = true, MonthlyProcessedPageQuota = 5 });
            Assert.False(await service.CheckQuotaAsync(company.PartyInfoId, CancellationToken.None)); // 5 used >= 5 limit
        }

        // ── Retention-tier resolution (SmartCaptureRetentionJobHandler) ─────────────────────────────
        // ResolveRetentionWindowDaysAsync is private, so these exercise it through the public
        // ExecuteAsync entry point (SyncJobHandler contract) — a document just past its tier's window
        // gets its file deleted; a document just inside the window is left alone. Real SQL because the
        // draft-linked vs submitted-linked branch queries InvoiceHeaders.UUID.
        [Fact]
        public async Task Retention_Deletes_Failed_Document_File_Past_Its_Short_Window_But_Keeps_The_Row()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "Retention Co Failed");
            ctx.PartyInfos.Add(company);
            await ctx.SaveChangesAsync();

            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "einv-smartcapture-tests", company.PartyInfoId.ToString());
            System.IO.Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid():N}.pdf";
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(folder, fileName), "dummy");

            var document = new SmartCaptureDocument
            {
                CompanyPartyInfoId = company.PartyInfoId,
                UploadedByUserId = "someone",
                OriginalFileName = "failed.pdf",
                InternalStorageReference = fileName,
                ContentType = "application/pdf",
                FileSize = 5,
                Status = SmartCaptureDocumentStatus.Failed,
                FailureCode = "NoTextExtracted",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-20), // RetentionDaysFailed default is 14
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-20),
            };
            ctx.SmartCaptureDocuments.Add(document);
            await ctx.SaveChangesAsync();

            var filePathConfig = Options.Create(new FilePathConfig
            {
                SmartCaptureFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "einv-smartcapture-tests")
            });
            var options = new SmartCaptureOptions { Enabled = true, RetentionDaysFailed = 14 };
            var handler = new SmartCaptureRetentionJobHandler(ctx, filePathConfig, options, new NullAuditService(), NullLogger<SmartCaptureRetentionJobHandler>.Instance);

            await handler.ExecuteAsync(new SyncJob { JobType = SyncJobType.SmartCaptureRetention }, CancellationToken.None);

            var reloaded = await ctx.SmartCaptureDocuments.FirstAsync(d => d.Id == document.Id);
            Assert.NotNull(reloaded); // row is kept
            Assert.NotNull(reloaded.FileDeletedAtUtc); // but the file is gone
            Assert.False(System.IO.File.Exists(System.IO.Path.Combine(folder, fileName)));
        }

        [Fact]
        public async Task Retention_Leaves_A_Document_Inside_Its_Window_Untouched()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "Retention Co Fresh");
            ctx.PartyInfos.Add(company);
            await ctx.SaveChangesAsync();

            var document = new SmartCaptureDocument
            {
                CompanyPartyInfoId = company.PartyInfoId,
                UploadedByUserId = "someone",
                OriginalFileName = "fresh.pdf",
                InternalStorageReference = "does-not-need-to-exist.pdf",
                ContentType = "application/pdf",
                FileSize = 5,
                Status = SmartCaptureDocumentStatus.Failed,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1), // well inside the 14-day default window
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
            };
            ctx.SmartCaptureDocuments.Add(document);
            await ctx.SaveChangesAsync();

            var filePathConfig = Options.Create(new FilePathConfig
            {
                SmartCaptureFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "einv-smartcapture-tests")
            });
            var options = new SmartCaptureOptions { Enabled = true, RetentionDaysFailed = 14 };
            var handler = new SmartCaptureRetentionJobHandler(ctx, filePathConfig, options, new NullAuditService(), NullLogger<SmartCaptureRetentionJobHandler>.Instance);

            await handler.ExecuteAsync(new SyncJob { JobType = SyncJobType.SmartCaptureRetention }, CancellationToken.None);

            var reloaded = await ctx.SmartCaptureDocuments.FirstAsync(d => d.Id == document.Id);
            Assert.Null(reloaded.FileDeletedAtUtc); // too soon — must not have been touched
        }

        [Fact]
        public async Task Retention_Uses_The_Long_SubmittedLinked_Window_When_The_Linked_Invoice_Has_A_Uuid()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "Retention Co Submitted");
            var buyer = await CreateValidPartyInfoAsync(ctx, "Retention Co Buyer");
            ctx.PartyInfos.AddRange(company, buyer);
            await ctx.SaveChangesAsync();

            var invoiceNo = $"EINV-TEST-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo,
                PrefixedID = invoiceNo,
                CreatedDate = DateTime.UtcNow,
                DocTypeCode = "01",
                Currency = "MYR",
                SupplierId = company.PartyInfoId,
                CustomerId = buyer.PartyInfoId,
                UUID = "LHDN-REAL-UUID-123", // present → "submitted", not just a local draft
                CreatedBy = "test",
                InternalStatusId = await ctx.Statuses.Select(s => s.StatusCode).FirstAsync(), // Statuses IS migration-seeded (unlike State/Country/RegType above)
            });
            await ctx.SaveChangesAsync();

            var document = new SmartCaptureDocument
            {
                CompanyPartyInfoId = company.PartyInfoId,
                UploadedByUserId = "someone",
                OriginalFileName = "submitted.pdf",
                InternalStorageReference = "does-not-need-to-exist.pdf",
                ContentType = "application/pdf",
                FileSize = 5,
                Status = SmartCaptureDocumentStatus.DraftCreated,
                RelatedInvoiceHeaderInvoiceNo = invoiceNo,
                // Older than the short RetentionDaysDraftLinked window used in this test, but nowhere near
                // the long RetentionDaysSubmittedLinked window — proves the handler picked the submitted
                // tier, not the draft tier, purely from InvoiceHeaders.UUID being set.
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-10),
            };
            ctx.SmartCaptureDocuments.Add(document);
            await ctx.SaveChangesAsync();

            var filePathConfig = Options.Create(new FilePathConfig
            {
                SmartCaptureFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "einv-smartcapture-tests")
            });
            var options = new SmartCaptureOptions { Enabled = true, RetentionDaysDraftLinked = 5, RetentionDaysSubmittedLinked = 2555 };
            var handler = new SmartCaptureRetentionJobHandler(ctx, filePathConfig, options, new NullAuditService(), NullLogger<SmartCaptureRetentionJobHandler>.Instance);

            await handler.ExecuteAsync(new SyncJob { JobType = SyncJobType.SmartCaptureRetention }, CancellationToken.None);

            var reloaded = await ctx.SmartCaptureDocuments.FirstAsync(d => d.Id == document.Id);
            // 10 days old > RetentionDaysDraftLinked(5) but << RetentionDaysSubmittedLinked(2555) — if the
            // handler wrongly used the short draft-tier window, the file would have been deleted here.
            Assert.Null(reloaded.FileDeletedAtUtc);
        }

        // ── Extraction-job idempotency (SmartCaptureExtractionJobHandler) ───────────────────────────
        [Fact]
        public async Task ExtractionJob_Skips_Reprocessing_A_Document_Already_Past_Extraction()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();

            var company = await CreateValidPartyInfoAsync(ctx, "Idempotency Co");
            ctx.PartyInfos.Add(company);
            await ctx.SaveChangesAsync();

            // Already ReviewRequired — as if a prior attempt succeeded but the worker crashed before
            // marking the SyncJob row Completed, so the durable worker retries the same job.
            var document = new SmartCaptureDocument
            {
                CompanyPartyInfoId = company.PartyInfoId,
                UploadedByUserId = "someone",
                OriginalFileName = "already-done.pdf",
                InternalStorageReference = "does-not-need-to-exist-for-this-test.pdf",
                ContentType = "application/pdf",
                FileSize = 5,
                Status = SmartCaptureDocumentStatus.ReviewRequired,
                NormalizedExtractionJson = "{\"SuggestionJson\":\"{}\",\"ReviewItems\":[],\"ReviewHasErrors\":false,\"ReviewReadyForForm\":true}",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            ctx.SmartCaptureDocuments.Add(document);
            await ctx.SaveChangesAsync();
            var originalExtractionJson = document.NormalizedExtractionJson;

            var filePathConfig = Options.Create(new FilePathConfig
            {
                SmartCaptureFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "einv-smartcapture-tests-nonexistent")
            });
            var options = new SmartCaptureOptions { Enabled = true, MaxPages = 15 };
            var handler = new EINVWORLD.Services.SmartCapture.SmartCaptureExtractionJobHandler(
                ctx, filePathConfig, options,
                textExtractor: null!, ocr: null!, assistant: null!, // never touched if the idempotency
                                                                     // short-circuit works — a NullReferenceException here would mean it re-ran extraction
                audit: new NullAuditService(), NullLogger<EINVWORLD.Services.SmartCapture.SmartCaptureExtractionJobHandler>.Instance);

            var job = new SyncJob
            {
                JobType = SyncJobType.SmartCaptureExtraction,
                PayloadJson = SyncJobPayload.CreateForSmartCaptureDocument(document.Id),
            };

            var result = await handler.ExecuteAsync(job, CancellationToken.None);

            Assert.Contains("already processed", result, StringComparison.OrdinalIgnoreCase);
            var reloaded = await ctx.SmartCaptureDocuments.FirstAsync(d => d.Id == document.Id);
            Assert.Equal(SmartCaptureDocumentStatus.ReviewRequired, reloaded.Status); // unchanged
            Assert.Equal(originalExtractionJson, reloaded.NormalizedExtractionJson); // not overwritten
        }

        // ── Confirm-race (SmartCaptureReviewModel.OnPostConfirmAsync) ───────────────────────────────
        [Fact]
        public async Task RowVersion_Conflict_Is_Thrown_When_Two_Requests_Race_To_Confirm_The_Same_Document()
        {
            // Reproduces the precondition SmartCaptureReviewModel.OnPostConfirmAsync's
            // DbUpdateConcurrencyException catch block exists for: two concurrent "Confirm" POSTs both
            // load the document (as two separate requests/DbContexts would), the first commits its
            // status update, and the second — still holding its own copy's original RowVersion — must
            // fail with a concurrency conflict, not silently overwrite the winner's result.
            if (!_fx.Available) return;

            await using var seedCtx = _fx.CreateContext();
            var company = await CreateValidPartyInfoAsync(seedCtx, "Race Co");
            seedCtx.PartyInfos.Add(company);
            await seedCtx.SaveChangesAsync();

            var document = new SmartCaptureDocument
            {
                CompanyPartyInfoId = company.PartyInfoId,
                UploadedByUserId = "someone",
                OriginalFileName = "race.pdf",
                InternalStorageReference = "race.pdf",
                ContentType = "application/pdf",
                FileSize = 5,
                Status = SmartCaptureDocumentStatus.ReviewRequired,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            seedCtx.SmartCaptureDocuments.Add(document);
            await seedCtx.SaveChangesAsync();

            // Two independent contexts each load the same row — exactly what two concurrent HTTP
            // requests would produce (each gets its own scoped DbContext).
            await using var winnerCtx = _fx.CreateContext();
            var winnerDoc = await winnerCtx.SmartCaptureDocuments.SingleAsync(d => d.Id == document.Id);

            await using var loserCtx = _fx.CreateContext();
            var loserDoc = await loserCtx.SmartCaptureDocuments.SingleAsync(d => d.Id == document.Id);

            winnerDoc.Status = SmartCaptureDocumentStatus.DraftCreated;
            winnerDoc.RelatedInvoiceHeaderInvoiceNo = "INV-WINNER";
            await winnerCtx.SaveChangesAsync();

            loserDoc.Status = SmartCaptureDocumentStatus.DraftCreated;
            loserDoc.RelatedInvoiceHeaderInvoiceNo = "INV-LOSER";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => loserCtx.SaveChangesAsync());

            var final = await seedCtx.SmartCaptureDocuments.AsNoTracking().SingleAsync(d => d.Id == document.Id);
            Assert.Equal("INV-WINNER", final.RelatedInvoiceHeaderInvoiceNo); // loser never overwrote it
        }
    }
}
