using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtotalToInvoiceTemplateLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountExclTax",
                table: "InvoiceTemplateLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountInclTax",
                table: "InvoiceTemplateLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "InvoiceTemplateLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "InvoiceTemplateLines",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountExclTax",
                table: "InvoiceTemplateLines");

            migrationBuilder.DropColumn(
                name: "AmountInclTax",
                table: "InvoiceTemplateLines");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "InvoiceTemplateLines");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "InvoiceTemplateLines");
        }
    }
}
