using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyRolePartyInfoScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartyInfoId",
                table: "CompanyRoles",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CompanyRoles",
                keyColumn: "CompanyRoleId",
                keyValue: 1,
                column: "PartyInfoId",
                value: null);

            migrationBuilder.UpdateData(
                table: "CompanyRoles",
                keyColumn: "CompanyRoleId",
                keyValue: 2,
                column: "PartyInfoId",
                value: null);

            migrationBuilder.UpdateData(
                table: "CompanyRoles",
                keyColumn: "CompanyRoleId",
                keyValue: 3,
                column: "PartyInfoId",
                value: null);

            migrationBuilder.UpdateData(
                table: "CompanyRoles",
                keyColumn: "CompanyRoleId",
                keyValue: 4,
                column: "PartyInfoId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyRoles_PartyInfoId",
                table: "CompanyRoles",
                column: "PartyInfoId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyRoles_PartyInfos_PartyInfoId",
                table: "CompanyRoles",
                column: "PartyInfoId",
                principalTable: "PartyInfos",
                principalColumn: "PartyInfoId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyRoles_PartyInfos_PartyInfoId",
                table: "CompanyRoles");

            migrationBuilder.DropIndex(
                name: "IX_CompanyRoles_PartyInfoId",
                table: "CompanyRoles");

            migrationBuilder.DropColumn(
                name: "PartyInfoId",
                table: "CompanyRoles");
        }
    }
}
