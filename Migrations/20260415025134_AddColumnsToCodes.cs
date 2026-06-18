using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnsToCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "UnitTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "UnitTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "TaxTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "TaxTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "StateCodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "StateCodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "PaymentMethods",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "PaymentMethods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "MSICSubCategoryCodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "MSICSubCategoryCodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "EInvoiceTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "EInvoiceTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "CurrencyCodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "CurrencyCodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "CountryCodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "CountryCodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ClassificationCodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "ClassificationCodes",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "UnitTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "UnitTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "TaxTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "TaxTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "StateCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "StateCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "MSICSubCategoryCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "MSICSubCategoryCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "EInvoiceTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "EInvoiceTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CurrencyCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "CurrencyCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CountryCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "CountryCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ClassificationCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "ClassificationCodes");
        }
    }
}
