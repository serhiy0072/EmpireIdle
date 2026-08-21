using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TimerScannerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UnitTrainingOrders_CompletesAt_GarrisonId",
                table: "UnitTrainingOrders",
                columns: new[] { "CompletesAt", "GarrisonId" });

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_ConstructionCompletesAt_VillageId",
                table: "Buildings",
                columns: new[] { "ConstructionCompletesAt", "VillageId" },
                filter: "\"ConstructionCompletesAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnitTrainingOrders_CompletesAt_GarrisonId",
                table: "UnitTrainingOrders");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_ConstructionCompletesAt_VillageId",
                table: "Buildings");
        }
    }
}
