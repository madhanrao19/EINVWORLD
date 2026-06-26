using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncJobs_Status_NextRunAtUtc",
                table: "SyncJobs");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_LHDNStatusId",
                table: "InvoiceHeaders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SyncJobs_Status_NextRunAtUtc",
                table: "SyncJobs",
                columns: new[] { "Status", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_LHDNStatusId",
                table: "InvoiceHeaders",
                column: "LHDNStatusId");
        }
    }
}
