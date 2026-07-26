using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyRoleId",
                table: "UserCompanies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyRoles",
                columns: table => new
                {
                    CompanyRoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CanManageUsers = table.Column<bool>(type: "bit", nullable: false),
                    CanEditProfile = table.Column<bool>(type: "bit", nullable: false),
                    CanManageBranding = table.Column<bool>(type: "bit", nullable: false),
                    CanViewAudit = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemDefined = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyRoles", x => x.CompanyRoleId);
                });

            migrationBuilder.InsertData(
                table: "CompanyRoles",
                columns: new[] { "CompanyRoleId", "CanEditProfile", "CanManageBranding", "CanManageUsers", "CanViewAudit", "IsSystemDefined", "Name" },
                values: new object[,]
                {
                    { 1, true, true, true, true, true, "Owner" },
                    { 2, true, true, true, true, true, "Admin" },
                    { 3, true, false, false, false, true, "Editor" },
                    { 4, false, false, false, false, true, "Viewer" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_CompanyRoleId",
                table: "UserCompanies",
                column: "CompanyRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCompanies_CompanyRoles_CompanyRoleId",
                table: "UserCompanies",
                column: "CompanyRoleId",
                principalTable: "CompanyRoles",
                principalColumn: "CompanyRoleId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCompanies_CompanyRoles_CompanyRoleId",
                table: "UserCompanies");

            migrationBuilder.DropTable(
                name: "CompanyRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserCompanies_CompanyRoleId",
                table: "UserCompanies");

            migrationBuilder.DropColumn(
                name: "CompanyRoleId",
                table: "UserCompanies");
        }
    }
}
