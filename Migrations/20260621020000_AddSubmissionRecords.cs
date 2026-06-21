using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eInvWorld.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Local idempotency store: a successful submission keyed by a hash of its payload, so an
            // identical resubmission within the dedup window replays the cached response (mirrors
            // MyInvois' 422 DuplicateSubmission) instead of creating a duplicate document.
            migrationBuilder.CreateTable(
                name: "SubmissionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentCount = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseContent = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionRecords_PayloadHash_SubmittedAtUtc",
                table: "SubmissionRecords",
                columns: new[] { "PayloadHash", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionRecords");
        }
    }
}
