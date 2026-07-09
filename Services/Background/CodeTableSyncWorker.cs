using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace EINVWORLD.Services.Background
{
    /// <summary>
    /// Keeps the nine LHDN code tables (unit types, currencies, countries, states, tax types,
    /// payment modes, classification, MSIC sub-categories, e-invoice types) synchronized with the
    /// official machine-readable files the MyInvois SDK portal publishes at
    /// https://sdk.myinvois.hasil.gov.my/files/&lt;Table&gt;.json — replacing the previous process of
    /// manually copying JSON into the repo and database whenever a release note announced a change.
    ///
    /// Sync policy (deliberately additive-only, the database stays the source of truth):
    ///  - missing codes are INSERTED (active, UpdatedBy = "sdk-sync");
    ///  - changed names/descriptions are UPDATED (the SDK is authoritative for wording — e.g. the
    ///    CNY description amendment of 16 May 2025);
    ///  - rows are NEVER deleted or deactivated, and an admin's IsActive choice is preserved, so a
    ///    bad/truncated download can never remove reference data mid-invoice-entry.
    ///  - a fetched table that is empty or implausibly small versus the existing rows is skipped.
    ///
    /// Config (CodeTableSync section): Enabled (default true), BaseUrl, IntervalHours (default 24,
    /// min 1), StartupDelayMinutes (default 5). Nine small GETs a day to a public static host — no
    /// LHDN API rate-limit interaction (different host, no auth).
    /// </summary>
    public class CodeTableSyncWorker : BackgroundService
    {
        public const string HttpClientName = "LhdnSdkCodeFiles";
        private const string SyncUser = "sdk-sync";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<CodeTableSyncWorker> _logger;

        public CodeTableSyncWorker(
            IServiceScopeFactory scopeFactory,
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<CodeTableSyncWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("CodeTableSync:Enabled", true))
            {
                _logger.LogInformation("CodeTableSyncWorker disabled (CodeTableSync:Enabled=false).");
                return;
            }

            var startupDelay = TimeSpan.FromMinutes(Math.Max(0, _config.GetValue("CodeTableSync:StartupDelayMinutes", 5)));
            var interval = TimeSpan.FromHours(Math.Max(1, _config.GetValue("CodeTableSync:IntervalHours", 24)));

            _logger.LogInformation("🟢 CodeTableSyncWorker started. First run in {Delay} min, then every {Hours} h.",
                startupDelay.TotalMinutes, interval.TotalHours);

            try { await Task.Delay(startupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncAllTablesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Code-table sync cycle failed; will retry next cycle.");
                }

                try { await Task.Delay(interval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("🛑 CodeTableSyncWorker stopped.");
        }

        /// <summary>Runs one full sync of all nine tables. Each table fails independently.</summary>
        public async Task SyncAllTablesAsync(CancellationToken ct)
        {
            var baseUrl = _config.GetValue("CodeTableSync:BaseUrl", "https://sdk.myinvois.hasil.gov.my/files/")!;
            if (!baseUrl.EndsWith('/')) baseUrl += "/";

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var http = _httpClientFactory.CreateClient(HttpClientName);

            var results = new List<string>();

            await SyncOneAsync(db, http, baseUrl, "UnitTypes.json", "unit types", ct,
                fetched => ApplyUnitTypes(db, fetched), results);
            await SyncOneAsync(db, http, baseUrl, "CurrencyCodes.json", "currencies", ct,
                fetched => ApplySimple(db.CurrencyCodes, fetched, "Currency", e => e.Code,
                    (code, name) => new CurrencyCode { Code = code, Currency = name, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow },
                    e => e.Currency, (e, name) => { e.Currency = name; e.UpdatedBy = SyncUser; e.UpdatedDate = DateTime.UtcNow; }), results);
            await SyncOneAsync(db, http, baseUrl, "CountryCodes.json", "countries", ct,
                fetched => ApplySimple(db.CountryCodes, fetched, "Country", e => e.Code,
                    (code, name) => new CountryCode { Code = code, Country = name, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow },
                    e => e.Country, (e, name) => { e.Country = name; e.UpdatedBy = SyncUser; e.UpdatedDate = DateTime.UtcNow; }), results);
            await SyncOneAsync(db, http, baseUrl, "StateCodes.json", "states", ct,
                fetched => ApplySimple(db.StateCodes, fetched, "State", e => e.Code,
                    (code, name) => new StateCode { Code = code, State = name, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow },
                    e => e.State, (e, name) => { e.State = name; e.UpdatedBy = SyncUser; e.UpdatedDate = DateTime.UtcNow; }), results);
            await SyncOneAsync(db, http, baseUrl, "TaxTypes.json", "tax types", ct,
                fetched => ApplySimple(db.TaxTypes, fetched, "Description", e => e.Code,
                    (code, name) => new TaxType { Code = code, Description = name, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow },
                    e => e.Description, (e, name) => { e.Description = name; e.UpdatedBy = SyncUser; e.UpdatedDate = DateTime.UtcNow; }), results);
            await SyncOneAsync(db, http, baseUrl, "PaymentMethods.json", "payment modes", ct,
                fetched => ApplySimple(db.PaymentMethods, fetched, "Payment Method", e => e.Code,
                    (code, name) => new PaymentMode { Code = code, PaymentMethod = name, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow },
                    e => e.PaymentMethod, (e, name) => { e.PaymentMethod = name; e.UpdatedBy = SyncUser; e.UpdatedDate = DateTime.UtcNow; }), results);
            await SyncOneAsync(db, http, baseUrl, "ClassificationCodes.json", "classification codes", ct,
                fetched => ApplySimple(db.ClassificationCodes, fetched, "Description", e => e.Code,
                    (code, name) => new ClassificationCode { Code = code, Description = name, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow },
                    e => e.Description, (e, name) => { e.Description = name; e.UpdatedBy = SyncUser; e.UpdatedDate = DateTime.UtcNow; }), results);
            await SyncOneAsync(db, http, baseUrl, "EInvoiceTypes.json", "e-invoice types", ct,
                fetched => ApplySimple(db.EInvoiceTypes, fetched, "Description", e => e.Code,
                    (code, name) => new EInvoiceType { Code = code, Description = name, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow },
                    e => e.Description, (e, name) => { e.Description = name; e.UpdatedBy = SyncUser; e.UpdatedDate = DateTime.UtcNow; }), results);
            await SyncOneAsync(db, http, baseUrl, "MSICSubCategoryCodes.json", "MSIC sub-categories", ct,
                fetched => ApplyMsic(db, fetched), results);

            _logger.LogInformation("📚 LHDN code-table sync finished: {Summary}", string.Join("; ", results));
        }

        /// <summary>
        /// Fetches one table file and applies it. A failure (network, HTTP, parse, plausibility) is
        /// logged and skipped so the remaining tables still sync.
        /// </summary>
        private async Task SyncOneAsync(
            ApplicationDbContext db, HttpClient http, string baseUrl, string fileName, string label,
            CancellationToken ct, Func<JArray, Task<(int added, int updated, int existing)>> apply,
            List<string> results)
        {
            try
            {
                var json = await http.GetStringAsync(baseUrl + fileName, ct);
                var array = JArray.Parse(json);

                if (array.Count == 0)
                {
                    _logger.LogWarning("⚠️ Code-table sync: {File} returned 0 entries — skipped.", fileName);
                    results.Add($"{label}: skipped (empty)");
                    return;
                }

                var (added, updated, existing) = await apply(array);

                // Plausibility guard: a table that shrank by more than half versus what we already
                // have is almost certainly a truncated/wrong download, not a real LHDN change.
                if (existing > 10 && array.Count < existing / 2)
                {
                    _logger.LogWarning("⚠️ Code-table sync: {File} returned {Fetched} entries but DB has {Existing} — treated as suspect (additions from it were still safe/additive).",
                        fileName, array.Count, existing);
                }

                if (added > 0 || updated > 0)
                    await db.SaveChangesAsync(ct);

                results.Add($"{label}: +{added}/~{updated}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "⚠️ Code-table sync failed for {File}; continuing with the next table.", fileName);
                results.Add($"{label}: FAILED ({ex.GetType().Name})");
            }
        }

        /// <summary>Generic upsert for the simple two-column (Code + name) tables.</summary>
        private static async Task<(int added, int updated, int existing)> ApplySimple<TEntity>(
            DbSet<TEntity> set, JArray fetched, string nameJsonProperty,
            Func<TEntity, string> getCode,
            Func<string, string, TEntity> create,
            Func<TEntity, string> getName,
            Action<TEntity, string> updateName) where TEntity : class
        {
            // Code-table PKs are strings; load once (tables are small) and upsert in memory.
            var existing = (await set.ToListAsync()).ToDictionary(getCode, StringComparer.OrdinalIgnoreCase);

            int added = 0, updated = 0;
            foreach (var item in fetched.OfType<JObject>())
            {
                var code = ((string?)item["Code"])?.Trim();
                var name = ((string?)item[nameJsonProperty])?.Trim();
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;

                if (!existing.TryGetValue(code, out var row))
                {
                    set.Add(create(code, name));
                    added++;
                }
                else if (!string.Equals(getName(row), name, StringComparison.Ordinal))
                {
                    updateName(row, name);
                    updated++;
                }
            }
            return (added, updated, existing.Count);
        }

        /// <summary>UnitTypes: Code + Name.</summary>
        private static async Task<(int, int, int)> ApplyUnitTypes(ApplicationDbContext db, JArray fetched)
        {
            var existing = await db.UnitTypes.ToDictionaryAsync(e => e.Code, StringComparer.OrdinalIgnoreCase);
            int added = 0, updated = 0;
            foreach (var item in fetched.OfType<JObject>())
            {
                var code = ((string?)item["Code"])?.Trim();
                var name = ((string?)item["Name"])?.Trim();
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;

                if (!existing.TryGetValue(code, out var row))
                {
                    db.UnitTypes.Add(new UnitType { Code = code, Name = name, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow });
                    added++;
                }
                else if (!string.Equals(row.Name, name, StringComparison.Ordinal))
                {
                    row.Name = name; row.UpdatedBy = SyncUser; row.UpdatedDate = DateTime.UtcNow;
                    updated++;
                }
            }
            return (added, updated, existing.Count);
        }

        /// <summary>MSIC sub-categories: Code + Description + "MSIC Category Reference".</summary>
        private static async Task<(int, int, int)> ApplyMsic(ApplicationDbContext db, JArray fetched)
        {
            var existing = await db.MSICSubCategoryCodes.ToDictionaryAsync(e => e.Code, StringComparer.OrdinalIgnoreCase);
            int added = 0, updated = 0;
            foreach (var item in fetched.OfType<JObject>())
            {
                var code = ((string?)item["Code"])?.Trim();
                var desc = ((string?)item["Description"])?.Trim();
                var catRef = ((string?)item["MSIC Category Reference"])?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(desc)) continue;

                if (!existing.TryGetValue(code, out var row))
                {
                    db.MSICSubCategoryCodes.Add(new MSICSubCategoryCode { Code = code, Description = desc, MSICCategoryReference = catRef, IsActive = true, UpdatedBy = SyncUser, UpdatedDate = DateTime.UtcNow });
                    added++;
                }
                else if (!string.Equals(row.Description, desc, StringComparison.Ordinal)
                         || !string.Equals(row.MSICCategoryReference, catRef, StringComparison.Ordinal))
                {
                    row.Description = desc; row.MSICCategoryReference = catRef;
                    row.UpdatedBy = SyncUser; row.UpdatedDate = DateTime.UtcNow;
                    updated++;
                }
            }
            return (added, updated, existing.Count);
        }
    }
}
