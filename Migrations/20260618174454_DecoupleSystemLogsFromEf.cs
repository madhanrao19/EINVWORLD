using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleSystemLogsFromEf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration ONLY removes SystemLogs from the EF model snapshot — EF no longer manages it.
            // The DropTable that EF scaffolded here was intentionally removed: the table is PRESERVED and
            // is now owned by the Serilog MSSqlServer sink (autoCreateSqlTable=true). Existing log rows
            // are kept; the sink continues writing to the same table.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: reverting this migration does not re-add SystemLogs to EF (it stays sink-owned).
        }
    }
}
