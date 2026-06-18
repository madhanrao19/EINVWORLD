using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicCustomerTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PublicCustomerId",
                table: "UserCompanies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublicCustomerId",
                table: "SupplierBuyers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublicCustomerId1",
                table: "SupplierBuyers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PublicCustomers",
                columns: table => new
                {
                    PublicCustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IndustryClassificationCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BizDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TIN = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    RegTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RegNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldRegNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SST = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    TTX = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: true),
                    Addr1 = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Addr2 = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Addr3 = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StateCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    PhoneNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FaxNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BankAccountNo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Attention = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AuthorisationNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InviteCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsAdminCreated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCustomers", x => x.PublicCustomerId);
                    table.ForeignKey(
                        name: "FK_PublicCustomers_CountryCodes_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "CountryCodes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicCustomers_RegistrationTypes_RegTypeCode",
                        column: x => x.RegTypeCode,
                        principalTable: "RegistrationTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicCustomers_StateCodes_StateCode",
                        column: x => x.StateCode,
                        principalTable: "StateCodes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_PublicCustomerId",
                table: "UserCompanies",
                column: "PublicCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBuyers_PublicCustomerId",
                table: "SupplierBuyers",
                column: "PublicCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBuyers_PublicCustomerId1",
                table: "SupplierBuyers",
                column: "PublicCustomerId1");

            migrationBuilder.CreateIndex(
                name: "IX_PublicCustomers_CountryCode",
                table: "PublicCustomers",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_PublicCustomers_RegTypeCode",
                table: "PublicCustomers",
                column: "RegTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_PublicCustomers_StateCode",
                table: "PublicCustomers",
                column: "StateCode");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierBuyers_PublicCustomers_PublicCustomerId",
                table: "SupplierBuyers",
                column: "PublicCustomerId",
                principalTable: "PublicCustomers",
                principalColumn: "PublicCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierBuyers_PublicCustomers_PublicCustomerId1",
                table: "SupplierBuyers",
                column: "PublicCustomerId1",
                principalTable: "PublicCustomers",
                principalColumn: "PublicCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCompanies_PublicCustomers_PublicCustomerId",
                table: "UserCompanies",
                column: "PublicCustomerId",
                principalTable: "PublicCustomers",
                principalColumn: "PublicCustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierBuyers_PublicCustomers_PublicCustomerId",
                table: "SupplierBuyers");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierBuyers_PublicCustomers_PublicCustomerId1",
                table: "SupplierBuyers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCompanies_PublicCustomers_PublicCustomerId",
                table: "UserCompanies");

            migrationBuilder.DropTable(
                name: "PublicCustomers");

            migrationBuilder.DropIndex(
                name: "IX_UserCompanies_PublicCustomerId",
                table: "UserCompanies");

            migrationBuilder.DropIndex(
                name: "IX_SupplierBuyers_PublicCustomerId",
                table: "SupplierBuyers");

            migrationBuilder.DropIndex(
                name: "IX_SupplierBuyers_PublicCustomerId1",
                table: "SupplierBuyers");

            migrationBuilder.DropColumn(
                name: "PublicCustomerId",
                table: "UserCompanies");

            migrationBuilder.DropColumn(
                name: "PublicCustomerId",
                table: "SupplierBuyers");

            migrationBuilder.DropColumn(
                name: "PublicCustomerId1",
                table: "SupplierBuyers");
        }
    }
}
