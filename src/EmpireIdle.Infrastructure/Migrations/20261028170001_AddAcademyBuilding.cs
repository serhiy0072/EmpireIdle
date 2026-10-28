using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademyBuilding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Академія стоїть у кожному селі від створення, як і решта будівель; відкривається з ратуші 6
            migrationBuilder.Sql("""
                INSERT INTO "Buildings" ("Id", "Type", "Level", "VillageId", "AccruedAmount", "DamageLevel",
                    "DamagedProductionMultiplier", "HungerProductionMultiplier", "LastCollectedAt", "LastAccruedAt")
                SELECT gen_random_uuid(), 'academy', 1, v."Id", 0, 0, 1.0, 1.0, now(), now()
                FROM "Villages" v
                WHERE NOT EXISTS (SELECT 1 FROM "Buildings" b WHERE b."VillageId" = v."Id" AND b."Type" = 'academy');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "Buildings" WHERE "Type" = 'academy';""");
        }
    }
}
