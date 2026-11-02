using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using CsvHelper;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Shared CSV import reading for the Items, Buyers, Companies and Invoice CSV imports and the Bulk
    /// Invoice import. Keeps the CsvHelper-specific code (and its version quirks) in one place so the
    /// import pages only see plain records / rows. Behaviour is pinned by CsvImportHelperTests.
    /// </summary>
    public static class CsvImportHelper
    {
        private static readonly CsvReaderOptions Options = new()
        {
            Separator = ',',
            HasHeader = true,
            TrimWhitespace = true,
            SkipEmptyLines = true,
            MissingField = CsvMissingFieldHandling.ReturnEmpty,
            FormatProvider = CultureInfo.InvariantCulture,
        };

        // Bulk import has always failed the whole file on a row with fewer cells than the header.
        private static readonly CsvReaderOptions StrictRowOptions = Options with { MissingField = CsvMissingFieldHandling.Throw };

        /// <summary>
        /// Reads every record of <typeparamref name="T"/> by matching header text to property names exactly
        /// (case-sensitive). Lenient: values are trimmed, blank lines skipped, extra columns ignored, and a
        /// missing column or cell leaves the property empty. An empty cell gives "" for strings and null for
        /// nullable numbers; a non-numeric value in a number column, or a header that matches no property,
        /// throws and fails the whole file.
        /// </summary>
        /// <remarks>
        /// Hand-mapped rather than CsvHelper 34's source-generated GetRecords, which makes every property a
        /// required column. Supports string and decimal / decimal? properties (all the import DTOs use).
        /// </remarks>
        public static List<T> ReadRecords<T>(Stream stream) where T : new()
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.Ordinal);

            using var csv = CsvReader.Create(new StreamReader(stream), Options, leaveOpen: false);
            var result = new List<T>();
            List<(int Index, PropertyInfo Prop)>? columns = null;

            while (csv.Read())
            {
                columns ??= csv.Headers
                    .Select((h, i) => (Index: i, Prop: props.GetValueOrDefault(h.Trim())))
                    .Where(c => c.Prop != null)
                    .Select(c => (c.Index, c.Prop!))
                    .DistinctBy(c => c.Item2) // a repeated header: the first column wins, as before
                    .ToList();
                if (columns.Count == 0)
                    throw new InvalidDataException($"No column in this file matches a {typeof(T).Name} field. Check the header row.");

                var record = new T();
                foreach (var (index, prop) in columns)
                    prop.SetValue(record, ConvertCell(csv.GetString(index), prop, csv.RowNumber + 1));
                result.Add(record);
            }
            return result;
        }

        // row = spreadsheet row number (header is row 1), as the user sees it in Excel.
        private static object? ConvertCell(string value, PropertyInfo prop, long row)
        {
            var type = prop.PropertyType;
            if (type == typeof(string)) return value;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying == typeof(decimal) && value.Length == 0) return null;
            if ((underlying ?? type) == typeof(decimal))
            {
                if (decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d))
                    return d;
                throw new FormatException($"Row {row}: '{value}' in column {prop.Name} is not a number.");
            }
            throw new NotSupportedException($"CSV import does not support {type.Name} ({prop.DeclaringType?.Name}.{prop.Name}).");
        }

        /// <summary>
        /// Reads a header row plus data rows as header → trimmed value dictionaries (header lookup is
        /// case-insensitive). Columns with a blank header are skipped, and so are rows whose values are
        /// all empty. An empty stream gives an empty list.
        /// </summary>
        public static List<Dictionary<string, string>> ReadRows(Stream stream)
        {
            using var csv = CsvReader.Create(new StreamReader(stream), StrictRowOptions, leaveOpen: false);
            var result = new List<Dictionary<string, string>>();

            while (csv.Read())
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < csv.Headers.Count; i++)
                {
                    var h = csv.Headers[i];
                    if (string.IsNullOrWhiteSpace(h)) continue;
                    dict[h.Trim()] = csv.GetString(i).Trim();
                }
                if (dict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                    result.Add(dict);
            }
            return result;
        }
    }
}
