using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionCancellationEmailTrackingToInvoiceHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DEFAULT 1 (true) backfills every existing row as "not applicable" — same pattern as
            // IsNewInvoiceReceivedEmailSent — so this migration never retroactively emails anyone
            // about invoices already in the database. Only the reject/cancel handlers explicitly set
            // these to false, in the same save as the status transition, opting that one row into the
            // retry pipeline (InvoiceFinalizer/InvoiceStatusUpdater).
            migrationBuilder.AddColumn<bool>(
                name: "IsRejectionEmailSent",
                table: "InvoiceHeaders",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectionEmailSentAt",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionEmailSentTo",
                table: "InvoiceHeaders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancellationEmailSent",
                table: "InvoiceHeaders",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationEmailSentAt",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationEmailSentTo",
                table: "InvoiceHeaders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Persisted so a background retry of the cancellation email can rebuild the email body
            // without the original interactive request's in-memory reason (Reject already has
            // RejectedReason for the same purpose).
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "InvoiceHeaders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRejectionEmailSent",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "RejectionEmailSentAt",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "RejectionEmailSentTo",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "IsCancellationEmailSent",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "CancellationEmailSentAt",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "CancellationEmailSentTo",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "InvoiceHeaders");
        }
    }
}
