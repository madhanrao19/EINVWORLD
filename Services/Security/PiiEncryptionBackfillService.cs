using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using eInvWorld.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Services.Security
{
    /// <summary>
    /// One-time, admin-triggered, idempotent backfill that encrypts the existing plaintext values in the
    /// field-level PII columns (bank account numbers + secondary/tertiary address lines) in place.
    /// <para>
    /// Reads and writes each column with <em>raw SQL</em> so it bypasses the EF value converter entirely:
    /// it sees the true stored bytes, can tell already-encrypted rows from plaintext ones (a value that
    /// <see cref="IDataProtector.Unprotect(string)"/> accepts is already ciphertext and is left untouched),
    /// and writes ciphertext directly. This makes the operation safe to re-run any number of times — an
    /// interrupted run simply resumes, and a fully-encrypted table is a no-op.
    /// </para>
    /// <para>
    /// Take a full database backup before the first run (per CLAUDE.md), and ensure the DataProtection
    /// key-ring is backed up — losing it makes these columns permanently unreadable.
    /// </para>
    /// </summary>
    public sealed class PiiEncryptionBackfillService
    {
        private readonly ApplicationDbContext _db;
        private readonly IDataProtector _protector;
        private readonly ILogger<PiiEncryptionBackfillService> _logger;

        /// <summary>Table + key-column + PII-column triples processed by the backfill.</summary>
        private static readonly (string Table, string KeyColumn, string Column)[] Targets =
        {
            ("InvoiceHeaders",  "InvoiceNo",       "BankAccountNo"),
            ("PartyInfos",      "PartyInfoId",     "Addr2"),
            ("PartyInfos",      "PartyInfoId",     "Addr3"),
            ("PartyInfos",      "PartyInfoId",     "BankAccountNo"),
            ("PublicCustomers", "PublicCustomerId","Addr2"),
            ("PublicCustomers", "PublicCustomerId","Addr3"),
            ("PublicCustomers", "PublicCustomerId","BankAccountNo"),
            ("InvoiceTemplates","Id",              "BankAccountNo"),
        };

        public PiiEncryptionBackfillService(
            ApplicationDbContext db,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<PiiEncryptionBackfillService> logger)
        {
            _db = db;
            _protector = dataProtectionProvider.CreateProtector(ApplicationDbContext.PiiProtectionPurpose);
            _logger = logger;
        }

        /// <summary>
        /// Encrypts every not-yet-encrypted value across all target columns. Returns a summary of what was
        /// scanned and changed. All identifiers come from the hardcoded <see cref="Targets"/> allowlist
        /// (never user input); every value is passed as a bound parameter.
        /// </summary>
        public async Task<BackfillResult> RunAsync(CancellationToken ct = default)
        {
            var result = new BackfillResult();
            var conn = _db.Database.GetDbConnection();
            var wasClosed = conn.State != ConnectionState.Open;
            if (wasClosed)
                await conn.OpenAsync(ct).ConfigureAwait(false);

            try
            {
                foreach (var (table, keyColumn, column) in Targets)
                {
                    // 1) Read every non-empty raw value into memory (reader must be closed before updating).
                    var pending = new List<(object Key, string Cipher)>();
                    var scanned = 0;

                    using (var read = conn.CreateCommand())
                    {
                        read.CommandText =
                            $"SELECT [{keyColumn}], [{column}] FROM [{table}] " +
                            $"WHERE [{column}] IS NOT NULL AND LEN([{column}]) > 0;";
                        await using var reader = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
                        while (await reader.ReadAsync(ct).ConfigureAwait(false))
                        {
                            scanned++;
                            var key = reader.GetValue(0);
                            var stored = reader.GetString(1);
                            if (IsAlreadyEncrypted(stored))
                                continue; // leave ciphertext untouched → idempotent

                            pending.Add((key, _protector.Protect(stored)));
                        }
                    }

                    // 2) Write the freshly-encrypted values back, one bound UPDATE per row.
                    foreach (var (key, cipher) in pending)
                    {
                        ct.ThrowIfCancellationRequested();
                        using var update = conn.CreateCommand();
                        update.CommandText =
                            $"UPDATE [{table}] SET [{column}] = @val WHERE [{keyColumn}] = @key;";
                        update.Parameters.Add(Param(update, "@val", cipher));
                        update.Parameters.Add(Param(update, "@key", key));
                        await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    result.Add(table, column, scanned, pending.Count);
                    _logger.LogInformation(
                        "PII backfill: {Table}.{Column} scanned {Scanned}, encrypted {Encrypted}.",
                        table, column, scanned, pending.Count);
                }
            }
            finally
            {
                if (wasClosed && conn.State == ConnectionState.Open)
                    await conn.CloseAsync().ConfigureAwait(false);
            }

            _logger.LogInformation(
                "PII backfill complete: scanned {Scanned}, encrypted {Encrypted} value(s) across {Columns} column(s).",
                result.TotalScanned, result.TotalEncrypted, Targets.Length);
            return result;
        }

        /// <summary>
        /// A value is already encrypted iff the current key-ring can unprotect it. Anything else (legacy
        /// plaintext, or a value not in base64url form) is treated as needing encryption.
        /// </summary>
        private bool IsAlreadyEncrypted(string stored)
        {
            try
            {
                _protector.Unprotect(stored);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static DbParameter Param(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            return p;
        }

        /// <summary>Summary of a backfill run.</summary>
        public sealed class BackfillResult
        {
            private readonly List<(string Table, string Column, int Scanned, int Encrypted)> _rows = new();

            public int TotalScanned { get; private set; }
            public int TotalEncrypted { get; private set; }
            public IReadOnlyList<(string Table, string Column, int Scanned, int Encrypted)> Rows => _rows;

            internal void Add(string table, string column, int scanned, int encrypted)
            {
                _rows.Add((table, column, scanned, encrypted));
                TotalScanned += scanned;
                TotalEncrypted += encrypted;
            }

            /// <summary>Human-readable one-line summary for the admin UI / audit trail.</summary>
            public string Summary()
            {
                var sb = new StringBuilder();
                sb.Append($"Encrypted {TotalEncrypted} of {TotalScanned} scanned value(s). ");
                var changed = _rows.Where(r => r.Encrypted > 0).ToList();
                sb.Append(changed.Count == 0
                    ? "All target columns were already encrypted."
                    : string.Join(", ", changed.Select(r => $"{r.Table}.{r.Column}={r.Encrypted}")));
                return sb.ToString();
            }
        }
    }
}
