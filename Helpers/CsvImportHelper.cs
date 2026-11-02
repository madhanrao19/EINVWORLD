using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Shared CSV import reading for the Items, Buyers, Companies and Invoice CSV imports and the Bulk
    /// Invoice import. Keeps the CsvHelper-specific code (and its version quirks) in one place so the
    /// import pages only see plain records / rows.
    /// </summary>
    public static class CsvImportHelper
    {
        /// <summary>
        /// Reads every record of <typeparamref name="T"/> by matching header text to property names.
        /// Lenient: values are trimmed, blank lines skipped, extra columns ignored, and a missing column
        /// leaves the property at its default. An empty cell gives "" for strings and null for nullable
        /// numbers; a non-numeric value in a number column throws, failing the whole file.
        /// </summary>
        public static List<T> ReadRecords<T>(Stream stream)
        {
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                HeaderValidated = null,
                MissingFieldFound = null
            });
            return csv.GetRecords<T>().ToList();
        }

        /// <summary>
        /// Reads a header row plus data rows as header → trimmed value dictionaries (header lookup is
        /// case-insensitive). Columns with a blank header are skipped, and so are rows whose values are
        /// all empty. An empty stream gives an empty list.
        /// </summary>
        public static List<Dictionary<string, string>> ReadRows(Stream stream)
        {
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var result = new List<Dictionary<string, string>>();

            if (!csv.Read()) return result;
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? System.Array.Empty<string>();

            while (csv.Read())
            {
                var dict = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
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
