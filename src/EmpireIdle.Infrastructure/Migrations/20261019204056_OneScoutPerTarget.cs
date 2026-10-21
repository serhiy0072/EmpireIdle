using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OneScoutPerTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Marches_GarrisonId_TargetId",
                table: "Marches",
                columns: new[] { "GarrisonId", "TargetId" },
                unique: true,
                filter: "\"Intent\" = 3 AND \"State\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Marches_GarrisonId_TargetId",
                table: "Marches");
        }
    }
}
