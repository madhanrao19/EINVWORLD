using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <inheritdoc />
    public partial class AddLineTariffOriginAndHeaderShippingCustoms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductTariffCode",
                table: "InvoiceLines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryOfOrigin",
                table: "InvoiceLines",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "InvoiceLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeChargeAmount",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeChargeReason",
                table: "InvoiceLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientName",
                table: "InvoiceHeaders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientAddrLine1",
                table: "InvoiceHeaders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientAddrLine2",
                table: "InvoiceHeaders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientAddrLine3",
                table: "InvoiceHeaders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientPostcode",
                table: "InvoiceHeaders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientCity",
                table: "InvoiceHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientState",
                table: "InvoiceHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientCountryCode",
                table: "InvoiceHeaders",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientIdType",
                table: "InvoiceHeaders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientIdNumber",
                table: "InvoiceHeaders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingRecipientTIN",
                table: "InvoiceHeaders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomsFormNo1Reference",
                table: "InvoiceHeaders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeTradeAgreementInfo",
                table: "InvoiceHeaders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertifiedExporterAuthorizationNumber",
                table: "InvoiceHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomsFormNo2Reference",
                table: "InvoiceHeaders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherChargesAmount",
                table: "InvoiceHeaders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherChargesDescription",
                table: "InvoiceHeaders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CountryOfOrigin",
                table: "InvoiceLines",
                column: "CountryOfOrigin");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_ShippingRecipientCountryCode",
                table: "InvoiceHeaders",
                column: "ShippingRecipientCountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHeaders_ShippingRecipientIdType",
                table: "InvoiceHeaders",
                column: "ShippingRecipientIdType");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLines_CountryCodes_CountryOfOrigin",
                table: "InvoiceLines",
                column: "CountryOfOrigin",
                principalTable: "CountryCodes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceHeaders_CountryCodes_ShippingRecipientCountryCode",
                table: "InvoiceHeaders",
                column: "ShippingRecipientCountryCode",
                principalTable: "CountryCodes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceHeaders_RegistrationTypes_ShippingRecipientIdType",
                table: "InvoiceHeaders",
                column: "ShippingRecipientIdType",
                principalTable: "RegistrationTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLines_CountryCodes_CountryOfOrigin",
                table: "InvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceHeaders_CountryCodes_ShippingRecipientCountryCode",
                table: "InvoiceHeaders");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceHeaders_RegistrationTypes_ShippingRecipientIdType",
                table: "InvoiceHeaders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_CountryOfOrigin",
                table: "InvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_ShippingRecipientCountryCode",
                table: "InvoiceHeaders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceHeaders_ShippingRecipientIdType",
                table: "InvoiceHeaders");

            migrationBuilder.DropColumn(name: "ProductTariffCode", table: "InvoiceLines");
            migrationBuilder.DropColumn(name: "CountryOfOrigin", table: "InvoiceLines");
            migrationBuilder.DropColumn(name: "DiscountReason", table: "InvoiceLines");
            migrationBuilder.DropColumn(name: "FeeChargeAmount", table: "InvoiceLines");
            migrationBuilder.DropColumn(name: "FeeChargeReason", table: "InvoiceLines");

            migrationBuilder.DropColumn(name: "ShippingRecipientName", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientAddrLine1", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientAddrLine2", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientAddrLine3", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientPostcode", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientCity", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientState", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientCountryCode", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientIdType", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientIdNumber", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "ShippingRecipientTIN", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "CustomsFormNo1Reference", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "FreeTradeAgreementInfo", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "CertifiedExporterAuthorizationNumber", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "CustomsFormNo2Reference", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "OtherChargesAmount", table: "InvoiceHeaders");
            migrationBuilder.DropColumn(name: "OtherChargesDescription", table: "InvoiceHeaders");
        }
    }
}
