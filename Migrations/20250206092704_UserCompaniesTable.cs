using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class UserCompaniesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_PartyInfos_PartyInfoId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PartyInfoId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PartyInfoId",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "UserCompanies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartyInfoId = table.Column<int>(type: "int", nullable: false),
                    IsPrimaryCompany = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompanies_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCompanies_PartyInfos_PartyInfoId",
                        column: x => x.PartyInfoId,
                        principalTable: "PartyInfos",
                        principalColumn: "PartyInfoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_PartyInfoId",
                table: "UserCompanies",
                column: "PartyInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_UserId",
                table: "UserCompanies",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCompanies");

            migrationBuilder.AddColumn<int>(
                name: "PartyInfoId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PartyInfoId",
                table: "AspNetUsers",
                column: "PartyInfoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_PartyInfos_PartyInfoId",
                table: "AspNetUsers",
                column: "PartyInfoId",
                principalTable: "PartyInfos",
                principalColumn: "PartyInfoId");
        }
    }
}
