using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyInfoToInvoiceTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SupplierId",
                table: "InvoiceTemplates",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "InvoiceTemplates",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_CustomerId",
                table: "InvoiceTemplates",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_SupplierId",
                table: "InvoiceTemplates",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceTemplates_PartyInfos_CustomerId",
                table: "InvoiceTemplates",
                column: "CustomerId",
                principalTable: "PartyInfos",
                principalColumn: "PartyInfoId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceTemplates_PartyInfos_SupplierId",
                table: "InvoiceTemplates",
                column: "SupplierId",
                principalTable: "PartyInfos",
                principalColumn: "PartyInfoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceTemplates_PartyInfos_CustomerId",
                table: "InvoiceTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceTemplates_PartyInfos_SupplierId",
                table: "InvoiceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceTemplates_CustomerId",
                table: "InvoiceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceTemplates_SupplierId",
                table: "InvoiceTemplates");

            migrationBuilder.AlterColumn<string>(
                name: "SupplierId",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CustomerId",
                table: "InvoiceTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
