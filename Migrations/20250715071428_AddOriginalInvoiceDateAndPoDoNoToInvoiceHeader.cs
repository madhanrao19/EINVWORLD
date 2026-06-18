using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalInvoiceDateAndPoDoNoToInvoiceHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Attention",
                table: "InvoiceHeaders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNo",
                table: "InvoiceHeaders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "InvoiceHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalInvoiceDate",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoDoNo",
                table: "InvoiceHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attention",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "BankAccountNo",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceDate",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "PoDoNo",
                table: "InvoiceHeaders");
        }
    }
}
