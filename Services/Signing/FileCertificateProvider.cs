using System.Security.Cryptography.X509Certificates;
using eInvWorld.Models.Settings;
using Microsoft.Extensions.Options;

namespace eInvWorld.Services.Signing
{
    /// <summary>
    /// Default certificate provider: loads the signing .p12/PFX from the file path configured at
    /// <c>LHDNApiConfig:CertPath</c> (password from <c>CertPass</c>, supplied via user-secrets/env vars).
    /// This is the exact loading logic that previously lived inside
    /// <c>DocumentSigningService.GetCertificate()</c>, extracted verbatim behind the custody seam.
    /// Registered as a singleton, so the certificate is loaded once per process — consistent with the
    /// cert-rotation runbook, which recycles the app pool (iisreset) after swapping the file.
    /// </summary>
    public sealed class FileCertificateProvider : ICertificateProvider
    {
        private readonly DigitalSignatureSettings _settings;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileCertificateProvider> _logger;
        private readonly object _lock = new();
        private X509Certificate2? _certificate;

        public FileCertificateProvider(
            IOptions<DigitalSignatureSettings> settings,
            IWebHostEnvironment env,
            ILogger<FileCertificateProvider> logger)
        {
            _settings = settings.Value;
            _env = env;
            _logger = logger;
        }

        public string Name => "File";

        public X509Certificate2 GetSigningCertificate()
        {
            if (_certificate != null) return _certificate;
            lock (_lock)
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
                var cert = X509CertificateLoader.LoadPkcs12FromFile(path, _settings.CertPass);
                _logger.LogInformation("🔐 Loaded signing certificate '{Subject}' (valid until {Expiry:yyyy-MM-dd}).",
                    cert.Subject, cert.NotAfter);
                _certificate = cert;
                return _certificate;
            }
        }
    }
}
