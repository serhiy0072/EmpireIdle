using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueBuildingTypePerVillage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Buildings_VillageId",
                table: "Buildings");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_VillageId_Type",
                table: "Buildings",
                columns: new[] { "VillageId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Buildings_VillageId_Type",
                table: "Buildings");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_VillageId",
                table: "Buildings",
                column: "VillageId");
        }
    }
}
