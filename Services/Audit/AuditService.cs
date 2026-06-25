using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.Audit
{
    /// <summary>
    /// Append-only, hash-chained audit log. Appends are serialised process-wide (single-instance on-prem)
    /// so the "previous row" read and the new row write can't race and fork the chain. Each write runs in
    /// its own DI scope / DbContext so it is isolated from whatever the calling code is doing.
    /// </summary>
    public sealed class AuditService : IAuditService
    {
        // Genesis link for the very first row — a fixed, well-known constant.
        private const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

        private static readonly SemaphoreSlim ChainLock = new(1, 1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AuditService> _log;

        public AuditService(IServiceScopeFactory scopeFactory, IHttpContextAccessor http, ILogger<AuditService> log)
        {
            _scopeFactory = scopeFactory;
            _http = http;
            _log = log;
        }

        public async Task WriteAsync(string action, AuditEntry entry, CancellationToken ct = default)
        {
            try
            {
                var ctx = _http.HttpContext;
                var row = new AuditLog
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    Action = Trunc(action, 80) ?? string.Empty,
                    // Fall back to the request's correlation id (set by CorrelationIdMiddleware) so an audit
                    // row can be tied to the request's log lines even when the caller didn't pass one.
                    CorrelationId = Trunc(entry.CorrelationId ?? ctx?.TraceIdentifier, 64),
                    Tin = Trunc(entry.Tin, 50),
                    InvoiceNo = Trunc(entry.InvoiceNo, 100),
                    Uuid = Trunc(entry.Uuid, 100),
                    OldValueJson = entry.OldValueJson,
                    NewValueJson = entry.NewValueJson,
                    UserId = Trunc(ctx?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, 450),
                    UserName = Trunc(entry.UserNameOverride ?? ctx?.User?.Identity?.Name, 256),
                    IpAddress = Trunc(ctx?.Connection?.RemoteIpAddress?.ToString(), 64),
                    UserAgent = Trunc(ctx?.Request?.Headers["User-Agent"].ToString(), 512),
                };

                await ChainLock.WaitAsync(ct);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    row.PreviousHash = await db.Set<AuditLog>()
                        .OrderByDescending(a => a.Id)
                        .Select(a => a.RowHash)
                        .FirstOrDefaultAsync(ct) ?? Genesis;

                    row.RowHash = ComputeRowHash(row);

                    db.Set<AuditLog>().Add(row);
                    await db.SaveChangesAsync(ct);
                }
                finally
                {
                    ChainLock.Release();
                }
            }
            catch (Exception ex)
            {
                // Auditing must never break the audited operation; log and move on.
                _log.LogError(ex, "Failed to write audit entry for action {Action}", action);
            }
        }

        public async Task<AuditVerificationResult> VerifyChainAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            long checked_ = 0;
            string expectedPrev = Genesis;

            // Stream in Id order; recompute each row's hash from its own fields + stored PreviousHash.
            var rows = db.Set<AuditLog>().AsNoTracking().OrderBy(a => a.Id).AsAsyncEnumerable();
            await foreach (var row in rows.WithCancellation(ct))
            {
                checked_++;

                if (row.PreviousHash != expectedPrev)
                    return new AuditVerificationResult(false, checked_, row.Id,
                        $"Row {row.Id}: previous-hash link is broken (a row was inserted or deleted before it).");

                if (ComputeRowHash(row) != row.RowHash)
                    return new AuditVerificationResult(false, checked_, row.Id,
                        $"Row {row.Id}: contents were altered after it was written.");

                expectedPrev = row.RowHash;
            }

            return new AuditVerificationResult(true, checked_, null,
                checked_ == 0 ? "No audit rows yet." : $"Chain verified: {checked_} row(s) intact.");
        }

        /// <summary>Canonical hash of a row's content chained onto its PreviousHash. Excludes Id and RowHash.</summary>
        private static string ComputeRowHash(AuditLog r)
        {
            var canonical = string.Join("\u241F", new[]
            {
                r.PreviousHash,
                r.CreatedAtUtc.ToString("O"),
                r.Action,
                r.CorrelationId, r.UserId, r.UserName, r.Tin, r.InvoiceNo, r.Uuid,
                r.OldValueJson, r.NewValueJson, r.IpAddress, r.UserAgent
            });

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

        private static string? Trunc(string? v, int max) =>
            string.IsNullOrEmpty(v) || v.Length <= max ? v : v.Substring(0, max);
    }
}
