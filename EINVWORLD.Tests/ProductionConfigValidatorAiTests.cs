using System;
using System.Collections.Generic;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Focused tests for the AI branch of <see cref="ProductionConfigValidator"/>: the canonical "AI"
    /// section is validated when present (and takes precedence over the legacy "AIAssistant" section),
    /// enabling AI without a BaseUrl/Model is a blocking error, and a missing AI config is a no-op.
    /// </summary>
    public class ProductionConfigValidatorAiTests
    {
        /// <summary>Builds config with the always-required keys satisfied, plus any AI overrides supplied.</summary>
        private static IConfiguration Config(params (string Key, string Value)[] overrides)
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;",
                ["LHDNApiConfig:BaseUrl"] = "https://preprod-api.myinvois.hasil.gov.my",
            };
            foreach (var (k, v) in overrides) dict[k] = v;
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        [Fact]
        public void AiDisabled_DoesNotValidateAiKeys()
        {
            // Enabled=false → BaseUrl/Model absence is irrelevant.
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(("AI:Enabled", "false")), isProduction: false));
            Assert.Null(ex);
        }

        [Fact]
        public void MissingAiConfig_IsANoOp()
        {
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(), isProduction: false));
            Assert.Null(ex);
        }

        [Fact]
        public void AiEnabled_WithBlankBaseUrlAndModel_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProductionConfigValidator.Validate(Config(("AI:Enabled", "true")), isProduction: false));

            Assert.Contains("AI:BaseUrl", ex.Message);
            Assert.Contains("AI:Model", ex.Message);
        }

        [Fact]
        public void AiEnabled_FullyConfigured_DoesNotThrow()
        {
            var ex = Record.Exception(() => ProductionConfigValidator.Validate(
                Config(("AI:Enabled", "true"), ("AI:BaseUrl", "http://localhost:11434"), ("AI:Model", "gemma3:12b")),
                isProduction: false));
            Assert.Null(ex);
        }

        [Fact]
        public void AiSection_TakesPrecedenceOverLegacyAiAssistant()
        {
            // Valid "AI" section present → the invalid legacy "AIAssistant" section must be ignored.
            var ex = Record.Exception(() => ProductionConfigValidator.Validate(
                Config(
                    ("AI:Enabled", "true"), ("AI:BaseUrl", "http://localhost:11434"), ("AI:Model", "gemma3:12b"),
                    ("AIAssistant:Enabled", "true")), // legacy enabled but blank — should NOT be checked
                isProduction: false));
            Assert.Null(ex);
        }

        [Fact]
        public void LegacyAiAssistant_UsedWhenNoAiSection()
        {
            // No "AI" section → the validator falls back to "AIAssistant".
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProductionConfigValidator.Validate(Config(("AIAssistant:Enabled", "true")), isProduction: false));

            Assert.Contains("AIAssistant:BaseUrl", ex.Message);
        }
    }
}
