using System;
using System.Collections.Generic;
using System.IO;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Tests for the DataProtection key-ring branch of <see cref="ProductionConfigValidator"/>.
    /// Contract: in Production the key ring must be set AND resolve OUTSIDE the deployable App
    /// (contentRoot) folder — a blank path, a relative path, or an absolute path under contentRoot
    /// all block startup, because each is silently wiped by a redeploy that clears App\. A path
    /// outside contentRoot passes. The containment check is skipped when contentRoot is null or
    /// when not in Production (dev may use the in-app fallback).
    /// </summary>
    public class ProductionConfigValidatorKeyRingTests
    {
        private static IConfiguration Config(string? keyRingPath)
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;",
                ["LHDNApiConfig:BaseUrl"] = "https://preprod-api.myinvois.hasil.gov.my",
                // ClientId/ClientSecret are required in Production by the validator; supply them so these
                // tests isolate the key-ring containment behaviour rather than tripping the LHDN checks.
                ["LHDNApiConfig:ClientId"] = "test-client-id",
                ["LHDNApiConfig:ClientSecret"] = "test-secret",
                ["DataProtection:KeyRingPath"] = keyRingPath,
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        [Fact]
        public void PathOutsideContentRoot_DoesNotThrow()
        {
            var root = Path.Combine(Path.GetTempPath(), "einv-app");
            var outside = Path.Combine(Path.GetTempPath(), "einv-keys");
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(outside), isProduction: true, contentRoot: root));
            Assert.Null(ex);
        }

        [Fact]
        public void AbsolutePathInsideContentRoot_Throws()
        {
            var root = Path.Combine(Path.GetTempPath(), "einv-app");
            var inside = Path.Combine(root, "DataProtectionKeys");
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(inside), isProduction: true, contentRoot: root));
            Assert.IsType<InvalidOperationException>(ex);
        }

        [Fact]
        public void RelativePathResolvingInsideContentRoot_Throws()
        {
            // A bare relative path resolves against contentRoot (matches Program.cs behaviour) → inside.
            var root = Path.Combine(Path.GetTempPath(), "einv-app");
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config("DataProtectionKeys"), isProduction: true, contentRoot: root));
            Assert.IsType<InvalidOperationException>(ex);
        }

        [Fact]
        public void BlankPath_InProduction_Throws()
        {
            var root = Path.Combine(Path.GetTempPath(), "einv-app");
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(""), isProduction: true, contentRoot: root));
            Assert.IsType<InvalidOperationException>(ex);
        }

        [Fact]
        public void PathInsideContentRoot_NotProduction_DoesNotThrow()
        {
            // Dev is allowed to use the in-app fallback.
            var root = Path.Combine(Path.GetTempPath(), "einv-app");
            var inside = Path.Combine(root, "DataProtectionKeys");
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(inside), isProduction: false, contentRoot: root));
            Assert.Null(ex);
        }

        [Fact]
        public void NoContentRoot_SkipsContainmentCheck()
        {
            // Backwards-compatible call (no contentRoot): a non-blank path passes even if it would be inside.
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config("DataProtectionKeys"), isProduction: true));
            Assert.Null(ex);
        }
    }
}
