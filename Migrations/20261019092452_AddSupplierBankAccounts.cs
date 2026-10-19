using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <summary>
    /// Lets a company hold several bank accounts with one default (SupplierBankAccounts). Additive: creates
    /// the table and copies each company's existing bank details in as its default account; PartyInfos is
    /// not changed.
    /// </summary>
    public partial class AddSupplierBankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierBankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyInfoId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BankAccountNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierBankAccounts_PartyInfos_PartyInfoId",
                        column: x => x.PartyInfoId,
                        principalTable: "PartyInfos",
                        principalColumn: "PartyInfoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBankAccounts_PartyInfoId",
                table: "SupplierBankAccounts",
                column: "PartyInfoId");

            // Each company's existing bank details become its default account. BankAccountNo is copied as
            // stored — ciphertext (all PII columns share one protector purpose) or legacy plaintext that the
            // PII backfill encrypts later. PartyInfos keeps its copy as the mirror of the default.
            migrationBuilder.Sql(@"
INSERT INTO [SupplierBankAccounts] ([PartyInfoId], [BankName], [BankAccountNo], [IsDefault], [CreatedDate], [CreatedBy])
SELECT p.[PartyInfoId], LEFT(ISNULL(LTRIM(RTRIM(p.[BankName])), N''), 100), p.[BankAccountNo], 1, GETDATE(), N'migration'
FROM [PartyInfos] p
WHERE p.[BankAccountNo] IS NOT NULL AND LTRIM(RTRIM(p.[BankAccountNo])) <> N''
  AND NOT EXISTS (SELECT 1 FROM [SupplierBankAccounts] a WHERE a.[PartyInfoId] = p.[PartyInfoId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierBankAccounts");
        }
    }
}
