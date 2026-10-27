using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VillageScannerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Villages_ServerId_AutoCollectedAt",
                table: "Villages",
                columns: new[] { "ServerId", "AutoCollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Villages_ServerId_FoodUpkeepAt",
                table: "Villages",
                columns: new[] { "ServerId", "FoodUpkeepAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Villages_ServerId_AutoCollectedAt",
                table: "Villages");

            migrationBuilder.DropIndex(
                name: "IX_Villages_ServerId_FoodUpkeepAt",
                table: "Villages");
        }
    }
}
