using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class RemovePartyInfoStateCodeForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartyInfos_StateCodes_StateCode",
                table: "PartyInfos");

            migrationBuilder.DropIndex(
                name: "IX_PartyInfos_StateCode",
                table: "PartyInfos");

            migrationBuilder.AlterColumn<string>(
                name: "StateCode",
                table: "PartyInfos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StateCode",
                table: "PartyInfos",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_PartyInfos_StateCode",
                table: "PartyInfos",
                column: "StateCode");

            migrationBuilder.AddForeignKey(
                name: "FK_PartyInfos_StateCodes_StateCode",
                table: "PartyInfos",
                column: "StateCode",
                principalTable: "StateCodes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
