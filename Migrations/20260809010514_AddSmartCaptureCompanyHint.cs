using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartCaptureCompanyHint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmartCaptureCompanyHints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyPartyInfoId = table.Column<int>(type: "int", nullable: false),
                    MostCommonDocTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DocTypeVotes = table.Column<int>(type: "int", nullable: false),
                    MostCommonCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CurrencyVotes = table.Column<int>(type: "int", nullable: false),
                    MostCommonTaxType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TaxTypeVotes = table.Column<int>(type: "int", nullable: false),
                    MostCommonTaxRatePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxRateVotes = table.Column<int>(type: "int", nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartCaptureCompanyHints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartCaptureCompanyHints_PartyInfos_CompanyPartyInfoId",
                        column: x => x.CompanyPartyInfoId,
                        principalTable: "PartyInfos",
                        principalColumn: "PartyInfoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmartCaptureCompanyHints_CompanyPartyInfoId",
                table: "SmartCaptureCompanyHints",
                column: "CompanyPartyInfoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmartCaptureCompanyHints");
        }
    }
}
