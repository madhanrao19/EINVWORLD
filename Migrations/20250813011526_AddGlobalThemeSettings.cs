using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalThemeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalThemeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataLayout = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataTheme = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataThemeColors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataTopbar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataSidebar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataSidebarSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataSidebarImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataLayoutWidth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataLayoutPosition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataLayoutStyle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataBsTheme = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataPreloader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataBodyImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataSidebarVisibility = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalThemeSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalThemeSettings");
        }
    }
}
