using System;
using System.Linq;
using System.Threading.Tasks;
using EINVWORLD.Data;
using EINVWORLD.Models.Public;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// The website database (Resources/ResourceTypes) must migrate on an empty SQL Server database. Before
    /// v1.50.49 CreateResourceTypes added a foreign key to a ResourceTypes table that no migration created (the
    /// original databases had it made by hand), so a fresh install failed. Each test uses its own throwaway
    /// database and runs only when INTEGRATION_SQLSERVER is set.
    /// </summary>
    public class WebsiteDbMigrationTests
    {
        private const string BeforeCreateResourceTypes = "20250613044307_RemoveResourceTypeColumns";

        private static WebsiteDbContext? NewContext()
        {
            var baseConn = Environment.GetEnvironmentVariable("INTEGRATION_SQLSERVER");
            if (string.IsNullOrWhiteSpace(baseConn)) return null;
            var cs = new SqlConnectionStringBuilder(baseConn)
            {
                InitialCatalog = $"einv_web_it_{Guid.NewGuid():N}",
                TrustServerCertificate = true,
            }.ConnectionString;
            return new WebsiteDbContext(new DbContextOptionsBuilder<WebsiteDbContext>().UseSqlServer(cs).Options);
        }

        private static async Task<int> CountAsync(WebsiteDbContext ctx, string sql) =>
            await ctx.Database.SqlQueryRaw<int>(sql).SingleAsync();

        private static Task<int> ForeignKeyCount(WebsiteDbContext ctx) => CountAsync(ctx,
            "SELECT COUNT(*) AS Value FROM sys.foreign_keys WHERE name = 'FK_Resources_ResourceTypes_ResourceTypeCode' AND delete_referential_action_desc = 'CASCADE'");

        [Fact]
        public async Task Empty_Database_Migrates_And_Resources_Work()
        {
            await using var ctx = NewContext();
            if (ctx == null) return;
            try
            {
                await ctx.Database.MigrateAsync();
                Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
                Assert.Equal(1, await ForeignKeyCount(ctx));

                ctx.ResourceTypes.Add(new ResourceType { Code = "article", Name = "Articles" });
                ctx.Resources.Add(new ResourceItem { Title = "t", Summary = "s", ImageUrl = "", ThumbnailUrl = "", ContentHtml = "c", Slug = "it-slug", ResourceTypeCode = "article", DatePublished = DateTime.UtcNow });
                await ctx.SaveChangesAsync();
                Assert.Equal("Articles", (await ctx.Resources.Include(r => r.ResourceType).SingleAsync()).ResourceType!.Name);

                // The foreign key is enforced: a resource can't point at a type that doesn't exist.
                ctx.Resources.Add(new ResourceItem { Title = "t", Summary = "s", ImageUrl = "", ThumbnailUrl = "", ContentHtml = "c", Slug = "it-slug-2", ResourceTypeCode = "missing", DatePublished = DateTime.UtcNow });
                await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
            }
            finally { await ctx.Database.EnsureDeletedAsync(); }
        }

        [Fact]
        public async Task Existing_Database_With_Hand_Made_ResourceTypes_Keeps_Its_Table_And_Rows()
        {
            await using var ctx = NewContext();
            if (ctx == null) return;
            try
            {
                var migrator = ctx.GetService<IMigrator>();
                await migrator.MigrateAsync(BeforeCreateResourceTypes);
                // As on the original databases: the table was made by hand (auto-named PK) and already has rows.
                await ctx.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE ResourceTypes (Code nvarchar(50) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL); " +
                    "INSERT INTO ResourceTypes VALUES ('article', 'Articles'), ('guide', 'Guides');");

                await ctx.Database.MigrateAsync();

                Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
                Assert.Equal(2, await ctx.ResourceTypes.CountAsync());
                Assert.Equal(0, await CountAsync(ctx, "SELECT COUNT(*) AS Value FROM sys.key_constraints WHERE name = 'PK_ResourceTypes'")); // ours not created
                Assert.Equal(1, await ForeignKeyCount(ctx));
            }
            finally { await ctx.Database.EnsureDeletedAsync(); }
        }

        [Fact]
        public async Task Rollback_Past_CreateResourceTypes_And_Reapply_Works()
        {
            await using var ctx = NewContext();
            if (ctx == null) return;
            try
            {
                await ctx.Database.MigrateAsync();
                var migrator = ctx.GetService<IMigrator>();

                await migrator.MigrateAsync(BeforeCreateResourceTypes);
                Assert.Equal(0, await ForeignKeyCount(ctx));
                Assert.Equal(1, await CountAsync(ctx, "SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'ResourceTypes'")); // Down keeps the table, as before

                await ctx.Database.MigrateAsync();
                Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
                Assert.Equal(1, await ForeignKeyCount(ctx));
            }
            finally { await ctx.Database.EnsureDeletedAsync(); }
        }
    }
}
