using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.ViewModels;

namespace eInvWorld.Helpers
{
    /// <summary>
    /// MyInvois tax-type rules checked on the server, whatever page or job produced the invoice.
    /// Tax Type 06 ("Not Applicable") must carry a 0% rate and 0.00 tax; LHDN rejects anything else
    /// (CF394). Tax Exemption (E) and genuine 0% rates on other types are deliberately not touched.
    /// </summary>
    public static class TaxTypeRules
    {
        /// <summary>LHDN tax type code for "Not Applicable".</summary>
        public const string NotApplicable = "06";

        /// <summary>True when a Tax Type 06 entry has a non-zero rate or tax amount.</summary>
        public static bool IsInvalidNotApplicable(string? taxCategory, decimal? percentage, decimal? amount) =>
            string.Equals(taxCategory?.Trim(), NotApplicable, StringComparison.Ordinal)
            && ((percentage ?? 0m) != 0m || (amount ?? 0m) != 0m);

        /// <summary>
        /// Forces every Tax Type 06 entry on posted lines to 0% / 0.00, so a rate typed past the read-only
        /// field (or sent by a hand-made POST) is never saved to a draft or template. Returns how many changed.
        /// </summary>
        public static int ZeroNotApplicable(IEnumerable<InvoiceLineView>? lines)
        {
            var changed = 0;
            foreach (var tax in lines?.SelectMany(l => l.Taxes ?? Enumerable.Empty<InvoiceTaxView>()) ?? Enumerable.Empty<InvoiceTaxView>())
            {
                if (!IsInvalidNotApplicable(tax.TaxCategory, tax.TaxPercentage, tax.TaxAmount)) continue;
                tax.TaxPercentage = 0m;
                tax.TaxAmount = 0m;
                changed++;
            }
            return changed;
        }

        /// <summary>Finance-facing message naming the line.</summary>
        public static string Message(string? lineNo, string? description) =>
            $"Item {lineNo}{(string.IsNullOrWhiteSpace(description) ? "" : " – " + description.Trim())}: Tax Rate must be 0% when Tax Type is Not Applicable.";

        /// <summary>One message per invoice line that breaks the rule (checked before the payload is built).</summary>
        public static List<string> LineErrors(IEnumerable<InvoiceLine> lines) =>
            lines.Where(l => l.InvoiceTaxes.Any(t => IsInvalidNotApplicable(t.TaxCategory, t.TaxPercentage, t.TaxAmount)))
                 .Select(l => Message(l.LineNumber.ToString(), l.ItemDescription))
                 .ToList();

        /// <summary>
        /// Same check on a built UBL JSON payload — the final net before anything goes to LHDN, covering
        /// payload files saved before this rule existed.
        /// </summary>
        public static List<string> PayloadErrors(string invoiceJson)
        {
            var errors = new List<string>();
            using var doc = JsonDocument.Parse(invoiceJson);
            if (!doc.RootElement.TryGetProperty("Invoice", out var invoices)) return errors;
            foreach (var invoice in invoices.EnumerateArray())
            {
                if (!invoice.TryGetProperty("InvoiceLine", out var lines)) continue;
                foreach (var line in lines.EnumerateArray())
                {
                    if (!line.TryGetProperty("TaxTotal", out var taxTotals)) continue;
                    var bad = taxTotals.EnumerateArray()
                        .SelectMany(tt => tt.TryGetProperty("TaxSubtotal", out var subs) ? subs.EnumerateArray() : Enumerable.Empty<JsonElement>())
                        .Any(s => IsInvalidNotApplicable(Value(s, "TaxCategory", "ID"), Number(s, "Percent"), Number(s, "TaxAmount")));
                    if (bad)
                        errors.Add(Message(Value(line, "ID")?.TrimStart('0'), Value(line, "Item", "Description"))); // line IDs are "001"
                }
            }
            return errors;
        }

        // UBL JSON wraps every value as [{ "_": value }]; walk the path and return that value as text.
        private static string? Value(JsonElement e, params string[] path)
        {
            foreach (var p in path)
            {
                if (!e.TryGetProperty(p, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;
                e = arr[0];
            }
            if (!e.TryGetProperty("_", out var v)) return null;
            return v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null };
        }

        private static decimal? Number(JsonElement e, string name) =>
            decimal.TryParse(Value(e, name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
