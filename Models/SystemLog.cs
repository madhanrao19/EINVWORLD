using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models
{
    // Maps to the "SystemLogs" table that the Serilog MSSqlServer sink writes to.
    // The table schema is owned and auto-created/updated by EF migrations (AddSystemLogsTable /
    // AddUserNameToLogs), which run on startup when DatabaseSettings:AutoMigrateOnStartup is true.
    // The Serilog sink therefore keeps autoCreateSqlTable=false so it never races EF's CreateTable on a
    // fresh database. Keep these columns in sync with the sink's columnOptionsSection in appsettings.json.
    [Table("SystemLogs")]
    public class SystemLog
    {
        [Key]
        public int Id { get; set; }

        public string Message { get; set; } = null!;

        public string Level { get; set; } = null!;

        public DateTime TimeStamp { get; set; }

        public string? Exception { get; set; }

        public string? LogEvent { get; set; } // Stores JSON data if needed

        public string? IPAddress { get; set; }

        // ✅ ADD THIS NEW COLUMN
        public string? UserName { get; set; }
    }
}