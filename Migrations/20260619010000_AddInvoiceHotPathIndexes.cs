using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceHotPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RefDocumentNo and InvoiceDirection are nvarchar(max), which SQL Server cannot use as an
            // index key. Bound them first (generous vs the actual value formats), then index the hot
            // lookup/filter columns used by the status sync, search, and invoice-detail pages.
            migrationBuilder.AlterColumn<string>(
                name: "RefDocumentNo",
                table: "InvoiceHeaders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceDirection",
                table: "InvoiceHeaders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_CreatedDate",
                table: "InvoiceHeaders",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_InvoiceDirection",
                table: "InvoiceHeaders",
                column: "InvoiceDirection");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_RefDocumentNo",
                table: "InvoiceHeaders",
                column: "RefDocumentNo");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_UUID",
                table: "InvoiceHeaders",
                column: "UUID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHistories_InvoiceNo",
                table: "InvoiceHistories",
                column: "InvoiceNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_CreatedDate",
                table: "InvoiceHeaders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_InvoiceDirection",
                table: "InvoiceHeaders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_RefDocumentNo",
                table: "InvoiceHeaders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_UUID",
                table: "InvoiceHeaders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceHistories_InvoiceNo",
                table: "InvoiceHistories");

            migrationBuilder.AlterColumn<string>(
                name: "RefDocumentNo",
                table: "InvoiceHeaders",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceDirection",
                table: "InvoiceHeaders",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
