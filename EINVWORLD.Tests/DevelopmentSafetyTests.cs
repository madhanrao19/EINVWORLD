using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EINVWORLD.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Startup safeguards: a Development run never touches the shared Staging/Production database by accident, and
    /// Staging/Production startup is unchanged.
    /// </summary>
    public class DevelopmentSafetyTests
    {
        private const string Shared = "Server=192.168.1.26,1433;Database=EINVWORLD_STAGING;User Id=u;Password=p;TrustServerCertificate=True";
        private const string SharedWebsite = "Server=192.168.1.26,1433;Database=EINVWORLDWEBSITE_STAGING;User Id=u;Password=p";
        private const string Local = "Server=(localdb)\\MSSQLLocalDB;Database=EINVWORLD_DEV;Trusted_Connection=True";

        private static IConfiguration Config(string? main, string? website, params (string Key, string Value)[] extra)
        {
            var d = new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = main, ["ConnectionStrings:WebsiteDb"] = website };
            foreach (var (k, v) in extra) d[k] = v;
            return new ConfigurationBuilder().AddInMemoryCollection(d).Build();
        }

        // ---- Staging / Production: unchanged ----

        [Fact]
        public void Server_environments_keep_workers_seeders_and_their_migration_setting()
        {
            var plan = DevelopmentSafety.Evaluate(false, Config(Shared, SharedWebsite, ("DatabaseSettings:AutoMigrateOnStartup", "false")));
            Assert.Equal(new StartupDatabasePlan(true, false, true, null), plan);

            Assert.True(DevelopmentSafety.Evaluate(false, Config(Shared, SharedWebsite)).RunMigrations); // code default stays true
        }

        [Fact]
        public void Server_environments_ignore_development_switches()
        {
            var plan = DevelopmentSafety.Evaluate(false, Config(Shared, SharedWebsite,
                ("Development:RunBackgroundJobs", "false"), ("Development:RunSeeders", "false")));
            Assert.True(plan.RunBackgroundJobs);
            Assert.True(plan.RunSeeders);
        }

        // ---- Development: fail closed ----

        [Theory]
        [InlineData(Shared, Local)]
        [InlineData(Local, SharedWebsite)]
        [InlineData("", Local)]                       // blank
        [InlineData(null, Local)]                     // missing
        [InlineData("this is not a connection string", Local)]
        [InlineData("Server=einvworld-db.example.com;Database=x", Local)]
        public void Development_refuses_a_shared_or_unknown_database(string? main, string? website)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => DevelopmentSafety.Evaluate(true, Config(main, website), "DEVPC"));
            Assert.Contains("Development startup refused", ex.Message);
            Assert.DoesNotContain("Password", ex.Message); // never echoes the connection string
        }

        [Fact]
        public void Opting_in_to_a_shared_database_keeps_everything_off()
        {
            var plan = DevelopmentSafety.Evaluate(true, Config(Shared, SharedWebsite, ("Development:AllowSharedDatabase", "true")), "DEVPC");

            Assert.False(plan.RunBackgroundJobs);
            Assert.False(plan.RunMigrations);
            Assert.False(plan.RunSeeders);
            Assert.Contains("SHARED", plan.Notice);
        }

        [Theory]
        [InlineData("Development:RunBackgroundJobs")]
        [InlineData("Development:RunMigrations")]
        [InlineData("Development:RunSeeders")]
        public void Overrides_are_refused_against_a_shared_database(string key)
        {
            var config = Config(Shared, SharedWebsite, ("Development:AllowSharedDatabase", "true"), (key, "true"));
            Assert.Throws<InvalidOperationException>(() => DevelopmentSafety.Evaluate(true, config, "DEVPC"));
        }

        [Fact]
        public void AutoMigrate_setting_alone_does_not_migrate_in_development()
        {
            var plan = DevelopmentSafety.Evaluate(true, Config(Local, Local, ("DatabaseSettings:AutoMigrateOnStartup", "true")), "DEVPC");
            Assert.False(plan.RunMigrations);
        }

        // ---- Development against a local database ----

        [Fact]
        public void Local_database_defaults_to_everything_off()
        {
            var plan = DevelopmentSafety.Evaluate(true, Config(Local, Local), "DEVPC");
            Assert.Equal((false, false, false), (plan.RunBackgroundJobs, plan.RunMigrations, plan.RunSeeders));
            Assert.Contains("OFF", plan.Notice);
        }

        [Fact]
        public void Local_database_overrides_switch_each_one_on()
        {
            var plan = DevelopmentSafety.Evaluate(true, Config(Local, Local,
                ("Development:RunBackgroundJobs", "true"), ("Development:RunMigrations", "true"), ("Development:RunSeeders", "true")), "DEVPC");
            Assert.Equal((true, true, true), (plan.RunBackgroundJobs, plan.RunMigrations, plan.RunSeeders));
        }

        [Theory]
        [InlineData("Server=localhost;Database=eInvWorld;Trusted_Connection=True", true)]
        [InlineData("Server=.;Database=a", true)]
        [InlineData("Server=.\\SQLEXPRESS;Database=a", true)]
        [InlineData("Server=(local);Database=a", true)]
        [InlineData("Data Source=tcp:127.0.0.1,1433;Database=a", true)]
        [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=a", true)]
        [InlineData("Server=DEVPC\\SQLEXPRESS;Database=a", true)]       // this machine by name
        [InlineData("Server=192.168.1.26,1433;Database=a", false)]
        [InlineData("Server=tcp:192.168.1.26;Database=a", false)]
        [InlineData("Server=EINVWORLDWEBSER;Database=a", false)]
        [InlineData("Server=localhost.evil.example;Database=a", false)]
        [InlineData("", false)]
        public void Recognises_local_servers_only(string cs, bool expected) =>
            Assert.Equal(expected, DevelopmentSafety.IsLocalDatabase(cs, "DEVPC"));

        // ---- background worker removal ----

        /// <summary>Stands in for a framework hosted service (the web server): it lives outside the app assembly.</summary>
        private sealed class OtherAssemblyWorker : BackgroundService
        {
            protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
        }

        [Fact]
        public void Removes_only_the_app_assemblys_hosted_services()
        {
            var services = new ServiceCollection();
            services.AddHostedService<eInvWorld.Services.Background.LogCleanupService>();
            services.AddHostedService<OtherAssemblyWorker>();

            int removed = DevelopmentSafety.RemoveOwnHostedServices(services, typeof(DevelopmentSafety).Assembly);

            Assert.Equal(1, removed);
            Assert.Equal(typeof(OtherAssemblyWorker), Assert.Single(services).ImplementationType);
        }
    }
}
