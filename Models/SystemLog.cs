using System;

namespace eInvWorld.Models
{
    // Plain read DTO for the "SystemLogs" table. The table is OWNED by the Serilog MSSqlServer sink,
    // which auto-creates it (autoCreateSqlTable=true in appsettings) — it is NOT an EF entity and is not
    // managed by EF migrations. The admin "System Logs" page reads it via a raw SQL query
    // (ApplicationDbContext.Database.SqlQueryRaw<SystemLog>). Keep these properties matching the sink's
    // columnOptionsSection columns in appsettings.json (standard columns + IPAddress + UserName).
    public class SystemLog
    {
        public int Id { get; set; }

        public string Message { get; set; } = null!;

        public string Level { get; set; } = null!;

        public DateTime TimeStamp { get; set; }

        public string? Exception { get; set; }

        public string? LogEvent { get; set; } // Stores JSON data if needed

        public string? IPAddress { get; set; }

        public string? UserName { get; set; }
    }
}