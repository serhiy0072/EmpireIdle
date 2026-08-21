using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartialIndexOnStaleReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords",
                column: "CreatedAt",
                filter: "\"ResponseJson\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords",
                column: "CreatedAt");
        }
    }
}
