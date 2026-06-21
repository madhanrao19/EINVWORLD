using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncJobDurability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Durable-queue columns: turn a SyncJobs row into the work item the DurableSyncJobWorker
            // claims (LockedBy/LockedUntilUtc), retries (AttemptCount/MaxAttempts/NextRunAtUtc) and
            // reconstructs from data (PayloadJson) — so jobs survive an app-pool recycle / reboot.
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "SyncJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "SyncJobs",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<System.DateTime>(
                name: "NextRunAtUtc",
                table: "SyncJobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "SyncJobs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<System.DateTime>(
                name: "LockedUntilUtc",
                table: "SyncJobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadJson",
                table: "SyncJobs",
                type: "nvarchar(max)",
                nullable: true);

            // Index for the worker's poll: WHERE Status = 'Queued' AND NextRunAtUtc <= now.
            migrationBuilder.CreateIndex(
                name: "IX_SyncJobs_Status_NextRunAtUtc",
                table: "SyncJobs",
                columns: new[] { "Status", "NextRunAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncJobs_Status_NextRunAtUtc",
                table: "SyncJobs");

            migrationBuilder.DropColumn(name: "AttemptCount", table: "SyncJobs");
            migrationBuilder.DropColumn(name: "MaxAttempts", table: "SyncJobs");
            migrationBuilder.DropColumn(name: "NextRunAtUtc", table: "SyncJobs");
            migrationBuilder.DropColumn(name: "LockedBy", table: "SyncJobs");
            migrationBuilder.DropColumn(name: "LockedUntilUtc", table: "SyncJobs");
            migrationBuilder.DropColumn(name: "PayloadJson", table: "SyncJobs");
        }
    }
}
