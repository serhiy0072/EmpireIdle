using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Provisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BeastsHungry",
                table: "Villages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Їдальня стоїть у кожному селі, як і решта будівель від створення; відкривається з ратуші 4
            migrationBuilder.Sql("""
                INSERT INTO "Buildings" ("Id", "Type", "Level", "VillageId", "LastCollectedAt", "LastAccruedAt")
                SELECT gen_random_uuid(), 'messhall', 1, v."Id", now(), now()
                FROM "Villages" v
                WHERE NOT EXISTS (SELECT 1 FROM "Buildings" b WHERE b."VillageId" = v."Id" AND b."Type" = 'messhall');
                """);

            // Звірі тепер їдять корм, а його ще ніхто не виробив: разовий запас, щоб звіринець не зголоднів
            // у першу ж оплату після оновлення (рішення режисера 20.10.2026)
            migrationBuilder.Sql("""
                UPDATE "VillageResources" r
                SET "Amount" = r."Amount" + 3000
                FROM "Villages" v
                WHERE r."VillageId" = v."Id" AND r."ResourceType" = 'fodder'
                  AND EXISTS (SELECT 1 FROM "BeastPens" p JOIN "Beasts" b ON b."BeastPenId" = p."Id" WHERE p."PlayerId" = v."PlayerId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "Buildings" WHERE "Type" = 'messhall';""");

            migrationBuilder.DropColumn(
                name: "BeastsHungry",
                table: "Villages");
        }
    }
}
