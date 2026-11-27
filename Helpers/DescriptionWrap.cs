using System.Collections.Generic;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Item description line wrapping, identical to <c>enforceLineLength</c> in create-invoice.js / InvoiceEdit.cshtml,
    /// so the PDF, Print and the invoice preview break lines exactly where the Create/Edit textarea does, whatever path
    /// the text came in by (typed, saved item, import, Smart Capture, template, recurring).
    /// </summary>
    public static class DescriptionWrap
    {
        /// <summary>Maximum characters per description line (same value the invoice forms use).</summary>
        public const int LineLength = 56;

        /// <summary>Breaks every line longer than <paramref name="max"/> at its last space, or hard at max when it has none.</summary>
        public static string Wrap(string? text, int max = LineLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var result = new List<string>();
            foreach (var original in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = original;
                while (line.Length > max)
                {
                    var space = line.LastIndexOf(' ', max - 1);
                    if (space == -1)
                    {
                        result.Add(line[..max]);
                        line = line[max..];
                    }
                    else
                    {
                        result.Add(line[..space]);
                        line = line[(space + 1)..];
                    }
                }
                result.Add(line);
            }
            return string.Join("\n", result);
        }
    }
}
