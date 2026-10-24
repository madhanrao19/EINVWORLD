using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <summary>
    /// Template lines keep a line's Fee / Charge (amount + reason), so Save as template, Create from template,
    /// Edit template and Recurring generation no longer drop it. Additive only; existing rows get NULL (= no fee).
    /// </summary>
    public partial class AddTemplateLineFeeCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FeeChargeAmount",
                table: "InvoiceTemplateLines",
                type: "decimal(18, 2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeChargeReason",
                table: "InvoiceTemplateLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FeeChargeAmount", table: "InvoiceTemplateLines");
            migrationBuilder.DropColumn(name: "FeeChargeReason", table: "InvoiceTemplateLines");
        }
    }
}
