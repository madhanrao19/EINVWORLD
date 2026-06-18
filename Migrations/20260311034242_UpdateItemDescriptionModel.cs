using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class UpdateItemDescriptionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassificationCode",
                table: "ItemDescriptions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByCompanyId",
                table: "ItemDescriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                table: "ItemDescriptions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassificationCode",
                table: "ItemDescriptions");

            migrationBuilder.DropColumn(
                name: "CreatedByCompanyId",
                table: "ItemDescriptions");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                table: "ItemDescriptions");
        }
    }
}
