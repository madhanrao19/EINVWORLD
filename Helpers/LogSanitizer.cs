namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Masks personally identifiable identifiers (TIN / BRN / NRIC) before they reach structured logs,
    /// per the platform logging policy (never log PII). LHDN-assigned general TINs (EI000000000xx) are
    /// public constants, not PII, and stay readable for supportability.
    /// </summary>
    public static class LogSanitizer
    {
        /// <summary>
        /// Masks a TIN for logging: keeps the first 4 and last 2 characters (e.g. "C123*****89").
        /// General LHDN TINs are returned unmasked; null/blank returns "(none)".
        /// </summary>
        public static string MaskTin(string? tin)
        {
            if (string.IsNullOrWhiteSpace(tin)) return "(none)";
            var t = tin.Trim();
            if (GeneralTINHelper.IsGeneralTIN(t)) return t;
            return MaskId(t);
        }

        /// <summary>
        /// Masks any sensitive identifier (BRN, NRIC, passport no, …) for logging:
        /// keeps the first 4 and last 2 characters; short values are fully masked.
        /// </summary>
        public static string MaskId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "(none)";
            var v = id.Trim();
            if (v.Length <= 6) return new string('*', v.Length);
            return $"{v[..4]}{new string('*', v.Length - 6)}{v[^2..]}";
        }
    }
}
