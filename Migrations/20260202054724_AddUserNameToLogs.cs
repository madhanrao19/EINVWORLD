using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNameToLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Neutralised: the SystemLogs table (incl. IPAddress / UserName columns) is now created and
            // owned by the Serilog MSSqlServer sink (autoCreateSqlTable=true), not EF migrations.
            // Already applied on existing databases (never re-runs); a no-op on a fresh database so the
            // sink owns the schema. See Migrations/20260202025711_AddSystemLogsTable.cs.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: EF no longer owns the SystemLogs table.
        }
    }
}
