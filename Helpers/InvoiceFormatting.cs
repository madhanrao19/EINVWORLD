using System.Globalization;

namespace eInvWorld.Helpers
{
    public static class InvoiceFormatting
    {
        // Display-only 2dp formatting for TaxPercentage inputs (stored decimal(18,2)).
        public static string Fmt2(decimal? v) => (v ?? 0m).ToString("F2", CultureInfo.InvariantCulture);

        // Exchange rate is decimal(18,6) and LHDN takes it as entered — keep every stored digit (min 2dp)
        // so a saved 4.567 reopens as 4.567, not 4.57. Null renders empty (JS then defaults it).
        public static string FmtRate(decimal? v) => v?.ToString("0.00####", CultureInfo.InvariantCulture) ?? "";

        // Line Quantity (decimal(18,6)) and UnitPrice (decimal(18,4)): min 2dp, every stored digit kept, so a
        // 4dp unit price from import / Smart Capture isn't rounded to 2dp when the invoice is opened and saved.
        public static string FmtNum(decimal? v) => (v ?? 0m).ToString("0.00####", CultureInfo.InvariantCulture);
    }
}
