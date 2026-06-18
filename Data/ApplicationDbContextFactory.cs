using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace eInvWorld.Data
{
    /// <summary>
    /// Design-time factory used ONLY by the EF Core CLI (`dotnet ef migrations add`, `database update`,
    /// `migrations script`). It builds the context directly from configuration, so the tools do NOT run
    /// the web host's Program.Main (which migrates, seeds, and loads the native wkhtmltox DLL). Not used
    /// at runtime. Connection string is read from appsettings / env vars; a placeholder is used as a last
    /// resort because `migrations add` and `migrations script` do not open a DB connection.
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Secrets are externalized, so the connection string may be blank in appsettings.json.
                // `migrations add` / `migrations script` do not open a connection, so a placeholder is fine.
                connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=EINVWORLD_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";
            }

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new ApplicationDbContext(options);
        }
    }
}
