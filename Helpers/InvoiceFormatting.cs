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
    }
}
