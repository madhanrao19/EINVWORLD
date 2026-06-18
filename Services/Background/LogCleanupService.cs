using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace eInvWorld.Services.Background
{
    public class LogCleanupService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LogCleanupService> _logger;
        // Check for old logs every 4 hours
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(4);

        public LogCleanupService(IConfiguration configuration, ILogger<LogCleanupService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ Log Cleanup Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. Get Settings from AppSettings (with a default fallback of 30 if missing)
                    int retentionDays = _configuration.GetValue<int>("LogCleanupSettings:RetentionDays", 30);
                    var connectionString = _configuration.GetConnectionString("DefaultConnection");

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync(stoppingToken);

                        // 2. Define the cleanup command using a Parameter
                        // We use -@RetentionDays to subtract the days from the current date
                        var sql = "DELETE FROM SystemLogs WHERE TimeStamp < DATEADD(day, -@RetentionDays, GETDATE())";

                        using (var command = new SqlCommand(sql, connection))
                        {
                            // Pass the value safely as a parameter
                            command.Parameters.AddWithValue("@RetentionDays", retentionDays);

                            var rowsAffected = await command.ExecuteNonQueryAsync(stoppingToken);

                            if (rowsAffected > 0)
                            {
                                _logger.LogInformation($"🧹 Cleanup Complete: Deleted {rowsAffected} logs older than {retentionDays} days from the Database.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error cleaning up SystemLogs database table.");
                }

                // 3. Wait 4 hours before checking again
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}