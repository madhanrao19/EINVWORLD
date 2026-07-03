using System.Security.Cryptography.X509Certificates;

namespace eInvWorld.Services.Signing
{
    /// <summary>
    /// A swappable source of the LHDN XAdES signing certificate — the custody seam that keeps
    /// <see cref="DocumentSigningService"/> independent of WHERE the private key lives. Selected at
    /// runtime by matching <see cref="Name"/> against <c>LHDNApiConfig:SigningKeyProvider</c>
    /// (case-insensitive), mirroring the <c>IAiProvider</c> pattern used for the AI backends.
    ///
    /// ── Adding a vault/HSM provider later (no signing-service change required) ────────────────
    /// e.g. an AzureKeyVaultCertificateProvider:
    ///   1. Implement this interface (Name = "AzureKeyVault"); fetch the certificate with the
    ///      Azure.Security.KeyVault.Certificates NuGet package (sync variants exist) using
    ///      DefaultAzureCredential (managed identity / env credentials — never a secret in config).
    ///   2. Add config keys, e.g. LHDNApiConfig:KeyVaultUri + LHDNApiConfig:KeyVaultCertName.
    ///   3. Register it as an ICertificateProvider in Program.cs and set
    ///      LHDNApiConfig:SigningKeyProvider = "AzureKeyVault".
    /// See SECRETS-SETUP.md ("Signing-key custody") for the operator-facing version of this.
    /// </summary>
    public interface ICertificateProvider
    {
        /// <summary>Provider identifier matched against <c>LHDNApiConfig:SigningKeyProvider</c> (e.g. "File").</summary>
        string Name { get; }

        /// <summary>
        /// Returns the signing certificate WITH its private key. Deliberately synchronous: the signing
        /// pipeline (<see cref="IDocumentSigningService.PrepareDocumentForSubmission"/>) is sync
        /// end-to-end, and providers are expected to cache so this is cheap after first load. Throw
        /// (never return null) when the certificate cannot be produced — an unsigned document must never
        /// be sent when signing is supposed to be on.
        /// </summary>
        X509Certificate2 GetSigningCertificate();
    }
}
