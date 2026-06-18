using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailAndPdfFlagsToInvoiceHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsValidationEmailSent",
                table: "InvoiceHeaders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPdfGenerated",
                table: "InvoiceHeaders",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsValidationEmailSent",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "IsPdfGenerated",
                table: "InvoiceHeaders");
        }
    }
}
