using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EINVWORLD.Services.Assistant
{
    /// <summary>Typed shape of the JSON the assistant returns from <c>SuggestInvoiceAsync</c>.</summary>
    public sealed class InvoiceSuggestion
    {
        public string? DocumentType { get; set; }
        public string? DocumentTypeName { get; set; }
        public string? Currency { get; set; }
        public string? BuyerName { get; set; }
        public string? BuyerTin { get; set; }
        public List<SuggestionLine> LineItems { get; set; } = new();
        public string? TaxType { get; set; }
        public decimal? TaxRatePercent { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class SuggestionLine
    {
        public string? Description { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? ClassificationCode { get; set; }
    }

    public enum CheckSeverity { Ok, Warning, Error }

    public sealed record CheckItem(CheckSeverity Severity, string Message);

    /// <summary>Result of reviewing a suggestion — a readiness checklist shown to the user.</summary>
    public sealed class SuggestionReview
    {
        public bool Parsed { get; init; }
        public List<CheckItem> Items { get; } = new();

        public bool HasErrors => Items.Any(i => i.Severity == CheckSeverity.Error);
        public bool HasWarnings => Items.Any(i => i.Severity == CheckSeverity.Warning);

        /// <summary>True when the suggestion is safe to load into the Create Invoice form (no hard errors).</summary>
        public bool ReadyForForm => Parsed && !HasErrors;

        internal void Ok(string m) => Items.Add(new(CheckSeverity.Ok, m));
        internal void Warn(string m) => Items.Add(new(CheckSeverity.Warning, m));
        internal void Error(string m) => Items.Add(new(CheckSeverity.Error, m));
    }

    /// <summary>
    /// Validates a model-produced invoice suggestion against the real LHDN reference data and basic
    /// numeric/required-field rules. Pure and deterministic (no I/O) so it is unit-testable and can be
    /// trusted to catch the model's hallucinations BEFORE anything is created or submitted.
    /// </summary>
    public static class InvoiceSuggestionValidator
    {
        private static readonly HashSet<string> DocTypes =
            new(System.StringComparer.OrdinalIgnoreCase) { "01", "02", "03", "04", "11", "12", "13", "14" };

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        /// <summary>Parses the raw JSON returned by the model; returns null when it cannot be parsed.</summary>
        public static InvoiceSuggestion? TryParse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<InvoiceSuggestion>(json, JsonOpts); }
            catch (JsonException) { return null; }
        }

        /// <summary>
        /// Reviews a suggestion against the allowed classification/tax codes and basic rules.
        /// <paramref name="classificationCodes"/> / <paramref name="taxCodes"/> may be empty (then those
        /// checks are skipped rather than failing). Returns a checklist for the UI.
        /// </summary>
        public static SuggestionReview Review(
            InvoiceSuggestion? suggestion,
            ISet<string>? classificationCodes,
            ISet<string>? taxCodes)
        {
            if (suggestion is null)
            {
                var bad = new SuggestionReview { Parsed = false };
                bad.Error("The assistant did not return a valid invoice structure. Try rephrasing the description.");
                return bad;
            }

            var r = new SuggestionReview { Parsed = true };

            // Document type
            if (string.IsNullOrWhiteSpace(suggestion.DocumentType) || !DocTypes.Contains(suggestion.DocumentType.Trim()))
                r.Error($"Document type '{suggestion.DocumentType}' is not one of the 8 LHDN types (01–04, 11–14).");
            else
                r.Ok($"Document type {suggestion.DocumentType} ({suggestion.DocumentTypeName}).");

            // Buyer
            if (string.IsNullOrWhiteSpace(suggestion.BuyerName))
                r.Warn("Buyer name is missing — pick the customer in the Create Invoice form.");
            if (string.IsNullOrWhiteSpace(suggestion.BuyerTin))
                r.Warn("Buyer TIN is missing — it must be filled and validated before submitting to LHDN.");

            // Lines
            if (suggestion.LineItems is null || suggestion.LineItems.Count == 0)
            {
                r.Error("No line items were suggested.");
            }
            else
            {
                int n = 0;
                foreach (var line in suggestion.LineItems)
                {
                    n++;
                    if (string.IsNullOrWhiteSpace(line.Description))
                        r.Warn($"Line {n}: description is empty.");
                    if (line.Quantity is null or <= 0)
                        r.Error($"Line {n}: quantity must be greater than zero.");
                    if (line.UnitPrice is null or < 0)
                        r.Error($"Line {n}: unit price is missing or negative.");

                    if (string.IsNullOrWhiteSpace(line.ClassificationCode))
                        r.Warn($"Line {n}: classification code is missing.");
                    else if (classificationCodes is { Count: > 0 } && !classificationCodes.Contains(line.ClassificationCode.Trim()))
                        r.Error($"Line {n}: classification code '{line.ClassificationCode}' is not a valid LHDN code.");
                }
            }

            // Tax
            if (!string.IsNullOrWhiteSpace(suggestion.TaxType)
                && taxCodes is { Count: > 0 }
                && !taxCodes.Contains(suggestion.TaxType.Trim()))
            {
                r.Warn($"Tax type '{suggestion.TaxType}' is not a recognised LHDN tax code — confirm it in the form.");
            }

            // Surface the model's own assumptions
            if (!string.IsNullOrWhiteSpace(suggestion.Notes))
                r.Warn("Assistant note: " + suggestion.Notes!.Trim());

            if (!r.HasErrors)
                r.Ok("No blocking issues found — review the details, then create the draft.");

            return r;
        }
    }
}
