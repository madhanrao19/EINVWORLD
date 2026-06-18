using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceStatusToResourceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Resources",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Resources");
        }
    }
}
