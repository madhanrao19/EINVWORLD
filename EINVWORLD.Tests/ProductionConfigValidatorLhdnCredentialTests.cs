using System;
using System.Collections.Generic;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Tests for the Production-only LHDN credential checks added alongside the appsettings
    /// consolidation: a blank ClientId, or both ClientSecret/ClientSecret2 blank, must fail startup
    /// in Production (previously these passed silently and only surfaced later as a confusing
    /// "invalid_client" from LHDN on first login) but must remain a no-op outside Production.
    /// </summary>
    public class ProductionConfigValidatorLhdnCredentialTests
    {
        private static IConfiguration Config(bool isProduction, params (string Key, string Value)[] overrides)
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;",
                ["LHDNApiConfig:BaseUrl"] = "https://api.myinvois.hasil.gov.my",
                ["LHDNApiConfig:ClientId"] = "real-client-id",
                ["LHDNApiConfig:ClientSecret"] = "real-secret",
                ["DataProtection:KeyRingPath"] = isProduction ? "D:\\Keys" : "",
            };
            foreach (var (k, v) in overrides) dict[k] = v;
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        [Fact]
        public void Production_FullyConfigured_DoesNotThrow()
        {
            var ex = Record.Exception(() => ProductionConfigValidator.Validate(Config(true), isProduction: true));
            Assert.Null(ex);
        }

        [Fact]
        public void Production_BlankClientId_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProductionConfigValidator.Validate(Config(true, ("LHDNApiConfig:ClientId", "")), isProduction: true));
            Assert.Contains("LHDNApiConfig:ClientId", ex.Message);
        }

        [Fact]
        public void Production_BothClientSecretsBlank_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProductionConfigValidator.Validate(Config(true, ("LHDNApiConfig:ClientSecret", "")), isProduction: true));
            Assert.Contains("ClientSecret", ex.Message);
        }

        [Fact]
        public void Production_OnlySecondClientSecretSet_DoesNotThrow()
        {
            // ClientSecret2 alone satisfies the check — LHDN supports either secret working.
            var ex = Record.Exception(() => ProductionConfigValidator.Validate(
                Config(true, ("LHDNApiConfig:ClientSecret", ""), ("LHDNApiConfig:ClientSecret2", "real-secret-2")),
                isProduction: true));
            Assert.Null(ex);
        }

        [Fact]
        public void NonProduction_BlankClientIdAndSecrets_DoesNotThrow()
        {
            // Dev/test configs routinely omit these — the check is Production-only.
            var ex = Record.Exception(() => ProductionConfigValidator.Validate(
                Config(false, ("LHDNApiConfig:ClientId", ""), ("LHDNApiConfig:ClientSecret", "")),
                isProduction: false));
            Assert.Null(ex);
        }
    }
}
