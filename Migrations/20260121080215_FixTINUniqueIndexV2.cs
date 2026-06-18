using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class FixTINUniqueIndexV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartyInfos_TIN",
                table: "PartyInfos");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInfos_TIN",
                table: "PartyInfos",
                column: "TIN",
                unique: true,
                filter: "[TIN] <> 'EI00000000010' AND [TIN] <> 'EI00000000020' AND [TIN] <> 'EI00000000030' AND [TIN] <> 'EI00000000040'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartyInfos_TIN",
                table: "PartyInfos");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInfos_TIN",
                table: "PartyInfos",
                column: "TIN",
                unique: true);
        }
    }
}
