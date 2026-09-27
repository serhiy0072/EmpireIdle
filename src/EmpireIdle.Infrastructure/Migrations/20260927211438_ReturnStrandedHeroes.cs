using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReturnStrandedHeroes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // К1: герої, що застрягли поза гарнізоном до виправлення TurnMarchBackAsync
            migrationBuilder.Sql("""
                UPDATE "Heroes" AS h
                SET "StationedGarrisonId" = g."Id",
                    "IsLeader" = FALSE,
                    "State" = CASE WHEN h."State" = 2 THEN 1 ELSE h."State" END,
                    "UpdatedAt" = now() AT TIME ZONE 'utc'
                FROM "Villages" AS v
                JOIN "Garrisons" AS g ON g."HostId" = v."Id" AND g."HostKind" = 1
                WHERE v."PlayerId" = h."PlayerId"
                  AND h."StationedGarrisonId" IS NULL
                  AND h."State" IN (2, 3)
                  AND NOT EXISTS (SELECT 1 FROM "Marches" AS m WHERE m."HeroId" = h."Id" AND m."State" <> 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
