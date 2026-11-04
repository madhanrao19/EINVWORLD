using System;
using System.Collections.Generic;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Cloudflare's Turnstile test keys switch bot protection off on a public site: they must stop a
    /// Production start, only warn on Staging, and be allowed in Development.
    /// </summary>
    public class ProductionConfigValidatorTurnstileTests
    {
        private const string TestSiteKey = "1x00000000000000000000AA";
        private const string TestSecretKey = "1x0000000000000000000000000000000AA";

        private static IConfiguration Config(string? siteKey, string? secretKey) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;",
                ["LHDNApiConfig:BaseUrl"] = "https://api.myinvois.hasil.gov.my",
                ["LHDNApiConfig:ClientId"] = "test-client-id",
                ["LHDNApiConfig:ClientSecret"] = "test-secret",
                ["DataProtection:KeyRingPath"] = @"D:\Keys",
                ["Turnstile:SiteKey"] = siteKey,
                ["Turnstile:SecretKey"] = secretKey,
            }).Build();

        [Theory]
        [InlineData(TestSiteKey, true)]
        [InlineData("2x00000000000000000000AB", true)]
        [InlineData("3x00000000000000000000FF", true)]
        [InlineData(TestSecretKey, true)]
        [InlineData("2x0000000000000000000000000000000AA", true)]
        [InlineData("0x4AAAAAAAjucfRHqjzBJlrd", false)]
        [InlineData("1x4AAAAAAAjucfRHqjzBJlrd", false)]
        [InlineData("1x00000", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsTurnstileTestKey_MatchesOnlyCloudflareTestKeys(string? key, bool expected) =>
            Assert.Equal(expected, ProductionConfigValidator.IsTurnstileTestKey(key));

        [Fact]
        public void Production_TestSiteKey_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProductionConfigValidator.Validate(Config(TestSiteKey, "real-secret"), isProduction: true));
            Assert.Contains("Turnstile:SiteKey", ex.Message);
        }

        [Fact]
        public void Production_TestSecretKey_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProductionConfigValidator.Validate(Config("0x4AAAAAAAjucfRHqjzBJlrd", TestSecretKey), isProduction: true));
            Assert.Contains("Turnstile:SecretKey", ex.Message);
        }

        [Fact]
        public void Production_RealKeys_DoesNotThrow() =>
            Assert.Null(Record.Exception(() =>
                ProductionConfigValidator.Validate(Config("0x4AAAAAAAjucfRHqjzBJlrd", "0x4AAAAAAA-real-secret"), isProduction: true)));

        [Fact]
        public void Staging_TestKeys_WarnsButStarts() =>
            Assert.Null(Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(TestSiteKey, TestSecretKey), isProduction: false)));

        [Fact]
        public void Development_TestKeys_Allowed() =>
            Assert.Null(Record.Exception(() =>
                ProductionConfigValidator.Validate(Config(TestSiteKey, TestSecretKey), isProduction: false, isDevelopment: true)));
    }
}
