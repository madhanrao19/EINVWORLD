using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models
{
    // This maps to the table created by Serilog
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