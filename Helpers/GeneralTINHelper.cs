namespace EINVWORLD.Helpers
{
    public static class GeneralTINHelper
    {
        // List of general LHDN-assigned TINs (based on SDK + JomEInvoice)
        private static readonly HashSet<string> GeneralTINs = new()
        {
            "EI00000000010", // General Public’s TIN/ Self-Billed Buyer
            "EI00000000020", // Foreign Buyer’s / Foreign Shipping Recipient’s TIN
            "EI00000000030", // Foreign Supplier’s TIN
            "EI00000000040"  // Buyer’s TIN (Government or Government Authorities)
        };

        /// <summary>
        /// Returns true if the TIN is an LHDN-assigned general-purpose TIN.
        /// These are not allowed to request tokens or be used in onBehalfOf.
        /// </summary>
        public static bool IsGeneralTIN(string? tin)
        {
            if (string.IsNullOrWhiteSpace(tin)) return false;
            return GeneralTINs.Contains(tin.Trim());
        }

        /// <summary>
        /// Returns the list of known general TINs.
        /// </summary>
        public static IEnumerable<string> GetAll() => GeneralTINs;
    }
}
