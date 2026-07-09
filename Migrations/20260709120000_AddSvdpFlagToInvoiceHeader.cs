using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddSvdpFlagToInvoiceHeader : Migration
    {
        // Additive: a bit column marking an invoice as an e-Invoice Special Voluntary Disclosure
        // Programme (SVDP) submission (LHDN SDK 8 Jul 2026; programme valid until 31 Dec 2027).
        // Existing rows default to 0 (normal v1.0 invoices); no data is touched.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSvdp",
                table: "InvoiceHeaders",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSvdp",
                table: "InvoiceHeaders");
        }
    }
}
