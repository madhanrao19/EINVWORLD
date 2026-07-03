using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using eInvWorld.Models.Settings;
using eInvWorld.Services;
using eInvWorld.Services.Signing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Covers the signing-key custody seam: the disabled pass-through (the current production path),
    /// certificate-provider selection by configured name, the hard failure when signing is enabled but no
    /// provider matches (an unsigned document must never be sent silently), and
    /// <see cref="FileCertificateProvider"/>'s config/file error contract. No real certificate or LHDN
    /// call is involved.
    /// </summary>
    public class DocumentSigningServiceTests
    {
        private sealed class FakeProvider : ICertificateProvider
        {
            private readonly Func<X509Certificate2>? _get;
            public FakeProvider(string name, Func<X509Certificate2>? get = null) { Name = name; _get = get; }
            public string Name { get; }
            public int Calls { get; private set; }
            public X509Certificate2 GetSigningCertificate()
            {
                Calls++;
                return _get is not null ? _get() : throw new InvalidOperationException("no cert configured in fake");
            }
        }

        /// <summary>Minimal IWebHostEnvironment for FileCertificateProvider path resolution.</summary>
        private sealed class StubEnv : IWebHostEnvironment
        {
            public string WebRootPath { get; set; } = "";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ApplicationName { get; set; } = "tests";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Path.GetTempPath();
            public string EnvironmentName { get; set; } = "Development";
        }

        private static DocumentSigningService Build(DigitalSignatureSettings settings, params ICertificateProvider[] providers) =>
            new(Options.Create(settings), providers, NullLogger<DocumentSigningService>.Instance);

        // ── Disabled pass-through (the live production path today) ────────────────────────────
        [Fact]
        public void Disabled_ReturnsInputUnchanged_AndNeverTouchesProviders()
        {
            var provider = new FakeProvider("File");
            var svc = Build(new DigitalSignatureSettings { SigningEnabled = false }, provider);

            const string json = "{\"Invoice\":[{\"ID\":[{\"_\":\"INV-1\"}]}]}";
            Assert.Same(json, svc.PrepareDocumentForSubmission(json)); // byte-for-byte: same reference
            Assert.Equal(0, provider.Calls);
            Assert.False(svc.Enabled);
        }

        [Fact]
        public void DocVersion_DefaultsTo10_WhenBlank()
        {
            var svc = Build(new DigitalSignatureSettings { DocVersion = "" });
            Assert.Equal("1.0", svc.DocVersion);
        }

        // ── Provider selection ─────────────────────────────────────────────────────────────────
        [Fact]
        public void Enabled_SelectsProviderByName_CaseInsensitively()
        {
            // The fake throws a distinctive message when its GetSigningCertificate is reached — proving
            // the RIGHT provider was chosen without needing a real signable certificate.
            var file = new FakeProvider("File");
            var vault = new FakeProvider("AzureKeyVault");
            var svc = Build(
                new DigitalSignatureSettings { SigningEnabled = true, SigningKeyProvider = "azurekeyvault" },
                file, vault);

            Assert.Throws<InvalidOperationException>(() => svc.PrepareDocumentForSubmission("{}"));
            Assert.Equal(1, vault.Calls);
            Assert.Equal(0, file.Calls);
        }

        [Fact]
        public void Enabled_BlankProviderName_FallsBackToFile()
        {
            var file = new FakeProvider("File");
            var svc = Build(
                new DigitalSignatureSettings { SigningEnabled = true, SigningKeyProvider = "" }, file);

            Assert.Throws<InvalidOperationException>(() => svc.PrepareDocumentForSubmission("{}"));
            Assert.Equal(1, file.Calls);
        }

        [Fact]
        public void Enabled_UnknownProviderName_ThrowsWithRegisteredList()
        {
            var svc = Build(
                new DigitalSignatureSettings { SigningEnabled = true, SigningKeyProvider = "Hsm" },
                new FakeProvider("File"));

            var ex = Assert.Throws<InvalidOperationException>(() => svc.PrepareDocumentForSubmission("{}"));
            Assert.Contains("Hsm", ex.Message);
            Assert.Contains("File", ex.Message); // registered providers are listed for diagnosis
        }

        // ── FileCertificateProvider error contract (unchanged from the pre-seam behaviour) ─────
        [Fact]
        public void FileProvider_BlankCertPath_ThrowsInvalidOperation()
        {
            var provider = new FileCertificateProvider(
                Options.Create(new DigitalSignatureSettings { CertPath = "" }),
                new StubEnv(), NullLogger<FileCertificateProvider>.Instance);

            Assert.Throws<InvalidOperationException>(() => provider.GetSigningCertificate());
        }

        [Fact]
        public void FileProvider_MissingFile_ThrowsFileNotFound()
        {
            var provider = new FileCertificateProvider(
                Options.Create(new DigitalSignatureSettings { CertPath = $"no-such-{Guid.NewGuid():N}.p12" }),
                new StubEnv(), NullLogger<FileCertificateProvider>.Instance);

            Assert.Throws<FileNotFoundException>(() => provider.GetSigningCertificate());
        }

        [Fact]
        public void FileProvider_Name_IsFile()
        {
            var provider = new FileCertificateProvider(
                Options.Create(new DigitalSignatureSettings()),
                new StubEnv(), NullLogger<FileCertificateProvider>.Instance);
            Assert.Equal("File", provider.Name);
        }
    }
}
