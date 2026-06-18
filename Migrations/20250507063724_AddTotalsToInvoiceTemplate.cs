using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalsToInvoiceTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForeignCurrency",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmountExclTax",
                table: "InvoiceTemplates",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmountIncTax",
                table: "InvoiceTemplates",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDiscountAmount",
                table: "InvoiceTemplates",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalNetAmount",
                table: "InvoiceTemplates",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPayableAmount",
                table: "InvoiceTemplates",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalTaxAmount",
                table: "InvoiceTemplates",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForeignCurrency",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "TotalAmountExclTax",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "TotalAmountIncTax",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "TotalDiscountAmount",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "TotalNetAmount",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "TotalPayableAmount",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "TotalTaxAmount",
                table: "InvoiceTemplates");
        }
    }
}
