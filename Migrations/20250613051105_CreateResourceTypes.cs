using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class CreateResourceTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ Add the FK column to Resources
            migrationBuilder.AddColumn<string>(
                name: "ResourceTypeCode",
                table: "Resources",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "article"); // Default to a valid existing code

            // ❌ DO NOT create ResourceTypes table if it already exists
            // migrationBuilder.CreateTable(...)

            // ✅ Create index
            migrationBuilder.CreateIndex(
                name: "IX_Resources_ResourceTypeCode",
                table: "Resources",
                column: "ResourceTypeCode");

            // ✅ Add FK constraint
            migrationBuilder.AddForeignKey(
                name: "FK_Resources_ResourceTypes_ResourceTypeCode",
                table: "Resources",
                column: "ResourceTypeCode",
                principalTable: "ResourceTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resources_ResourceTypes_ResourceTypeCode",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Resources_ResourceTypeCode",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "ResourceTypeCode",
                table: "Resources");

            // ❌ DO NOT drop the table
            // migrationBuilder.DropTable("ResourceTypes");
        }

    }
}
