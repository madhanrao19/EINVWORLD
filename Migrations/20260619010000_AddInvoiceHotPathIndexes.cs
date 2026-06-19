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
            //
            // Preflight: the old columns were nvarchar(max) and the import/form paths did not enforce a
            // length, so a pre-existing value longer than the new bound would make the ALTER COLUMN fail
            // and block startup (AutoMigrateOnStartup). Truncate any such over-length values first.
            // DATALENGTH (bytes; nvarchar = 2/char) is used so trailing spaces can't slip past the guard.
            // Real reference numbers / directions are far shorter, so only malformed rows are affected.
            migrationBuilder.Sql(
                "UPDATE [InvoiceHeaders] SET [RefDocumentNo] = LEFT([RefDocumentNo], 200) " +
                "WHERE [RefDocumentNo] IS NOT NULL AND DATALENGTH([RefDocumentNo]) > 400;");
            migrationBuilder.Sql(
                "UPDATE [InvoiceHeaders] SET [InvoiceDirection] = LEFT([InvoiceDirection], 50) " +
                "WHERE [InvoiceDirection] IS NOT NULL AND DATALENGTH([InvoiceDirection]) > 100;");

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
