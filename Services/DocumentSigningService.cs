using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using eInvWorld.Models.Settings;
using eInvWorld.Utility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eInvWorld.Services
{
    public interface IDocumentSigningService
    {
        /// <summary>True when v1.1 signing is enabled in configuration.</summary>
        bool Enabled { get; }

        /// <summary>The DocVersion to send to LHDN ("1.0" or "1.1").</summary>
        string DocVersion { get; }

        /// <summary>
        /// Returns the document JSON ready for submission. When signing is disabled this is the
        /// input unchanged. When enabled, a MyInvois XAdES enveloped signature is injected into the
        /// document (UBLExtensions + cac:Signature) and the signed JSON is returned.
        /// </summary>
        string PrepareDocumentForSubmission(string ublJson);
    }

    /// <summary>
    /// MyInvois (LHDN) v1.1 document digital signing.
    ///
    /// ── HOW TO ENABLE (no code change required) ───────────────────────────────────────────────
    ///   appsettings:  LHDNApiConfig:SigningEnabled = true
    ///                 LHDNApiConfig:DocVersion     = "1.1"
    ///                 LHDNApiConfig:CertPath       = "Cert/your-signing-cert.p12"
    ///   user-secrets: LHDNApiConfig:CertPass       = "********"
    ///   Then validate against the MyInvois PREPROD sandbox until documents are accepted.
    ///
    /// ⚠️ REFERENCE IMPLEMENTATION — VALIDATE BEFORE PRODUCTION USE.
    /// The structure below follows the documented XAdES algorithm
    /// (https://sdk.myinvois.hasil.gov.my/signature/), but the exact JSON canonicalisation used for
    /// the digests MUST be confirmed against the official SDK sample and a successful preprod
    /// submission before going live. The service is OFF by default, so this code never runs until
    /// you flip the flag — current unsigned v1.0 behaviour is completely unaffected.
    /// </summary>
    public class DocumentSigningService : IDocumentSigningService
    {
        private readonly DigitalSignatureSettings _settings;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DocumentSigningService> _logger;
        private X509Certificate2? _certificate;

        public DocumentSigningService(
            IOptions<DigitalSignatureSettings> settings,
            IWebHostEnvironment env,
            ILogger<DocumentSigningService> logger)
        {
            _settings = settings.Value;
            _env = env;
            _logger = logger;
        }

        public bool Enabled => _settings.SigningEnabled;
        public string DocVersion => string.IsNullOrWhiteSpace(_settings.DocVersion) ? "1.0" : _settings.DocVersion;

        public string PrepareDocumentForSubmission(string ublJson)
        {
            if (!_settings.SigningEnabled)
                return ublJson; // v1.0 path — unchanged behaviour.

            try
            {
                var cert = GetCertificate();

                // 1. Document digest: SHA-256 over the minified document (no UBLExtensions/Signature yet).
                var docMinified = Minify(ublJson);
                var docDigestBytes = SHA256.HashData(Encoding.UTF8.GetBytes(docMinified));
                var docDigest = Convert.ToBase64String(docDigestBytes);

                // 2. RSA-SHA256 signature over the document digest.
                var signatureBytes = HashUtility.SignData(docDigestBytes, cert)
                    ?? throw new InvalidOperationException("Signing failed: certificate has no usable RSA private key.");
                var sig = Convert.ToBase64String(signatureBytes);

                // 3. Certificate material.
                var certDigest = HashUtility.GetCertHash(cert);            // base64( SHA256(cert.RawData) )
                var x509Cert = HashUtility.GetX509Certificate(cert);       // base64( cert.RawData )
                var issuerName = cert.IssuerName.Name ?? string.Empty;
                var serialNumber = ParseSerial(cert);
                var signingTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

                // 4. SignedProperties → PropsDigest (SHA-256 over the minified SignedProperties).
                var signedProperties = BuildSignedProperties(signingTime, certDigest, issuerName, serialNumber);
                var propsMinified = signedProperties.ToJsonString();
                var propsDigest = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(propsMinified)));

                // 5. Assemble the signature and inject UBLExtensions + cac:Signature into the document.
                return InjectSignature(ublJson, docDigest, propsDigest, sig, x509Cert, issuerName, serialNumber, signedProperties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ v1.1 document signing failed. Aborting submission so an unsigned document is never sent.");
                throw; // Never silently fall back to unsigned when signing is supposed to be on.
            }
        }

        private X509Certificate2 GetCertificate()
        {
            if (_certificate != null) return _certificate;

            if (string.IsNullOrWhiteSpace(_settings.CertPath))
                throw new InvalidOperationException("LHDNApiConfig:CertPath is not configured but SigningEnabled is true.");

            var path = Path.IsPathRooted(_settings.CertPath)
                ? _settings.CertPath
                : Path.Combine(_env.ContentRootPath, _settings.CertPath);

            if (!File.Exists(path))
                throw new FileNotFoundException($"Signing certificate not found at '{path}'.");

            // X509CertificateLoader is the non-obsolete loader on .NET 9+.
            _certificate = X509CertificateLoader.LoadPkcs12FromFile(path, _settings.CertPass);
            _logger.LogInformation("🔐 Loaded signing certificate '{Subject}' (valid until {Expiry:yyyy-MM-dd}).",
                _certificate.Subject, _certificate.NotAfter);
            return _certificate;
        }

        private static string ParseSerial(X509Certificate2 cert)
        {
            // Decimal serial number as required by IssuerSerial.
            try { return System.Numerics.BigInteger.Parse("0" + cert.SerialNumber, System.Globalization.NumberStyles.HexNumber).ToString(); }
            catch { return cert.SerialNumber; }
        }

        private static string Minify(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement); // compact, property order preserved
        }

        // xades:SignedProperties as a MyInvois UBL-JSON object (also hashed to produce PropsDigest).
        private static JsonObject BuildSignedProperties(string signingTime, string certDigest, string issuerName, string serialNumber)
        {
            return new JsonObject
            {
                ["Target"] = "signature",
                ["SignedProperties"] = new JsonArray(new JsonObject
                {
                    ["Id"] = "id-xades-signed-props",
                    ["SignedSignatureProperties"] = new JsonArray(new JsonObject
                    {
                        ["SigningTime"] = new JsonArray(new JsonObject { ["_"] = signingTime }),
                        ["SigningCertificate"] = new JsonArray(new JsonObject
                        {
                            ["Cert"] = new JsonArray(new JsonObject
                            {
                                ["CertDigest"] = new JsonArray(new JsonObject
                                {
                                    ["DigestMethod"] = new JsonArray(new JsonObject { ["_"] = "", ["Algorithm"] = "http://www.w3.org/2001/04/xmlenc#sha256" }),
                                    ["DigestValue"] = new JsonArray(new JsonObject { ["_"] = certDigest })
                                }),
                                ["IssuerSerial"] = new JsonArray(new JsonObject
                                {
                                    ["X509IssuerName"] = new JsonArray(new JsonObject { ["_"] = issuerName }),
                                    ["X509SerialNumber"] = new JsonArray(new JsonObject { ["_"] = serialNumber })
                                })
                            })
                        })
                    })
                })
            };
        }

        private static string InjectSignature(
            string ublJson, string docDigest, string propsDigest, string sig, string x509Cert,
            string issuerName, string serialNumber, JsonObject signedProperties)
        {
            var root = JsonNode.Parse(ublJson)!.AsObject();

            // The document body lives under "Invoice"/"CreditNote"/... — take the first array property
            // that holds an object (the UBL document) and inject siblings into it.
            JsonObject body = FindDocumentBody(root);

            // cac:Signature reference
            body["Signature"] = new JsonArray(new JsonObject
            {
                ["ID"] = new JsonArray(new JsonObject { ["_"] = "urn:oasis:names:specification:ubl:signature:Invoice" }),
                ["SignatureMethod"] = new JsonArray(new JsonObject { ["_"] = "urn:oasis:names:specification:ubl:dsig:enveloped:xades" })
            });

            // ds:Signature
            var dsSignature = new JsonObject
            {
                ["ID"] = new JsonArray(new JsonObject { ["_"] = "urn:oasis:names:specification:ubl:signature:1" }),
                ["Object"] = new JsonArray(new JsonObject
                {
                    ["QualifyingProperties"] = new JsonArray(signedProperties.DeepClone())
                }),
                ["KeyInfo"] = new JsonArray(new JsonObject
                {
                    ["X509Data"] = new JsonArray(new JsonObject
                    {
                        ["X509Certificate"] = new JsonArray(new JsonObject { ["_"] = x509Cert }),
                        ["X509SubjectName"] = new JsonArray(new JsonObject { ["_"] = issuerName }),
                        ["X509IssuerSerial"] = new JsonArray(new JsonObject
                        {
                            ["X509IssuerName"] = new JsonArray(new JsonObject { ["_"] = issuerName }),
                            ["X509SerialNumber"] = new JsonArray(new JsonObject { ["_"] = serialNumber })
                        })
                    })
                }),
                ["SignatureValue"] = new JsonArray(new JsonObject { ["_"] = sig }),
                ["SignedInfo"] = new JsonArray(new JsonObject
                {
                    ["SignatureMethod"] = new JsonArray(new JsonObject { ["_"] = "", ["Algorithm"] = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256" }),
                    ["Reference"] = new JsonArray(
                        new JsonObject
                        {
                            ["Id"] = "id-doc-signed-data",
                            ["URI"] = "",
                            ["DigestMethod"] = new JsonArray(new JsonObject { ["_"] = "", ["Algorithm"] = "http://www.w3.org/2001/04/xmlenc#sha256" }),
                            ["DigestValue"] = new JsonArray(new JsonObject { ["_"] = docDigest })
                        },
                        new JsonObject
                        {
                            ["Id"] = "id-xades-signed-props",
                            ["Type"] = "http://uri.etsi.org/01903#SignedProperties",
                            ["URI"] = "#id-xades-signed-props",
                            ["DigestMethod"] = new JsonArray(new JsonObject { ["_"] = "", ["Algorithm"] = "http://www.w3.org/2001/04/xmlenc#sha256" }),
                            ["DigestValue"] = new JsonArray(new JsonObject { ["_"] = propsDigest })
                        })
                })
            };

            body["UBLExtensions"] = new JsonArray(new JsonObject
            {
                ["UBLExtension"] = new JsonArray(new JsonObject
                {
                    ["ExtensionURI"] = new JsonArray(new JsonObject { ["_"] = "urn:oasis:names:specification:ubl:dsig:enveloped:xades" }),
                    ["ExtensionContent"] = new JsonArray(new JsonObject
                    {
                        ["UBLDocumentSignatures"] = new JsonArray(new JsonObject
                        {
                            ["SignatureInformation"] = new JsonArray(new JsonObject
                            {
                                ["ID"] = new JsonArray(new JsonObject { ["_"] = "urn:oasis:names:specification:ubl:signature:1" }),
                                ["ReferencedSignatureID"] = new JsonArray(new JsonObject { ["_"] = "urn:oasis:names:specification:ubl:signature:Invoice" }),
                                ["Signature"] = new JsonArray(dsSignature)
                            })
                        })
                    })
                })
            });

            return root.ToJsonString();
        }

        // Finds the UBL document object inside the root (Invoice/CreditNote/DebitNote/etc.).
        private static JsonObject FindDocumentBody(JsonObject root)
        {
            foreach (var kvp in root)
            {
                if (kvp.Value is JsonArray arr && arr.Count > 0 && arr[0] is JsonObject obj)
                    return obj;
            }
            // Fall back to the root itself if the document isn't wrapped in a typed array.
            return root;
        }
    }
}
