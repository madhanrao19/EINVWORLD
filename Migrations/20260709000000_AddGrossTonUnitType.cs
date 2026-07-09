using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddGrossTonUnitType : Migration
    {
        // LHDN SDK update (28 Dec 2024) added unit code "GT" (gross ton) to the Unit of
        // Measurement code table. Data-only, additive and idempotent: inserts the row only
        // when it is absent, so databases where an admin already added GT are untouched.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [UnitTypes] WHERE [Code] = N'GT')
BEGIN
    INSERT INTO [UnitTypes] ([Code], [Name], [IsActive], [UpdatedBy], [UpdatedDate])
    VALUES (N'GT', N'gross ton', 1, N'system', GETUTCDATE());
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes only the row this migration seeded (admin-entered rows keep their own UpdatedBy).
            migrationBuilder.Sql("DELETE FROM [UnitTypes] WHERE [Code] = N'GT' AND [UpdatedBy] = N'system';");
        }
    }
}
