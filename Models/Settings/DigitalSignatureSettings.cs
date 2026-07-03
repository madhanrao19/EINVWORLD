namespace eInvWorld.Models.Settings
{
    /// <summary>
    /// Controls MyInvois v1.1 document digital signing. Bound from configuration section
    /// "LHDNApiConfig" so it can be toggled entirely via appsettings without a code change.
    ///
    /// Default is OFF: while disabled the system behaves exactly as before (unsigned v1.0
    /// submissions). To enable v1.1 once a signing certificate has been purchased:
    ///   1. Set LHDNApiConfig:SigningEnabled = true
    ///   2. Set LHDNApiConfig:DocVersion   = "1.1"
    ///   3. Provide LHDNApiConfig:CertPath  (PFX/.p12, resolved against the content root)
    ///      and LHDNApiConfig:CertPass (store the password in user-secrets / env vars, NOT appsettings)
    ///   4. Validate against the MyInvois PREPROD sandbox until documents are accepted, then go live.
    /// </summary>
    public class DigitalSignatureSettings
    {
        /// <summary>Master switch. When false, documents are submitted unsigned (current behaviour).</summary>
        public bool SigningEnabled { get; set; } = false;

        /// <summary>Document version sent to LHDN ("1.0" unsigned / "1.1" signed).</summary>
        public string DocVersion { get; set; } = "1.0";

        /// <summary>Path to the signing PFX/.p12 (relative paths resolve against the content root).</summary>
        public string? CertPath { get; set; }

        /// <summary>Password for the signing certificate. Keep in user-secrets / env vars.</summary>
        public string? CertPass { get; set; }

        /// <summary>
        /// Which <c>ICertificateProvider</c> supplies the signing certificate. "File" (default) loads the
        /// .p12 from <see cref="CertPath"/>. Reserved for future custody upgrades (e.g. "AzureKeyVault" —
        /// see SECRETS-SETUP.md for the drop-in contract); selection is by provider name, so adding one is
        /// a DI registration plus this config value, with no change to the signing service or its callers.
        /// </summary>
        public string SigningKeyProvider { get; set; } = "File";
    }
}
