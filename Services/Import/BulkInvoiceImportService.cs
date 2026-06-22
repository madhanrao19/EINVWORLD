using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Services.Import
{
    public enum ImportSeverity { Error, Warning }

    public sealed record ImportIssue(ImportSeverity Severity, string Column, string Message);

    public sealed class ImportRowResult
    {
        public int RowNumber { get; init; }
        public Dictionary<string, string> Values { get; init; } = new();
        public List<ImportIssue> Issues { get; } = new();
        public bool HasError => Issues.Any(i => i.Severity == ImportSeverity.Error);
    }

    public sealed class ImportPreview
    {
        public List<ImportRowResult> Rows { get; } = new();
        public string? ParseError { get; set; }
        public int TotalRows => Rows.Count;
        public int ErrorRows => Rows.Count(r => r.HasError);
        public int OkRows => TotalRows - ErrorRows;
    }

    /// <summary>
    /// Parses an uploaded invoice spreadsheet (CSV or XLSX) and validates each row against the real
    /// LHDN reference codes + basic numeric/required rules. Validation-only: it never persists anything.
    /// One row per invoice line; rows sharing an InvoiceNo belong to the same invoice.
    /// </summary>
    public interface IBulkInvoiceImportService
    {
        /// <summary>Columns of the import template, in order.</summary>
        IReadOnlyList<string> TemplateColumns { get; }

        /// <summary>Builds a downloadable .xlsx template (header row + one example row).</summary>
        byte[] BuildTemplateXlsx();

        /// <summary>Parses + validates an uploaded CSV/XLSX. <paramref name="isCsv"/> selects the parser.</summary>
        Task<ImportPreview> ParseAndValidateAsync(Stream file, bool isCsv, CancellationToken ct = default);
    }

    public sealed class BulkInvoiceImportService : IBulkInvoiceImportService
    {
        private static readonly string[] Columns =
        {
            "InvoiceNo", "DocTypeCode", "IssueDate", "Currency", "BuyerName", "BuyerTIN",
            "LineNumber", "ItemDescription", "ClassificationCode", "UnitOfMeasure",
            "Quantity", "UnitPrice", "TaxType", "TaxRatePercent"
        };

        private static readonly HashSet<string> DocTypes =
            new(StringComparer.OrdinalIgnoreCase) { "01", "02", "03", "04", "11", "12", "13", "14" };

        private readonly ApplicationDbContext _db;

        public BulkInvoiceImportService(ApplicationDbContext db) => _db = db;

        public IReadOnlyList<string> TemplateColumns => Columns;

        public byte[] BuildTemplateXlsx()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Invoices");

            for (var c = 0; c < Columns.Length; c++)
            {
                ws.Cell(1, c + 1).Value = Columns[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
            }

            // One example row to show the expected shape.
            var example = new[]
            {
                "INV-0001", "01", DateTime.UtcNow.ToString("yyyy-MM-dd"), "MYR", "Acme Sdn Bhd", "C1234567890",
                "1", "Consulting services", "022", "C62", "1", "1000.00", "01", "8"
            };
            for (var c = 0; c < example.Length; c++)
                ws.Cell(2, c + 1).Value = example[c];

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<ImportPreview> ParseAndValidateAsync(Stream file, bool isCsv, CancellationToken ct = default)
        {
            var preview = new ImportPreview();

            List<Dictionary<string, string>> rows;
            try
            {
                rows = isCsv ? ParseCsv(file) : ParseXlsx(file);
            }
            catch (Exception ex)
            {
                preview.ParseError = "Could not read the file: " + ex.Message;
                return preview;
            }

            if (rows.Count == 0)
            {
                preview.ParseError = "No data rows found. Use the template and keep the header row.";
                return preview;
            }

            // Load the authoritative reference codes once.
            var classification = await LoadCodesAsync(_db.ClassificationCodes.Select(x => x.Code), ct);
            var taxTypes = await LoadCodesAsync(_db.TaxTypes.Select(x => x.Code), ct);
            var currencies = await LoadCodesAsync(_db.CurrencyCodes.Select(x => x.Code), ct);
            var units = await LoadCodesAsync(_db.UnitTypes.Select(x => x.Code), ct);

            var rowNum = 1; // header is row 1; first data row is 2
            foreach (var values in rows)
            {
                rowNum++;
                var r = new ImportRowResult { RowNumber = rowNum, Values = values };
                Validate(r, classification, taxTypes, currencies, units);
                preview.Rows.Add(r);
            }

            return preview;
        }

        private static void Validate(ImportRowResult r,
            ISet<string> classification, ISet<string> taxTypes, ISet<string> currencies, ISet<string> units)
        {
            string V(string col) => r.Values.TryGetValue(col, out var v) ? v.Trim() : string.Empty;

            void Err(string col, string msg) => r.Issues.Add(new(ImportSeverity.Error, col, msg));
            void Warn(string col, string msg) => r.Issues.Add(new(ImportSeverity.Warning, col, msg));

            if (string.IsNullOrWhiteSpace(V("InvoiceNo"))) Err("InvoiceNo", "Invoice number is required.");

            var docType = V("DocTypeCode");
            if (string.IsNullOrWhiteSpace(docType)) Err("DocTypeCode", "Document type is required.");
            else if (!DocTypes.Contains(docType)) Err("DocTypeCode", $"'{docType}' is not a valid LHDN document type (01–04, 11–14).");

            var issueDate = V("IssueDate");
            if (string.IsNullOrWhiteSpace(issueDate)) Err("IssueDate", "Issue date is required.");
            else if (!DateTime.TryParse(issueDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                Err("IssueDate", $"'{issueDate}' is not a valid date (use yyyy-MM-dd).");

            var currency = V("Currency");
            if (!string.IsNullOrWhiteSpace(currency) && currencies.Count > 0 && !currencies.Contains(currency))
                Warn("Currency", $"'{currency}' is not a recognised currency code.");

            if (string.IsNullOrWhiteSpace(V("BuyerTIN")))
                Warn("BuyerTIN", "Buyer TIN is missing — it must be set and validated before submission.");

            if (string.IsNullOrWhiteSpace(V("ItemDescription"))) Err("ItemDescription", "Item description is required.");

            var classCode = V("ClassificationCode");
            if (string.IsNullOrWhiteSpace(classCode)) Err("ClassificationCode", "Classification code is required.");
            else if (classification.Count > 0 && !classification.Contains(classCode))
                Err("ClassificationCode", $"'{classCode}' is not a valid LHDN classification code.");

            var unit = V("UnitOfMeasure");
            if (!string.IsNullOrWhiteSpace(unit) && units.Count > 0 && !units.Contains(unit))
                Warn("UnitOfMeasure", $"'{unit}' is not a recognised unit code.");

            ValidateDecimal(r, "Quantity", V("Quantity"), required: true, mustBePositive: true);
            ValidateDecimal(r, "UnitPrice", V("UnitPrice"), required: true, mustBeNonNegative: true);

            var taxType = V("TaxType");
            if (!string.IsNullOrWhiteSpace(taxType) && taxTypes.Count > 0 && !taxTypes.Contains(taxType))
                Warn("TaxType", $"'{taxType}' is not a recognised LHDN tax code.");

            var taxRate = V("TaxRatePercent");
            if (!string.IsNullOrWhiteSpace(taxRate) && !decimal.TryParse(taxRate, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                Warn("TaxRatePercent", $"'{taxRate}' is not a number.");
        }

        private static void ValidateDecimal(ImportRowResult r, string col, string raw,
            bool required = false, bool mustBePositive = false, bool mustBeNonNegative = false)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (required) r.Issues.Add(new(ImportSeverity.Error, col, $"{col} is required."));
                return;
            }
            if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                r.Issues.Add(new(ImportSeverity.Error, col, $"'{raw}' is not a number."));
                return;
            }
            if (mustBePositive && value <= 0) r.Issues.Add(new(ImportSeverity.Error, col, $"{col} must be greater than zero."));
            if (mustBeNonNegative && value < 0) r.Issues.Add(new(ImportSeverity.Error, col, $"{col} cannot be negative."));
        }

        private static async Task<HashSet<string>> LoadCodesAsync(IQueryable<string> query, CancellationToken ct)
        {
            try
            {
                var list = await query.ToListAsync(ct);
                return new HashSet<string>(list.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // If a reference table is unavailable, skip that check rather than failing the whole import.
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static List<Dictionary<string, string>> ParseXlsx(Stream file)
        {
            using var wb = new XLWorkbook(file);
            var ws = wb.Worksheets.First();
            var used = ws.RangeUsed();
            var result = new List<Dictionary<string, string>>();
            if (used is null) return result;

            var rowsUsed = used.RowsUsed().ToList();
            if (rowsUsed.Count < 2) return result;

            var headers = rowsUsed[0].Cells().Select(c => c.GetString().Trim()).ToList();
            foreach (var row in rowsUsed.Skip(1))
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(headers[i])) continue;
                    dict[headers[i]] = row.Cell(i + 1).GetString().Trim();
                }
                if (dict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                    result.Add(dict);
            }
            return result;
        }

        private static List<Dictionary<string, string>> ParseCsv(Stream file)
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var result = new List<Dictionary<string, string>>();

            if (!csv.Read()) return result;
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            while (csv.Read())
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in headers)
                {
                    if (string.IsNullOrWhiteSpace(h)) continue;
                    dict[h.Trim()] = (csv.GetField(h) ?? string.Empty).Trim();
                }
                if (dict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                    result.Add(dict);
            }
            return result;
        }
    }
}
