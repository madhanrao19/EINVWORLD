using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmittedDateTimeToInvoiceHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedDateTime",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedDateTime",
                table: "InvoiceHeaders");
        }
    }
}
