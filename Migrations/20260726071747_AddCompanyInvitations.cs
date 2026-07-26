using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyInvitations",
                columns: table => new
                {
                    CompanyInvitationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyInfoId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyRoleId = table.Column<int>(type: "int", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyInvitations", x => x.CompanyInvitationId);
                    table.ForeignKey(
                        name: "FK_CompanyInvitations_CompanyRoles_CompanyRoleId",
                        column: x => x.CompanyRoleId,
                        principalTable: "CompanyRoles",
                        principalColumn: "CompanyRoleId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompanyInvitations_PartyInfos_PartyInfoId",
                        column: x => x.PartyInfoId,
                        principalTable: "PartyInfos",
                        principalColumn: "PartyInfoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_CompanyRoleId",
                table: "CompanyInvitations",
                column: "CompanyRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_PartyInfoId",
                table: "CompanyInvitations",
                column: "PartyInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_TokenHash",
                table: "CompanyInvitations",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyInvitations");
        }
    }
}
