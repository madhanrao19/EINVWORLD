using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCompanyAccess",
                table: "UserCompanies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsViewOnly",
                table: "UserCompanies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PublicCustomerId",
                table: "InvoiceTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_PublicCustomerId",
                table: "InvoiceTemplates",
                column: "PublicCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceTemplates_PublicCustomers_PublicCustomerId",
                table: "InvoiceTemplates",
                column: "PublicCustomerId",
                principalTable: "PublicCustomers",
                principalColumn: "PublicCustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceTemplates_PublicCustomers_PublicCustomerId",
                table: "InvoiceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceTemplates_PublicCustomerId",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "HasCompanyAccess",
                table: "UserCompanies");

            migrationBuilder.DropColumn(
                name: "IsViewOnly",
                table: "UserCompanies");

            migrationBuilder.DropColumn(
                name: "PublicCustomerId",
                table: "InvoiceTemplates");
        }
    }
}
