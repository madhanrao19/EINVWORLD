using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class RemovePreFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceTemplateId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    PublicCustomerId = table.Column<int>(type: "int", nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextRunDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AutoSubmitToMyInvois = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringProfiles_InvoiceTemplates_InvoiceTemplateId",
                        column: x => x.InvoiceTemplateId,
                        principalTable: "InvoiceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringRunHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecurringProfileId = table.Column<int>(type: "int", nullable: false),
                    RunTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RunStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GeneratedInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LhdnSubmissionUid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringRunHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringProfiles_InvoiceTemplateId",
                table: "RecurringProfiles",
                column: "InvoiceTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringProfiles");

            migrationBuilder.DropTable(
                name: "RecurringRunHistories");
        }
    }
}
