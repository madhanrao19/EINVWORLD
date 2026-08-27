using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class RemovePublicCustomerStateCodeForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PublicCustomers_StateCodes_StateCode",
                table: "PublicCustomers");

            migrationBuilder.DropIndex(
                name: "IX_PublicCustomers_StateCode",
                table: "PublicCustomers");

            migrationBuilder.AlterColumn<string>(
                name: "StateCode",
                table: "PublicCustomers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StateCode",
                table: "PublicCustomers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_PublicCustomers_StateCode",
                table: "PublicCustomers",
                column: "StateCode");

            migrationBuilder.AddForeignKey(
                name: "FK_PublicCustomers_StateCodes_StateCode",
                table: "PublicCustomers",
                column: "StateCode",
                principalTable: "StateCodes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
