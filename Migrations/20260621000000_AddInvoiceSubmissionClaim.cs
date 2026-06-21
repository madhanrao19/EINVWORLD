using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceSubmissionClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Concurrency claim used to make the double-submit guard atomic (compare-and-set before the
            // LHDN call). Nullable; no default — an unclaimed/unsubmitted invoice has NULL.
            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionClaimedAtUtc",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionClaimedAtUtc",
                table: "InvoiceHeaders");
        }
    }
}
