using Ganss.Xss;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Cleans Resource article HTML before it is saved. The body is rendered raw on the public
    /// /resources/article page, so this is both the stored-XSS guard and the place where pasted
    /// Word/web formatting (colours, fonts, sizes, Mso classes) is dropped so the site stylesheet decides
    /// how the article looks on every screen size.
    /// </summary>
    public static class ResourceHtmlSanitizer
    {
        // HtmlSanitizer is thread-safe once configured.
        private static readonly HtmlSanitizer Sanitizer = Create();

        private static HtmlSanitizer Create()
        {
            var s = new HtmlSanitizer { KeepChildNodes = true };

            // Only alignment survives from inline styles; everything else comes from the article CSS.
            s.AllowedCssProperties.Clear();
            s.AllowedCssProperties.Add("text-align");

            foreach (var tag in new[] { "font", "center", "big", "small", "style", "form", "input", "button", "select", "textarea", "label" })
                s.AllowedTags.Remove(tag);

            foreach (var attr in new[] { "class", "bgcolor", "color", "face", "size", "background", "border", "cellpadding", "cellspacing", "align", "valign", "nowrap", "lang", "dir" })
                s.AllowedAttributes.Remove(attr);
            s.AllowedAttributes.Add("rel");

            s.AllowedSchemes.Add("mailto");
            s.AllowedSchemes.Add("tel");
            return s;
        }

        /// <summary>Returns safe article HTML; null/blank input returns an empty string.</summary>
        public static string Sanitize(string? html) =>
            string.IsNullOrWhiteSpace(html) ? string.Empty : Sanitizer.Sanitize(html);
    }
}
