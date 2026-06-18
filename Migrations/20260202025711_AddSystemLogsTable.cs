using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SystemLogs is now OWNED by the Serilog MSSqlServer sink (autoCreateSqlTable=true), not EF.
            // This original CreateTable is intentionally neutralised so a FRESH database lets Serilog
            // create the table (avoiding a race where EF's CREATE TABLE collides with the sink's).
            // On existing databases this migration is already recorded as applied and never re-runs,
            // so emptying the body has no effect there.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: EF no longer owns the SystemLogs table (see Up).
        }
    }
}
