using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// RoleSeeder on a fresh database: no built-in default passwords. Demo users are created only from configured,
    /// non-default bootstrap passwords, and existing users are never changed.
    /// </summary>
    public class RoleSeederBootstrapTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public RoleSeederBootstrapTests(SqlServerFixture fx) => _fx = fx;

        private const string Admin = "admin@einvworld.com", Supplier = "supplier@einvworld.com", Buyer = "buyer@einvworld.com";

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("Admin@123", false)]       // the old public defaults are refused
        [InlineData("supplier@123", false)]
        [InlineData(" Buyer@123 ", false)]
        [InlineData("x9!Qm2#Lw7$Rt4", true)]
        public void Bootstrap_password_must_be_set_and_not_a_retired_default(string? password, bool usable) =>
            Assert.Equal(usable, RoleSeeder.IsUsableBootstrapPassword(password));

        private static (RoleSeeder Seeder, UserManager<ApplicationUser> Users, RoleManager<IdentityRole> Roles) Build(
            ApplicationDbContext ctx, params (string Key, string Value)[] settings)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
                .Build();
            var normalizer = new UpperInvariantLookupNormalizer();
            var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(ctx), Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(), Array.Empty<IUserValidator<ApplicationUser>>(),
                new IPasswordValidator<ApplicationUser>[] { new PasswordValidator<ApplicationUser>() }, normalizer,
                new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance);
            var roles = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(ctx), Array.Empty<IRoleValidator<IdentityRole>>(),
                normalizer, new IdentityErrorDescriber(), NullLogger<RoleManager<IdentityRole>>.Instance);
            return (new RoleSeeder(roles, users, ctx, config, NullLogger<RoleSeeder>.Instance), users, roles);
        }

        [Fact]
        public async Task Fresh_database_seeds_roles_and_only_configured_demo_users_and_never_changes_existing_ones()
        {
            if (!_fx.Available) return;

            // 1. Demo seeding on, nothing configured: roles exist, no demo user (no built-in password).
            await using (var ctx = _fx.CreateContext())
            {
                var (seeder, users, roles) = Build(ctx, ("Seeding:SeedDefaultUsers", "true"));
                await seeder.SeedRolesAndAdminAsync();

                foreach (var role in new[] { "Admin", "Supplier", "Buyer", "Director", "Representative" })
                    Assert.True(await roles.RoleExistsAsync(role), role);
                Assert.Null(await users.FindByEmailAsync(Admin));
                Assert.Null(await users.FindByEmailAsync(Supplier));
                Assert.Null(await users.FindByEmailAsync(Buyer));
            }

            // 2. The retired public default is refused even when configured explicitly.
            await using (var ctx = _fx.CreateContext())
            {
                var (seeder, users, _) = Build(ctx, ("Seeding:SeedDefaultUsers", "true"), ("Seeding:DefaultAdminPassword", "Admin@123"));
                await seeder.SeedRolesAndAdminAsync();
                Assert.Null(await users.FindByEmailAsync(Admin));
            }

            // 3. A configured strong password creates the admin, with that password and the Admin role.
            const string first = "x9!Qm2#Lw7$Rt4";
            await using (var ctx = _fx.CreateContext())
            {
                var (seeder, users, _) = Build(ctx, ("Seeding:SeedDefaultUsers", "true"), ("Seeding:DefaultAdminPassword", first));
                await seeder.SeedRolesAndAdminAsync();
                var admin = await users.FindByEmailAsync(Admin);
                Assert.NotNull(admin);
                Assert.True(await users.CheckPasswordAsync(admin!, first));
                Assert.True(await users.IsInRoleAsync(admin!, "Admin"));
                Assert.Null(await users.FindByEmailAsync(Supplier)); // its password isn't configured
            }

            // 4. A later startup with a different configured password leaves the existing user's password alone.
            await using (var ctx = _fx.CreateContext())
            {
                var (seeder, users, _) = Build(ctx, ("Seeding:SeedDefaultUsers", "true"), ("Seeding:DefaultAdminPassword", "Zz8@another#Pass1"));
                await seeder.SeedRolesAndAdminAsync();
                var admin = await users.FindByEmailAsync(Admin);
                Assert.True(await users.CheckPasswordAsync(admin!, first));
            }

            // 5. Seeding off (the Staging/Production setting): nothing is created even with a password configured.
            await using (var ctx = _fx.CreateContext())
            {
                var (seeder, users, _) = Build(ctx, ("Seeding:DefaultSupplierPassword", "Yy7#strong!Pass2"));
                await seeder.SeedRolesAndAdminAsync();
                Assert.Null(await users.FindByEmailAsync(Supplier));
            }
        }
    }
}
