using System;
using System.Collections.Generic;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Tests for the webhook branch of <see cref="ProductionConfigValidator"/>. The webhook checks are
    /// deliberately <em>warnings, not blockers</em> (there are no required secrets — per-subscription
    /// secrets are generated in-app), so the contract under test is: enabling webhooks, even with the
    /// SSRF/TLS guards off in Production, must never turn startup into a hard failure; and the section is
    /// ignored entirely when disabled.
    /// </summary>
    public class ProductionConfigValidatorWebhookTests
    {
        private static IConfiguration Config(params (string Key, string Value)[] overrides)
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;",
                ["LHDNApiConfig:BaseUrl"] = "https://preprod-api.myinvois.hasil.gov.my",
                ["DataProtection:KeyRingPath"] = "D:\\Keys",
            };
            foreach (var (k, v) in overrides) dict[k] = v;
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        [Fact]
        public void WebhooksDisabled_IgnoresWebhookKeys()
        {
            var ex = Record.Exception(() => ProductionConfigValidator.Validate(
                Config(("Webhooks:Enabled", "false"),
                       ("Webhooks:RequireHttps", "false"),
                       ("Webhooks:BlockPrivateNetworks", "false")),
                isProduction: true));
            Assert.Null(ex);
        }

        [Fact]
        public void WebhooksEnabled_InsecureInProduction_WarnsButDoesNotThrow()
        {
            // Both SSRF/TLS guards off in Production → warnings only, never a blocking error.
            var ex = Record.Exception(() => ProductionConfigValidator.Validate(
                Config(("Webhooks:Enabled", "true"),
                       ("Webhooks:RequireHttps", "false"),
                       ("Webhooks:BlockPrivateNetworks", "false"),
                       ("Webhooks:DeliveryTimeoutSeconds", "0")),
                isProduction: true));
            Assert.Null(ex);
        }

        [Fact]
        public void WebhooksEnabled_SecureDefaults_DoesNotThrow()
        {
            var ex = Record.Exception(() => ProductionConfigValidator.Validate(
                Config(("Webhooks:Enabled", "true")), isProduction: true));
            Assert.Null(ex);
        }
    }
}
