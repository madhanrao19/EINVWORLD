using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Shared CSV export logic used by any list page's "Export CSV" action. Extracted from
    /// InvoiceLists.cshtml.cs so the CSV formula-injection guard (see EscapeCsv) exists in exactly one
    /// place instead of being copy-pasted per page.
    /// </summary>
    public static class CsvExportHelper
    {
        /// <summary>
        /// Escapes a single CSV cell: quotes it if it contains a comma/quote/newline, and neutralises
        /// CSV formula injection by prefixing a leading apostrophe when the value starts with a
        /// character (= + - @, or a tab/CR) that Excel/Sheets would otherwise treat as a formula.
        /// </summary>
        public static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            if ("=+-@\t\r".IndexOf(value[0]) >= 0)
            {
                value = "'" + value;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        /// <summary>
        /// Builds a complete CSV file (header row + data rows), escaping every cell, and returns it as
        /// UTF-8 bytes ready for <c>File(bytes, "text/csv", fileName)</c>.
        /// </summary>
        public static byte[] BuildCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }
    }
}
