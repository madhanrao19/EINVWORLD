using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTemplateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvoicePeriod = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceTemplateLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceTemplateId = table.Column<int>(type: "int", nullable: false),
                    ClassificationCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceTemplateLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceTemplateLines_InvoiceTemplates_InvoiceTemplateId",
                        column: x => x.InvoiceTemplateId,
                        principalTable: "InvoiceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceTemplateTaxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceTemplateLineId = table.Column<int>(type: "int", nullable: false),
                    TaxCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxExemptionReason = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceTemplateTaxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceTemplateTaxes_InvoiceTemplateLines_InvoiceTemplateLineId",
                        column: x => x.InvoiceTemplateLineId,
                        principalTable: "InvoiceTemplateLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplateLines_InvoiceTemplateId",
                table: "InvoiceTemplateLines",
                column: "InvoiceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplateTaxes_InvoiceTemplateLineId",
                table: "InvoiceTemplateTaxes",
                column: "InvoiceTemplateLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceTemplateTaxes");

            migrationBuilder.DropTable(
                name: "InvoiceTemplateLines");

            migrationBuilder.DropTable(
                name: "InvoiceTemplates");
        }
    }
}
