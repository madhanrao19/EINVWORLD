using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceTypeToResourceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resources_ResourceTypes_ResourceTypeCode",
                table: "Resources");

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_ResourceTypes_ResourceTypeCode",
                table: "Resources",
                column: "ResourceTypeCode",
                principalTable: "ResourceTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resources_ResourceTypes_ResourceTypeCode",
                table: "Resources");

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_ResourceTypes_ResourceTypeCode",
                table: "Resources",
                column: "ResourceTypeCode",
                principalTable: "ResourceTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
