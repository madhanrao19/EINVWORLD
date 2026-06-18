using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueSlugIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Resources",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Slug",
                table: "Resources",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resources_Slug",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Resources");
        }
    }
}
