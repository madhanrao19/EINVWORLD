using System;
using System.Collections.Generic;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>Email links come from the configured site address, never from the request's Host header (reset-link poisoning).</summary>
    public class EmailLinksTests
    {
        private static IConfiguration Config(string? baseUrl) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [EmailLinks.BaseUrlKey] = baseUrl })
            .Build();

        [Theory]
        [InlineData("https://www.einvworld.com/", "/Identity/Account/ResetPassword?code=x", "https://www.einvworld.com/Identity/Account/ResetPassword?code=x")]
        [InlineData("https://staging.einvworld.com", "Identity/Account/ConfirmEmail?userId=1", "https://staging.einvworld.com/Identity/Account/ConfirmEmail?userId=1")]
        public void Joins_The_Configured_Site_With_The_Page_Path(string baseUrl, string relative, string expected) =>
            Assert.Equal(expected, EmailLinks.Absolute(Config(baseUrl), relative));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-url")]
        public void Refuses_To_Build_A_Link_Without_A_Configured_Site(string? baseUrl) =>
            Assert.Throws<InvalidOperationException>(() => EmailLinks.Absolute(Config(baseUrl), "/x"));

        [Fact]
        public void Refuses_An_Unresolved_Page() =>
            Assert.Throws<InvalidOperationException>(() => EmailLinks.Absolute(Config("https://www.einvworld.com/"), null));

        [Fact]
        public void Every_Environment_Has_A_Site_Address()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "EINVWORLD.csproj"))) dir = dir.Parent;
            foreach (var env in new[] { "Production", "Staging", "Development" })
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile(System.IO.Path.Combine(dir!.FullName, "appsettings.json"))
                    .AddJsonFile(System.IO.Path.Combine(dir.FullName, $"appsettings.{env}.json"), optional: true)
                    .Build();
                Assert.StartsWith("https://", EmailLinks.Absolute(config, "/login"));
            }
            var prod = new ConfigurationBuilder()
                .AddJsonFile(System.IO.Path.Combine(dir!.FullName, "appsettings.json"))
                .AddJsonFile(System.IO.Path.Combine(dir.FullName, "appsettings.Production.json")).Build();
            Assert.Equal("https://www.einvworld.com/login", EmailLinks.Absolute(prod, "/login"));
        }
    }
}
