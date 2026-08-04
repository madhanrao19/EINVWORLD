using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartCaptureDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmartCaptureDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyPartyInfoId = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    InternalStorageReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NormalizedExtractionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallConfidence = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UsedOcr = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedDocTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RelatedInvoiceHeaderInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    UserSafeFailureMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FileDeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartCaptureDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartCaptureDocuments_PartyInfos_CompanyPartyInfoId",
                        column: x => x.CompanyPartyInfoId,
                        principalTable: "PartyInfos",
                        principalColumn: "PartyInfoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmartCaptureDocuments_CompanyPartyInfoId",
                table: "SmartCaptureDocuments",
                column: "CompanyPartyInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartCaptureDocuments_FileDeletedAtUtc",
                table: "SmartCaptureDocuments",
                column: "FileDeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SmartCaptureDocuments_FileHash",
                table: "SmartCaptureDocuments",
                column: "FileHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmartCaptureDocuments");
        }
    }
}
