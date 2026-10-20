using System.Globalization;

namespace eInvWorld.Helpers
{
    public static class InvoiceFormatting
    {
        // Display-only 2dp formatting for number inputs (Quantity, UnitPrice, TaxPercentage,
        // ExchangeRate) when a form is prefilled from a template/clone. Storage keeps its full
        // decimal(18,6)/decimal(18,4) precision — this only overrides the rendered <input value="…">,
        // matching the step="0.01" these fields already use, so a value like 1500.0000 doesn't leak
        // decimal-column padding onto the screen.
        public static string Fmt2(decimal? v) => (v ?? 0m).ToString("F2", CultureInfo.InvariantCulture);

        // Exchange rate is decimal(18,6) and LHDN takes it as entered — keep every stored digit (min 2dp)
        // so a saved 4.567 reopens as 4.567, not 4.57. Null renders empty (JS then defaults it).
        public static string FmtRate(decimal? v) => v?.ToString("0.00####", CultureInfo.InvariantCulture) ?? "";
    }
}
