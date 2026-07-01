using System;
using System.Collections.Generic;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Focused tests for the AI branch of <see cref="ProductionConfigValidator"/>: the "AI" section is
    /// the only section consulted, enabling AI without a BaseUrl/Model is a blocking error, and a missing
    /// AI config is a no-op. The retired "AIAssistant" section is ignored entirely.
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
        public void RetiredAiAssistantSection_IsIgnored()
        {
            // The legacy "AIAssistant" section is no longer read: even enabled + blank must NOT error,
            // because only the "AI" section is consulted now.
            var ex = Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(("AIAssistant:Enabled", "true")), isProduction: false));
            Assert.Null(ex);
        }
    }
}
