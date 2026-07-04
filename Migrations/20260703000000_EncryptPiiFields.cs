using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class EncryptPiiFields : Migration
    {
        // Widens the field-level PII columns (bank account numbers + secondary/tertiary address lines) from
        // nvarchar(150) to nvarchar(max) so they can hold DataProtection ciphertext, which is far longer
        // than the 150-char plaintext limit. This is a purely additive widening — no existing data is lost
        // or truncated. The rows themselves are encrypted in place afterwards by the admin-triggered,
        // idempotent backfill (PiiEncryptionBackfillService), NOT by this migration, so applying the
        // migration alone is safe and reversible (existing plaintext keeps reading via the converter's
        // lenient fallback). InvoiceTemplates.BankAccountNo is already nvarchar(max), so it is not altered.
        private static readonly (string Table, string Column)[] Columns =
        {
            ("InvoiceHeaders", "BankAccountNo"),
            ("PartyInfos", "Addr2"),
            ("PartyInfos", "Addr3"),
            ("PartyInfos", "BankAccountNo"),
            ("PublicCustomers", "Addr2"),
            ("PublicCustomers", "Addr3"),
            ("PublicCustomers", "BankAccountNo"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in Columns)
            {
                migrationBuilder.AlterColumn<string>(
                    name: column,
                    table: table,
                    type: "nvarchar(max)",
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "nvarchar(150)",
                    oldMaxLength: 150,
                    oldNullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting narrows the columns back to nvarchar(150). Run this only after decrypting the rows
            // back to plaintext (ciphertext exceeds 150 chars and would be truncated) — take a backup first.
            foreach (var (table, column) in Columns)
            {
                migrationBuilder.AlterColumn<string>(
                    name: column,
                    table: table,
                    type: "nvarchar(150)",
                    maxLength: 150,
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "nvarchar(max)",
                    oldNullable: true);
            }
        }
    }
}
