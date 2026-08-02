using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddNewInvoiceReceivedEmailTrackingToInvoiceHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DEFAULT 1 (true) backfills every existing row as "not applicable" so this migration never
            // retroactively emails anyone about invoices already in the database — only rows explicitly
            // created afterward with the flag set to false (InvoiceFullSyncHelper, for a genuinely new
            // buyer-side invoice synced from LHDN) become eligible for the notification.
            migrationBuilder.AddColumn<bool>(
                name: "IsNewInvoiceReceivedEmailSent",
                table: "InvoiceHeaders",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NewInvoiceReceivedEmailSentAt",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewInvoiceReceivedEmailSentTo",
                table: "InvoiceHeaders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNewInvoiceReceivedEmailSent",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "NewInvoiceReceivedEmailSentAt",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "NewInvoiceReceivedEmailSentTo",
                table: "InvoiceHeaders");
        }
    }
}
