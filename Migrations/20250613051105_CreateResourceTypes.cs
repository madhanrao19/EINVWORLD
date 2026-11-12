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

            // ResourceTypes was first created by hand on the original databases, so this migration only added the
            // FK and a fresh database failed here. Create it only when missing: databases that already have it (and
            // already applied this migration) are untouched. Same shape as the model: Code nvarchar(50) PK, Name nvarchar(100).
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[dbo].[ResourceTypes]', N'U') IS NULL
CREATE TABLE [dbo].[ResourceTypes] (
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_ResourceTypes] PRIMARY KEY ([Code])
);");

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
