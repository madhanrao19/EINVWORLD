using System.Collections.Generic;

namespace eInvWorld.Models
{
    public class EInvoiceVersion
    {
        public string Version { get; set; } // Version number (e.g., "1.0", "1.1")
        public string Description { get; set; } // Description of the version

        // Constructor
        public EInvoiceVersion(string version, string description)
        {
            Version = version;
            Description = description;
        }

        // Static method to get predefined list of e-invoice versions
        public static List<EInvoiceVersion> GetEInvoiceVersions()
        {
            return new List<EInvoiceVersion>
            {
                new EInvoiceVersion("1.0", "Version 1.0"),
                new EInvoiceVersion("1.1", "Version 1.1")
            };
        }
    }
}
