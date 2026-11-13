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
    /// It also <b>re-keys</b>: a value encrypted under a key that is no longer the key-ring's current key is
    /// re-encrypted under the current one, so an old key (for example one copied from another environment)
    /// can afterwards be removed from the ring. A value that looks like ciphertext but cannot be decrypted is
    /// never encrypted a second time; it is counted as unreadable and left as it is.
    /// </para>
    /// <para>
    /// Take a full database backup before the first run (per CLAUDE.md), and ensure the DataProtection
    /// key-ring is backed up — losing it makes these columns permanently unreadable.
    /// </para>
    /// </summary>
    public sealed class PiiEncryptionBackfillService
    {
        private readonly ApplicationDbContext _db;
        private readonly IDataProtector _piiProtector;
        private readonly IDataProtector _secretProtector;
        private readonly ILogger<PiiEncryptionBackfillService> _logger;

        /// <summary>Table + key-column + column processed by the backfill. Secret = the secret purpose, else PII.</summary>
        // LHDNTokens.AccessToken is left out on purpose: tokens live for an hour and are fetched again.
        private static readonly (string Table, string KeyColumn, string Column, bool Secret)[] Targets =
        {
            ("InvoiceHeaders",  "InvoiceNo",       "BankAccountNo", false),
            ("PartyInfos",      "PartyInfoId",     "Addr2", false),
            ("PartyInfos",      "PartyInfoId",     "Addr3", false),
            ("PartyInfos",      "PartyInfoId",     "BankAccountNo", false),
            ("PublicCustomers", "PublicCustomerId","Addr2", false),
            ("PublicCustomers", "PublicCustomerId","Addr3", false),
            ("PublicCustomers", "PublicCustomerId","BankAccountNo", false),
            ("InvoiceTemplates","Id",              "BankAccountNo", false),
            ("SupplierBankAccounts","Id",          "BankAccountNo", false),
            ("WebhookSubscriptions","Id",          "Secret", true),
        };

        public PiiEncryptionBackfillService(
            ApplicationDbContext db,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<PiiEncryptionBackfillService> logger)
        {
            _db = db;
            _piiProtector = dataProtectionProvider.CreateProtector(ApplicationDbContext.PiiProtectionPurpose);
            _secretProtector = dataProtectionProvider.CreateProtector(ApplicationDbContext.SecretProtectionPurpose);
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
                foreach (var (table, keyColumn, column, secret) in Targets)
                {
                    var protector = secret ? _secretProtector : _piiProtector;
                    // 1) Read every non-empty raw value into memory (reader must be closed before updating).
                    var pending = new List<(object Key, string Cipher)>();
                    int scanned = 0, encrypted = 0, rekeyed = 0, unreadable = 0;

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
                            var (state, plaintext) = Classify(protector, stored);
                            if (state == ValueState.Current)
                                continue; // leave current ciphertext untouched → idempotent
                            if (state == ValueState.Unreadable)
                            {
                                unreadable++; // never encrypt ciphertext a second time
                                continue;
                            }
                            if (state == ValueState.OldKey) rekeyed++; else encrypted++;
                            pending.Add((key, protector.Protect(plaintext!)));
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

                    result.Add(table, column, scanned, encrypted, rekeyed, unreadable);
                    _logger.LogInformation(
                        "PII backfill: {Table}.{Column} scanned {Scanned}, encrypted {Encrypted}, re-keyed {Rekeyed}, unreadable {Unreadable}.",
                        table, column, scanned, encrypted, rekeyed, unreadable);
                }
            }
            finally
            {
                if (wasClosed && conn.State == ConnectionState.Open)
                    await conn.CloseAsync().ConfigureAwait(false);
            }

            _logger.LogInformation(
                "PII backfill complete: scanned {Scanned}, encrypted {Encrypted}, re-keyed {Rekeyed}, unreadable {Unreadable} value(s) across {Columns} column(s).",
                result.TotalScanned, result.TotalEncrypted, result.TotalRekeyed, result.TotalUnreadable, Targets.Length);
            return result;
        }

        internal enum ValueState { Plaintext, Current, OldKey, Unreadable }

        /// <summary>
        /// Decides what a stored value is: ciphertext under the current key, ciphertext under an older key
        /// (returned decrypted, to be re-encrypted), legacy plaintext, or ciphertext no key in the ring opens.
        /// </summary>
        internal static (ValueState State, string? Plaintext) Classify(IDataProtector protector, string stored)
        {
            try
            {
                if (protector is IPersistedDataProtector persisted)
                {
                    var bytes = persisted.DangerousUnprotect(
                        Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(stored),
                        ignoreRevocationErrors: false, out var oldKey, out _);
                    return oldKey ? (ValueState.OldKey, Encoding.UTF8.GetString(bytes)) : (ValueState.Current, null);
                }
                protector.Unprotect(stored);
                return (ValueState.Current, null);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // "CfDJ8" is the base64url of the DataProtection payload header: ciphertext from a key we don't hold.
                return stored.StartsWith("CfDJ8", StringComparison.Ordinal)
                    ? (ValueState.Unreadable, null)
                    : (ValueState.Plaintext, stored);
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
            private readonly List<(string Table, string Column, int Scanned, int Encrypted, int Rekeyed, int Unreadable)> _rows = new();

            public int TotalScanned { get; private set; }
            public int TotalEncrypted { get; private set; }
            public int TotalRekeyed { get; private set; }
            public int TotalUnreadable { get; private set; }
            public IReadOnlyList<(string Table, string Column, int Scanned, int Encrypted, int Rekeyed, int Unreadable)> Rows => _rows;

            internal void Add(string table, string column, int scanned, int encrypted, int rekeyed, int unreadable)
            {
                _rows.Add((table, column, scanned, encrypted, rekeyed, unreadable));
                TotalScanned += scanned;
                TotalEncrypted += encrypted;
                TotalRekeyed += rekeyed;
                TotalUnreadable += unreadable;
            }

            /// <summary>Human-readable one-line summary for the admin UI / audit trail.</summary>
            public string Summary()
            {
                var sb = new StringBuilder();
                sb.Append($"Encrypted {TotalEncrypted} and re-keyed {TotalRekeyed} of {TotalScanned} scanned value(s). ");
                if (TotalUnreadable > 0)
                    sb.Append($"{TotalUnreadable} value(s) could not be decrypted with any key in the key-ring and were left unchanged. ");
                var changed = _rows.Where(r => r.Encrypted + r.Rekeyed > 0).ToList();
                sb.Append(changed.Count == 0
                    ? "All readable values were already encrypted with the current key."
                    : string.Join(", ", changed.Select(r => $"{r.Table}.{r.Column}={r.Encrypted + r.Rekeyed}")));
                return sb.ToString();
            }
        }
    }
}
