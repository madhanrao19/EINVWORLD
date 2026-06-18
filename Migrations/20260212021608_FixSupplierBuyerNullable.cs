using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class FixSupplierBuyerNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierBuyers_PublicCustomers_PublicCustomerId1",
                table: "SupplierBuyers");

            migrationBuilder.DropIndex(
                name: "IX_SupplierBuyers_PublicCustomerId1",
                table: "SupplierBuyers");

            migrationBuilder.DropColumn(
                name: "PublicCustomerId1",
                table: "SupplierBuyers");

            migrationBuilder.AddColumn<int>(
                name: "PublicCustomerId",
                table: "InvoiceHeaders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_PublicCustomerId",
                table: "InvoiceHeaders",
                column: "PublicCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceHeaders_PublicCustomers_PublicCustomerId",
                table: "InvoiceHeaders",
                column: "PublicCustomerId",
                principalTable: "PublicCustomers",
                principalColumn: "PublicCustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceHeaders_PublicCustomers_PublicCustomerId",
                table: "InvoiceHeaders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_PublicCustomerId",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(
                name: "PublicCustomerId",
                table: "InvoiceHeaders");

            migrationBuilder.AddColumn<int>(
                name: "PublicCustomerId1",
                table: "SupplierBuyers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBuyers_PublicCustomerId1",
                table: "SupplierBuyers",
                column: "PublicCustomerId1");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierBuyers_PublicCustomers_PublicCustomerId1",
                table: "SupplierBuyers",
                column: "PublicCustomerId1",
                principalTable: "PublicCustomers",
                principalColumn: "PublicCustomerId");
        }
    }
}
