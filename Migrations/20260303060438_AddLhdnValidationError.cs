using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddLhdnValidationError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // KEEP ONLY THIS PART: Adding the new column to InvoiceHeaders
            migrationBuilder.AddColumn<string>(
                name: "LHDNValidationErrorJson",
                table: "InvoiceHeaders",
                type: "nvarchar(max)",
                nullable: true);

            // (Removed the InvoiceTemplates PublicCustomerId code that was causing the error)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // KEEP ONLY THIS PART: Removing the new column if we rollback
            migrationBuilder.DropColumn(
                name: "LHDNValidationErrorJson",
                table: "InvoiceHeaders");

            // (Removed the InvoiceTemplates PublicCustomerId code)
        }
    }
}