using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace eInvWorld.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class LogExportController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public LogExportController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadLogs(
            [FromQuery] string format = "csv",
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? level = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var content = new StringBuilder();

            bool isCsv = format.ToLower() == "csv";
            string delimiter = isCsv ? "," : " | ";

            // Corrected Header: "User" label is fine for the file header
            content.AppendLine($"Timestamp{delimiter}User{delimiter}IP Address{delimiter}Level{delimiter}Message");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Build dynamic SQL using UserName instead of [User]
                var sql = new StringBuilder("SELECT TimeStamp, UserName, IPAddress, [Level], Message FROM SystemLogs WHERE 1=1 ");

                if (!string.IsNullOrEmpty(searchTerm))
                    sql.Append("AND (Message LIKE @search OR UserName LIKE @search OR IPAddress LIKE @search) ");

                if (!string.IsNullOrEmpty(level))
                    sql.Append("AND [Level] = @level ");

                if (startDate.HasValue)
                    sql.Append("AND TimeStamp >= @start ");

                if (endDate.HasValue)
                    sql.Append("AND TimeStamp < @end ");

                sql.Append("ORDER BY TimeStamp DESC");

                using (var command = new SqlCommand(sql.ToString(), connection))
                {
                    // Add Parameters to prevent SQL Injection
                    if (!string.IsNullOrEmpty(searchTerm)) command.Parameters.AddWithValue("@search", $"%{searchTerm}%");
                    if (!string.IsNullOrEmpty(level)) command.Parameters.AddWithValue("@level", level);
                    if (startDate.HasValue) command.Parameters.AddWithValue("@start", startDate.Value);
                    if (endDate.HasValue) command.Parameters.AddWithValue("@end", endDate.Value.AddDays(1));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string? timestamp = reader["TimeStamp"]?.ToString();
                            string? user = reader["UserName"]?.ToString(); // Corrected index name
                            string? ip = reader["IPAddress"]?.ToString();
                            string? levelVal = reader["Level"]?.ToString();
                            string? message = reader["Message"]?.ToString()?.Replace("\n", " ").Replace("\r", " ");

                            if (isCsv)
                            {
                                content.AppendLine($"\"{timestamp}\",\"{user}\",\"{ip}\",\"{levelVal}\",\"{message?.Replace("\"", "'")}\"");
                            }
                            else
                            {
                                content.AppendLine($"{timestamp}{delimiter}{user}{delimiter}{ip}{delimiter}{levelVal}{delimiter}{message}");
                            }
                        }
                    }
                }
            }

            var bytes = Encoding.UTF8.GetBytes(content.ToString());
            string extension = isCsv ? "csv" : "txt";
            return File(bytes, isCsv ? "text/csv" : "text/plain", $"SystemLogs_{DateTime.Now:yyyyMMdd}.{extension}");
        }
    }
}