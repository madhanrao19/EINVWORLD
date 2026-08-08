using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Background;
using eInvWorld.Models.SmartCapture;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EINVWORLD.Services.SmartCapture
{
    public enum SmartCaptureUploadFailureReason
    {
        None,
        Disabled,
        NotAMember,
        Empty,
        TooLarge,
        UnsupportedType,
        InvalidSignature,
        QuotaExceeded,
    }

    public sealed record SmartCaptureUploadResult(bool Ok, SmartCaptureUploadFailureReason Reason, SmartCaptureDocument? Document, string? UserMessage);

    /// <summary>
    /// The single place SmartCaptureDocument is written to or queried from. Every method that returns or
    /// mutates a document takes the acting user's id and re-derives their company membership via
    /// UserCompanies — the same scoping idiom used everywhere else in the app (see
    /// CreateFromFileModel.LoadKnownBuyersAsync) — so a caller cannot "forget" the tenant filter by writing
    /// its own query against SmartCaptureDocuments directly.
    /// </summary>
    public sealed class SmartCaptureDocumentService
    {
        private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 };
        private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private readonly ApplicationDbContext _context;
        private readonly FilePathConfig _filePathConfig;
        private readonly SmartCaptureOptions _options;
        private readonly IAuditService _audit;
        private readonly ILogger<SmartCaptureDocumentService> _logger;

        public SmartCaptureDocumentService(
            ApplicationDbContext context,
            IOptions<FilePathConfig> filePathConfig,
            SmartCaptureOptions options,
            IAuditService audit,
            ILogger<SmartCaptureDocumentService> logger)
        {
            _context = context;
            _filePathConfig = filePathConfig.Value;
            _options = options;
            _audit = audit;
            _logger = logger;
        }

        /// <summary>PartyInfoIds the given user belongs to, via UserCompanies. Every scoped query below
        /// filters through this — the one place the tenant-isolation join lives for Smart Capture.</summary>
        public async Task<List<int>> GetMemberCompanyIdsAsync(string userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId)) return new List<int>();
            return await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.PartyInfoId)
                .ToListAsync(ct);
        }

        /// <summary>Fetches a document only if the user is a member of the owning company. Returns null
        /// (not the row) on any tenant mismatch — callers must treat null as 404, never leak existence.</summary>
        public async Task<SmartCaptureDocument?> GetOwnedAsync(int documentId, string userId, CancellationToken ct)
        {
            var companyIds = await GetMemberCompanyIdsAsync(userId, ct);
            if (companyIds.Count == 0) return null;

            return await _context.SmartCaptureDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && companyIds.Contains(d.CompanyPartyInfoId), ct);
        }

        public async Task<List<SmartCaptureDocument>> ListForUserAsync(string userId, CancellationToken ct)
        {
            var companyIds = await GetMemberCompanyIdsAsync(userId, ct);
            if (companyIds.Count == 0) return new List<SmartCaptureDocument>();

            return await _context.SmartCaptureDocuments
                .Where(d => companyIds.Contains(d.CompanyPartyInfoId))
                .OrderByDescending(d => d.CreatedAtUtc)
                .ToListAsync(ct);
        }

        /// <summary>Validates, persists the file, writes the SmartCaptureDocument row, and enqueues
        /// the extraction SyncJob — all in one place so no call site can skip a step. No application-level
        /// malware scanning is performed — see SmartCaptureOptions / IIS-DEPLOYMENT-GUIDE.md PART 17d for
        /// the upload-security controls this relies on instead (format/signature validation, size/page/
        /// quota limits, tenant-scoped storage outside wwwroot, safe random filenames, no execution).</summary>
        public async Task<SmartCaptureUploadResult> UploadAsync(
            byte[] content, string originalFileName, string contentType, int companyPartyInfoId, string userId, CancellationToken ct)
        {
            if (!_options.Enabled)
                return new SmartCaptureUploadResult(false, SmartCaptureUploadFailureReason.Disabled, null, "Smart Capture is disabled.");

            var companyIds = await GetMemberCompanyIdsAsync(userId, ct);
            if (!companyIds.Contains(companyPartyInfoId))
                return new SmartCaptureUploadResult(false, SmartCaptureUploadFailureReason.NotAMember, null, "You do not have access to this company.");

            if (content.Length == 0)
                return new SmartCaptureUploadResult(false, SmartCaptureUploadFailureReason.Empty, null, "Please choose a file to upload.");

            if (content.Length > _options.MaxFileSizeMb * 1024L * 1024L)
                return new SmartCaptureUploadResult(false, SmartCaptureUploadFailureReason.TooLarge, null, $"File is too large (limit {_options.MaxFileSizeMb} MB).");

            var extension = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();
            if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return new SmartCaptureUploadResult(false, SmartCaptureUploadFailureReason.UnsupportedType, null, "Unsupported file type. Allowed: " + string.Join(", ", _options.AllowedExtensions));

            if (!HasValidSignature(content, extension))
                return new SmartCaptureUploadResult(false, SmartCaptureUploadFailureReason.InvalidSignature, null, "The file's content does not match its extension.");

            var quotaOk = await CheckQuotaAsync(companyPartyInfoId, ct);
            if (!quotaOk)
                return new SmartCaptureUploadResult(false, SmartCaptureUploadFailureReason.QuotaExceeded, null, "This company's monthly Smart Capture processing quota has been reached.");

            var fileHash = Convert.ToHexString(SHA256.HashData(content));
            var internalFileName = $"{Guid.NewGuid():N}.{extension}";

            if (!SafePath.TryResolve(_filePathConfig.SmartCaptureFolder, out var fullPath, companyPartyInfoId.ToString(), internalFileName))
            {
                _logger.LogError("SafePath resolution failed for Smart Capture upload (company {CompanyId})", companyPartyInfoId);
                return new SmartCaptureUploadResult(false, SmartCaptureUploadFailureReason.InvalidSignature, null, "Could not store the uploaded file.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, content, ct);

            var now = DateTime.UtcNow;
            var document = new SmartCaptureDocument
            {
                CompanyPartyInfoId = companyPartyInfoId,
                UploadedByUserId = userId,
                OriginalFileName = Path.GetFileName(originalFileName),
                InternalStorageReference = Path.Combine(companyPartyInfoId.ToString(), internalFileName),
                ContentType = contentType,
                FileSize = content.Length,
                FileHash = fileHash,
                Status = SmartCaptureDocumentStatus.Uploaded,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            _context.SmartCaptureDocuments.Add(document);
            await _context.SaveChangesAsync(ct);

            _context.SyncJobs.Add(new SyncJob
            {
                Tin = await GetCompanyTinAsync(companyPartyInfoId, ct) ?? string.Empty,
                JobType = SyncJobType.SmartCaptureExtraction,
                Status = SyncJobStatus.Queued,
                QueuedAtUtc = now,
                MaxAttempts = 3,
                TriggeredBy = userId,
                PayloadJson = SyncJobPayload.CreateForSmartCaptureDocument(document.Id),
            });
            document.Status = SmartCaptureDocumentStatus.Queued;
            await _context.SaveChangesAsync(ct);

            await _audit.WriteAsync("SmartCaptureUploaded", new AuditEntry
            {
                NewValueJson = JsonSerializer.Serialize(new { documentId = document.Id, file = document.OriginalFileName, bytes = content.Length })
            }, ct);

            return new SmartCaptureUploadResult(true, SmartCaptureUploadFailureReason.None, document, null);
        }

        private static bool HasValidSignature(byte[] content, string extension) => extension switch
        {
            "pdf" => StartsWith(content, PdfMagic),
            "jpg" or "jpeg" => StartsWith(content, JpegMagic),
            "png" => StartsWith(content, PngMagic),
            _ => false,
        };

        private static bool StartsWith(byte[] content, byte[] magic) =>
            content.Length >= magic.Length && content.AsSpan(0, magic.Length).SequenceEqual(magic);

        private async Task<string?> GetCompanyTinAsync(int companyPartyInfoId, CancellationToken ct) =>
            await _context.PartyInfos.Where(p => p.PartyInfoId == companyPartyInfoId).Select(p => p.TIN).FirstOrDefaultAsync(ct);

        /// <summary>Monthly quota measured in successfully processed pages (documents that completed
        /// extraction), not raw upload attempts — a failed/retried job does not multiply the charge because
        /// PageCount is only ever set once, when extraction actually completes.</summary>
        public async Task<bool> CheckQuotaAsync(int companyPartyInfoId, CancellationToken ct)
        {
            if (_options.MonthlyProcessedPageQuota <= 0) return true; // 0 or negative = unlimited

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usedPages = await _context.SmartCaptureDocuments
                .Where(d => d.CompanyPartyInfoId == companyPartyInfoId && d.CreatedAtUtc >= monthStart && d.PageCount != null)
                .SumAsync(d => (int?)d.PageCount, ct) ?? 0;

            return usedPages < _options.MonthlyProcessedPageQuota;
        }
    }
}
