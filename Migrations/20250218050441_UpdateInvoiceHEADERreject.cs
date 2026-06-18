using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInvoiceHEADERreject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                table: "InvoiceHeaders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedReason",
                table: "InvoiceHeaders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedTimestamp",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "RejectedReason",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "RejectedTimestamp",
                table: "InvoiceHeaders");
        }
    }
}
