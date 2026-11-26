using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EINVWORLD.Migrations
{
    /// <summary>
    /// Data only, no schema change. PrefixedID holds the ID LHDN knows a document by (the status sync already writes
    /// LHDN's internalId there). Before v1.50.79 the full sync stored an invoice whose LHDN ID collided with an existing
    /// InvoiceNo as "X(1)" in both InvoiceNo and PrefixedID; this puts LHDN's "X" back into PrefixedID for those rows, so
    /// a credit/debit/refund note against them can reference the original's real ID (DR313). Only rows the full sync
    /// created, still with PrefixedID = InvoiceNo, a "(digits)" suffix and an existing "X" row (the collision) are touched.
    /// </summary>
    public partial class BackfillSyncedLhdnInternalId : Migration
    {
        internal const string BackfillSql = @"
UPDATE h SET PrefixedID = s.Stripped
FROM [InvoiceHeaders] h
CROSS APPLY (SELECT LEFT(h.InvoiceNo, LEN(h.InvoiceNo) - CHARINDEX('(', REVERSE(h.InvoiceNo))) AS Stripped,
                    CHARINDEX('(', REVERSE(h.InvoiceNo)) AS Pos) s
WHERE h.ValidationEmailSentTo = N'System (Skipped for Sync)'
  AND h.PrefixedID = h.InvoiceNo
  AND RIGHT(h.InvoiceNo, 1) = N')'
  AND s.Pos > 2
  AND SUBSTRING(h.InvoiceNo, LEN(h.InvoiceNo) - s.Pos + 2, s.Pos - 2) NOT LIKE N'%[^0-9]%'
  AND EXISTS (SELECT 1 FROM [InvoiceHeaders] o WHERE o.InvoiceNo = s.Stripped);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(BackfillSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the old value is still in InvoiceNo, and the status sync writes LHDN's ID here anyway.
        }
    }
}
