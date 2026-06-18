using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateTimeReceived",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateTimeValidated",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LongId",
                table: "InvoiceHeaders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateTimeReceived",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "DateTimeValidated",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "LongId",
                table: "InvoiceHeaders");
        }
    }
}
