using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class AddSeoGeoFieldsToResourceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "Resources",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "Resources",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FocusKeyword",
                table: "Resources",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalUrl",
                table: "Resources",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OgText",
                table: "Resources",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageAlt",
                table: "Resources",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "Resources",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tldr",
                table: "Resources",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaType",
                table: "Resources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FaqItemsJson",
                table: "Resources",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MetaTitle", table: "Resources");
            migrationBuilder.DropColumn(name: "MetaDescription", table: "Resources");
            migrationBuilder.DropColumn(name: "FocusKeyword", table: "Resources");
            migrationBuilder.DropColumn(name: "CanonicalUrl", table: "Resources");
            migrationBuilder.DropColumn(name: "OgText", table: "Resources");
            migrationBuilder.DropColumn(name: "ImageAlt", table: "Resources");
            migrationBuilder.DropColumn(name: "Author", table: "Resources");
            migrationBuilder.DropColumn(name: "Tldr", table: "Resources");
            migrationBuilder.DropColumn(name: "SchemaType", table: "Resources");
            migrationBuilder.DropColumn(name: "FaqItemsJson", table: "Resources");
        }
    }
}
