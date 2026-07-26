namespace EINVWORLD.Helpers
{
    /// <summary>Display-only masking for sensitive free-text fields (bank numbers, etc.). Never used to decide access.</summary>
    public static class MaskingHelper
    {
        public static string MaskLast4(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var trimmed = value.Trim();
            return trimmed.Length <= 4
                ? new string('•', trimmed.Length)
                : $"•••• {trimmed[^4..]}";
        }
    }
}
