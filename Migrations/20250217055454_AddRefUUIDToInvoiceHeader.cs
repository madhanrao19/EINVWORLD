using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddRefUUIDToInvoiceHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefUUID",
                table: "InvoiceHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefUUID",
                table: "InvoiceHeaders");
        }
    }
}
