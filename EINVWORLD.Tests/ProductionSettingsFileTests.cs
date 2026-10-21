using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// appsettings.json is copied to Production on every deploy but carries Staging values. These tests merge it with
    /// appsettings.Production.json the way the app does and fail if any Staging folder or the Turnstile test key leaks through.
    /// </summary>
    public class ProductionSettingsFileTests
    {
        private static IConfigurationRoot LoadProduction()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EINVWORLD.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(dir!.FullName, "appsettings.json"))
                .AddJsonFile(Path.Combine(dir.FullName, "appsettings.Production.json"))
                .Build();
        }

        [Fact]
        public void Production_has_no_staging_paths_or_test_keys()
        {
            var config = LoadProduction();
            var leaks = config.AsEnumerable()
                .Where(kv => kv.Value != null && (kv.Value.Contains("EINVWORLD_STAGING", System.StringComparison.OrdinalIgnoreCase)
                                                  || kv.Value.StartsWith("Staging |", System.StringComparison.Ordinal)))
                .Select(kv => kv.Key)
                .ToList();
            Assert.Empty(leaks);
            Assert.DoesNotContain("Staging", config["EmailConfiguration:Default:FromEmailName"]);
            Assert.False(config["Turnstile:SiteKey"]!.StartsWith("1x000"), "Turnstile:SiteKey is Cloudflare's test key");
        }

        [Fact]
        public void Production_keeps_both_serilog_sinks()
        {
            var config = LoadProduction();
            Assert.Equal("File", config["Serilog:WriteTo:0:Name"]);
            Assert.Equal(@"E:\EINVWORLD\Logs\log.txt", config["Serilog:WriteTo:0:Args:path"]);
            Assert.Equal("MSSqlServer", config["Serilog:WriteTo:1:Name"]);
        }
    }
}
