using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceHeaderRowVersion : Migration
    {
        // Additive: a SQL Server rowversion concurrency column on InvoiceHeaders. The engine fills it
        // for every existing row automatically; no data is touched. Guards against lost updates between
        // the background status sync and concurrent user actions (cancel/edit).
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InvoiceHeaders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InvoiceHeaders");
        }
    }
}
