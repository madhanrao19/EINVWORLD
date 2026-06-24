using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceStatusSyncIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hot-path index for the InvoiceStatusUpdater background poller, which every cycle filters on
            // LHDNStatusId (non-final statuses, or Valid-without-LongId) and orders by LastUpdated. Without a
            // composite index this is a full scan on a large InvoiceHeaders table.
            // NOTE: deliberately NOT indexing LongId — it is nvarchar(max) and cannot be a key column in
            // SQL Server. The existing single-column LHDNStatusId index serves the "Valid without LongId" leg.
            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_LHDNStatusId_LastUpdated",
                table: "InvoiceHeaders",
                columns: new[] { "LHDNStatusId", "LastUpdated" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_LHDNStatusId_LastUpdated",
                table: "InvoiceHeaders");
        }
    }
}
