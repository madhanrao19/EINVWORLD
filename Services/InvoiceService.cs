using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Services
{
    /// <summary>
    /// Single source of truth for generating the next sequential invoice number (EINVxxxxx).
    /// Consolidates logic that was previously copy-pasted across 6 page models. The max is computed
    /// NUMERICALLY (not by string sort, which breaks past EINV99999) and parsed defensively so
    /// non-standard numbers such as "EINV00042(1)" (produced by the LHDN sync) don't throw.
    /// Uniqueness is ultimately guaranteed by the InvoiceNo primary key; this just avoids collisions.
    /// </summary>
    public class InvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(ApplicationDbContext context, ILogger<InvoiceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>Reserves and returns the next unused EINV number (checks headers + history).</summary>
        public string GenerateNextInvoiceNumber()
        {
            const int maxRetries = 10;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    int next = CurrentMaxNumber() + 1 + attempt; // +attempt nudges past a concurrent collision
                    string candidate = $"EINV{next:D5}";

                    bool exists = _context.InvoiceHeaders.Any(i => i.InvoiceNo == candidate)
                               || _context.InvoiceHistories.Any(h => h.InvoiceNo == candidate);
                    if (!exists)
                    {
                        _logger.LogDebug("Generated invoice number {InvoiceNo} (attempt {Attempt})", candidate, attempt + 1);
                        return candidate;
                    }

                    _logger.LogWarning("Invoice number {InvoiceNo} already exists, retrying (attempt {Attempt})", candidate, attempt + 1);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating invoice number (attempt {Attempt})", attempt + 1);
                }
            }

            // Last resort: a timestamp-derived number so creation never hard-fails.
            var fallback = $"EINV{DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString().Substring(5)}";
            _logger.LogWarning("Falling back to timestamp invoice number {InvoiceNo}", fallback);
            return fallback;
        }

        /// <summary>Returns the next candidate number WITHOUT reserving it (for UI preview only).</summary>
        public string PreviewNextInvoiceNumber() => $"EINV{CurrentMaxNumber() + 1:D5}";

        private int CurrentMaxNumber()
        {
            return Math.Max(
                MaxEinvNumber(_context.InvoiceHeaders.Select(i => i.InvoiceNo)),
                MaxEinvNumber(_context.InvoiceHistories.Select(h => h.InvoiceNo)));
        }

        // Pulls only the EINV-prefixed numeric parts and computes the max in memory, so ordering is
        // by numeric value (string OrderByDescending would rank "EINV100000" below "EINV99999").
        private static int MaxEinvNumber(IQueryable<string?> invoiceNos)
        {
            var parts = invoiceNos
                .Where(n => n != null && n.StartsWith("EINV") && n.Length > 4)
                .Select(n => n!.Substring(4))
                .ToList();

            int max = 0;
            foreach (var part in parts)
                if (int.TryParse(part, out int value) && value > max)
                    max = value;
            return max;
        }
    }
}
