using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

namespace EINVWORLD.Helpers
{
    public static class SlugHelper
    {
        public static string GenerateSlug(string title, int maxLength = 200)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            // Convert to lower case
            string slug = title.ToLowerInvariant();

            // Normalize accents (e.g., é → e)
            slug = RemoveDiacritics(slug);

            // Remove invalid characters
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

            // Convert multiple spaces/hyphens to one hyphen
            slug = Regex.Replace(slug, @"[\s-]+", "-").Trim('-');

            return slug;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
