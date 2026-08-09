using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartCaptureAutoSubmit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingAutoSubmitJobId",
                table: "SmartCaptureDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SmartCaptureAutoSubmitSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyPartyInfoId = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowedDocTypesCsv = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MaxAutoSubmitValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DelayMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartCaptureAutoSubmitSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartCaptureAutoSubmitSettings_PartyInfos_CompanyPartyInfoId",
                        column: x => x.CompanyPartyInfoId,
                        principalTable: "PartyInfos",
                        principalColumn: "PartyInfoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmartCaptureAutoSubmitSettings_CompanyPartyInfoId",
                table: "SmartCaptureAutoSubmitSettings",
                column: "CompanyPartyInfoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmartCaptureAutoSubmitSettings");

            migrationBuilder.DropColumn(
                name: "PendingAutoSubmitJobId",
                table: "SmartCaptureDocuments");
        }
    }
}
