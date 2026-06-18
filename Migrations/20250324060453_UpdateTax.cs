using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StateCode",
                table: "PartyInfos",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "PartyInfos",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "TaxExemptionReason",
                table: "InvoiceTaxes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContactUs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Company = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactUs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartyInfos_CountryCode",
                table: "PartyInfos",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInfos_StateCode",
                table: "PartyInfos",
                column: "StateCode");

            migrationBuilder.AddForeignKey(
                name: "FK_PartyInfos_CountryCodes_CountryCode",
                table: "PartyInfos",
                column: "CountryCode",
                principalTable: "CountryCodes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PartyInfos_StateCodes_StateCode",
                table: "PartyInfos",
                column: "StateCode",
                principalTable: "StateCodes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartyInfos_CountryCodes_CountryCode",
                table: "PartyInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_PartyInfos_StateCodes_StateCode",
                table: "PartyInfos");

            migrationBuilder.DropTable(
                name: "ContactUs");

            migrationBuilder.DropIndex(
                name: "IX_PartyInfos_CountryCode",
                table: "PartyInfos");

            migrationBuilder.DropIndex(
                name: "IX_PartyInfos_StateCode",
                table: "PartyInfos");

            migrationBuilder.DropColumn(
                name: "TaxExemptionReason",
                table: "InvoiceTaxes");

            migrationBuilder.AlterColumn<string>(
                name: "StateCode",
                table: "PartyInfos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "PartyInfos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
