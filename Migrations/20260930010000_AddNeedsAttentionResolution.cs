using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <summary>
    /// Lets Invalid / reject-requested invoices leave "Needs Attention": a manual resolve marker
    /// (AttentionResolvedAt/By) and a Resend link (ResentFromInvoiceNo) so a Valid copy clears its
    /// original automatically. Additive only; existing rows get NULL and behave as before.
    /// </summary>
    public partial class AddNeedsAttentionResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AttentionResolvedAt",
                table: "InvoiceHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttentionResolvedBy",
                table: "InvoiceHeaders",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResentFromInvoiceNo",
                table: "InvoiceHeaders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_ResentFromInvoiceNo",
                table: "InvoiceHeaders",
                column: "ResentFromInvoiceNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_ResentFromInvoiceNo",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(name: "AttentionResolvedAt", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "AttentionResolvedBy", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ResentFromInvoiceNo", table: "InvoiceHeaders");
        }
    }
}
