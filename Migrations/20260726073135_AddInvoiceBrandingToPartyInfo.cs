using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceBrandingToPartyInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceAccentColorHex",
                table: "PartyInfos",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceFooterNote",
                table: "PartyInfos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InvoiceShowBankDetails",
                table: "PartyInfos",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceAccentColorHex",
                table: "PartyInfos");

            migrationBuilder.DropColumn(
                name: "InvoiceFooterNote",
                table: "PartyInfos");

            migrationBuilder.DropColumn(
                name: "InvoiceShowBankDetails",
                table: "PartyInfos");
        }
    }
}
