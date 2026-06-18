using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class FixBuyerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop the old Primary Key (Composite: SupplierId + BuyerId)
            migrationBuilder.DropPrimaryKey(
                name: "PK_SupplierBuyers",
                table: "SupplierBuyers");

            // 2. Drop the old 'Id' column (Since we can't alter it to be Identity)
            migrationBuilder.DropColumn(
                name: "Id",
                table: "SupplierBuyers");

            // 3. Create the new 'Id' column with Identity (Auto-Increment)
            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "SupplierBuyers",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            // 4. Make BuyerId nullable (The original intent of this migration)
            migrationBuilder.AlterColumn<int>(
                name: "BuyerId",
                table: "SupplierBuyers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // 5. Add the new Primary Key using the new 'Id' column
            migrationBuilder.AddPrimaryKey(
                name: "PK_SupplierBuyers",
                table: "SupplierBuyers",
                column: "Id");

            // 6. Re-add the index for SupplierId (Good for performance)
            migrationBuilder.CreateIndex(
                name: "IX_SupplierBuyers_SupplierId",
                table: "SupplierBuyers",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SupplierBuyers",
                table: "SupplierBuyers");

            migrationBuilder.DropIndex(
                name: "IX_SupplierBuyers_SupplierId",
                table: "SupplierBuyers");

            migrationBuilder.AlterColumn<int>(
                name: "BuyerId",
                table: "SupplierBuyers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "SupplierBuyers",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SupplierBuyers",
                table: "SupplierBuyers",
                columns: new[] { "SupplierId", "BuyerId" });
        }
    }
}
