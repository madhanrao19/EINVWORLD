using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EINVWORLD.Helpers
{
    /// <summary>What startup may do to the database and in the background.</summary>
    public sealed record StartupDatabasePlan(bool RunBackgroundJobs, bool RunMigrations, bool RunSeeders, string? Notice);

    /// <summary>
    /// Keeps a local <c>Development</c> run away from the shared Staging/Production databases, which hold real taxpayer
    /// data. In Development: a connection string pointing at another machine stops startup unless
    /// <c>Development:AllowSharedDatabase=true</c>, and even then background workers, migrations and seeders stay off.
    /// Against a local database they are off by default and switched on one by one with
    /// <c>Development:RunBackgroundJobs</c> / <c>RunMigrations</c> / <c>RunSeeders</c>. Any other environment
    /// (Staging, Production) is unchanged.
    /// </summary>
    public static class DevelopmentSafety
    {
        private static readonly string[] ConnectionStringNames = { "DefaultConnection", "WebsiteDb" };
        private static readonly HashSet<string> LocalHosts = new(StringComparer.OrdinalIgnoreCase)
            { ".", "(local)", "localhost", "127.0.0.1", "::1", "[::1]" };

        public static StartupDatabasePlan Evaluate(bool isDevelopment, IConfiguration config, string? machineName = null)
        {
            if (!isDevelopment)
                return new(true, config.GetValue("DatabaseSettings:AutoMigrateOnStartup", true), true, null);

            bool runJobs = config.GetValue("Development:RunBackgroundJobs", false);
            bool runMigrations = config.GetValue("Development:RunMigrations", false);
            bool runSeeders = config.GetValue("Development:RunSeeders", false);

            var shared = ConnectionStringNames
                .Where(n => !IsLocalDatabase(config.GetConnectionString(n), machineName ?? Environment.MachineName))
                .ToList();
            if (shared.Count == 0)
                return new(runJobs, runMigrations, runSeeders,
                    runJobs || runMigrations || runSeeders ? null
                        : "Development: background workers, migrations and seeders are OFF (Development:RunBackgroundJobs/RunMigrations/RunSeeders=true to enable against this local database).");

            var names = string.Join(", ", shared);
            if (!config.GetValue("Development:AllowSharedDatabase", false))
                throw new InvalidOperationException(
                    $"Development startup refused: connection string(s) {names} point at a shared (non-local) SQL Server, which holds real " +
                    "taxpayer data. Point them at a local database, or set Development:AllowSharedDatabase=true to connect on purpose " +
                    "(background workers, migrations and seeders then stay off).");
            if (runJobs || runMigrations || runSeeders)
                throw new InvalidOperationException(
                    $"Development startup refused: Development:RunBackgroundJobs/RunMigrations/RunSeeders are for an isolated local database only, " +
                    $"but {names} point at a shared SQL Server.");
            return new(false, false, false,
                $"Development: connected to a SHARED database ({names}) on purpose (Development:AllowSharedDatabase=true); background workers, migrations and seeders are OFF.");
        }

        /// <summary>True only for a parseable connection string whose server is this machine (incl. LocalDB). Fails closed.</summary>
        public static bool IsLocalDatabase(string? connectionString, string machineName)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return false;
            string dataSource;
            try { dataSource = new SqlConnectionStringBuilder(connectionString).DataSource?.Trim() ?? ""; }
            catch (Exception) { return false; }

            int colon = dataSource.IndexOf(':');
            if (colon > 0 && colon <= 4 && !dataSource.StartsWith("[")) dataSource = dataSource[(colon + 1)..]; // tcp: / np: / lpc:
            if (dataSource.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase)) return true;
            string host = dataSource.Split(',')[0].Split('\\')[0].Trim();
            return host.Length > 0 && (LocalHosts.Contains(host) || string.Equals(host, machineName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Removes the hosted services implemented in <paramref name="assembly"/> (framework ones, like the web server, stay).</summary>
        public static int RemoveOwnHostedServices(IServiceCollection services, Assembly assembly)
        {
            var own = services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Assembly == assembly).ToList();
            foreach (var d in own) services.Remove(d);
            return own.Count;
        }
    }
}
