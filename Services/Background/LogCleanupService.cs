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

                    // Delete in bounded batches so a large backlog never holds a long lock or hits the
                    // command timeout. A single unbounded DELETE on a big SystemLogs table escalates to a
                    // table lock and times out (the cause of the "Execution Timeout Expired" errors); a
                    // batched loop keeps each statement small and lets other writers through between batches.
                    int batchSize = _configuration.GetValue<int>("LogCleanupSettings:BatchSize", 5000);
                    if (batchSize < 1) batchSize = 5000;

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync(stoppingToken);

                        // We use -@RetentionDays to subtract the days from the current date.
                        // DELETE TOP (@BatchSize) caps each statement; loop until a batch deletes nothing.
                        var sql = "DELETE TOP (@BatchSize) FROM SystemLogs WHERE TimeStamp < DATEADD(day, -@RetentionDays, GETDATE())";

                        long totalDeleted = 0;
                        int rowsAffected;
                        do
                        {
                            using (var command = new SqlCommand(sql, connection))
                            {
                                // Pass values safely as parameters
                                command.Parameters.AddWithValue("@RetentionDays", retentionDays);
                                command.Parameters.AddWithValue("@BatchSize", batchSize);
                                command.CommandTimeout = 120; // generous; each batch is small so this is just a safety net

                                rowsAffected = await command.ExecuteNonQueryAsync(stoppingToken);
                                totalDeleted += rowsAffected;
                            }

                            // Brief yield between batches so we don't monopolise the table on a big backlog.
                            if (rowsAffected == batchSize)
                                await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
                        }
                        while (rowsAffected == batchSize && !stoppingToken.IsCancellationRequested);

                        if (totalDeleted > 0)
                        {
                            _logger.LogInformation($"🧹 Cleanup Complete: Deleted {totalDeleted} logs older than {retentionDays} days from the Database.");
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