using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using eInvWorld.Models.InputModel;

namespace EINVWORLD.Helpers
{
    /// <summary>The fields Finance asks "who changed this?" about, captured from a saved invoice.</summary>
    public sealed record InvoiceSnapshot(string? Buyer, string? DocType, string? Currency, DateTime? IssueDate, decimal? Total, IReadOnlyList<LineSnapshot> Lines)
    {
        public static InvoiceSnapshot Of(InvoiceHeader h, string? buyerName) => new(
            buyerName, h.DocTypeCode, h.Currency, h.IssueDate, h.TotalPayableAmount,
            h.InvoiceLines.OrderBy(l => l.LineNumber).Select(LineSnapshot.Of).ToList());
    }

    /// <summary>One item line; the first tax is the one the invoice forms edit.</summary>
    public sealed record LineSnapshot(string? Description, string? Classification, string? Unit, decimal? Quantity, decimal? UnitPrice,
        decimal? Discount, decimal? Fee, string? TaxType, decimal? TaxRate)
    {
        public static LineSnapshot Of(InvoiceLine l)
        {
            var tax = l.InvoiceTaxes.FirstOrDefault();
            return new(l.ItemDescription, l.ClassificationCode, l.UnitOfMeasure, l.Quantity, l.UnitPrice,
                l.DiscountAmount, l.FeeChargeAmount, tax?.TaxCategory, tax?.TaxPercentage);
        }
    }

    /// <summary>
    /// "Buyer: ABC → XYZ; Item 2 Tax Type: 01 → 06" for the invoice activity log. Pure; capped to the Remarks column.
    /// Null when nothing Finance-relevant changed.
    /// </summary>
    public static class InvoiceChangeSummary
    {
        /// <summary>Display name of a stored invoice's buyer, for the "before" snapshot.</summary>
        public static string? BuyerName(eInvWorld.Data.ApplicationDbContext db, int? customerId, int? publicCustomerId) =>
            customerId > 0 ? db.PartyInfos.Where(p => p.PartyInfoId == customerId).Select(p => p.CompanyName).FirstOrDefault()
            : publicCustomerId > 0 ? db.PublicCustomers.Where(p => p.PublicCustomerId == publicCustomerId).Select(p => p.CompanyName).FirstOrDefault()
            : null;

        /// <summary>InvoiceHistory.Remarks length.</summary>
        public const int MaxLength = 1000;
        public const string Separator = "; ";

        public static string? Describe(InvoiceSnapshot before, InvoiceSnapshot after)
        {
            var changes = new List<string>();
            void Text(string label, string? a, string? b)
            {
                a = string.IsNullOrWhiteSpace(a) ? null : a.Trim();
                b = string.IsNullOrWhiteSpace(b) ? null : b.Trim();
                if (!string.Equals(a, b, StringComparison.Ordinal)) changes.Add($"{label}: {a ?? "(blank)"} → {b ?? "(blank)"}");
            }
            void Number(string label, decimal? a, decimal? b, string format = "0.##")
            {
                if ((a ?? 0) != (b ?? 0)) changes.Add($"{label}: {(a ?? 0).ToString(format, CultureInfo.InvariantCulture)} → {(b ?? 0).ToString(format, CultureInfo.InvariantCulture)}");
            }

            Text("Buyer", before.Buyer, after.Buyer);
            Text("Document type", before.DocType, after.DocType);
            Text("Currency", before.Currency, after.Currency);
            Text("Issue date", before.IssueDate?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture), after.IssueDate?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture));

            for (int i = 0; i < Math.Max(before.Lines.Count, after.Lines.Count); i++)
            {
                var item = $"Item {i + 1}";
                if (i >= before.Lines.Count) { changes.Add($"{item} added: {after.Lines[i].Description}"); continue; }
                if (i >= after.Lines.Count) { changes.Add($"{item} removed: {before.Lines[i].Description}"); continue; }
                LineSnapshot a = before.Lines[i], b = after.Lines[i];
                Text($"{item} Description", a.Description, b.Description);
                Text($"{item} Classification", a.Classification, b.Classification);
                Text($"{item} Unit", a.Unit, b.Unit);
                Number($"{item} Quantity", a.Quantity, b.Quantity, "0.####");
                Number($"{item} Unit Price", a.UnitPrice, b.UnitPrice, "N2");
                Number($"{item} Discount", a.Discount, b.Discount, "N2");
                Number($"{item} Fee/Charge", a.Fee, b.Fee, "N2");
                Text($"{item} Tax Type", a.TaxType, b.TaxType);
                Number($"{item} Tax Rate", a.TaxRate, b.TaxRate);
            }
            Number("Total", before.Total, after.Total, "N2");

            if (changes.Count == 0) return null;
            var text = string.Join(Separator, changes);
            return text.Length <= MaxLength ? text : text[..(MaxLength - 1)] + "…";
        }
    }
}
