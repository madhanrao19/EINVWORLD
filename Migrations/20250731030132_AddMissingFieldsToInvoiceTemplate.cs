using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingFieldsToInvoiceTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Attention",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNo",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldRegNo",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalInvoiceDate",
                table: "InvoiceTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoDoNo",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attention",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "BankAccountNo",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "OldRegNo",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceDate",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "PoDoNo",
                table: "InvoiceTemplates");
        }
    }
}
