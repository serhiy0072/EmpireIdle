using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Data-міграція до GDD §3.1: функціональні будівлі без рівнів. Схема не змінюється.
    /// Будинок зникає з гри, решта п'ять повертаються на рівень 1 без незавершених
    /// апгрейдів; запити кланової допомоги на такі апгрейди прибираються (внески — каскадом).
    /// </summary>
    public partial class FlattenFunctionalBuildings : Migration
    {
        private const string FunctionalBuildings = "'heroeshall', 'lootshop', 'market', 'scouttower', 'forge'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Допомога на будівництво (TargetType = 0) посилається на будівлю, якої апгрейд уже не буде
            migrationBuilder.Sql($"""
                DELETE FROM "ClanHelpRequests"
                WHERE "TargetType" = 0
                  AND "TargetId" IN (SELECT "Id" FROM "Buildings" WHERE "Type" IN ('house', {FunctionalBuildings}));
                """);

            migrationBuilder.Sql("""DELETE FROM "Buildings" WHERE "Type" = 'house';""");

            migrationBuilder.Sql($"""
                UPDATE "Buildings"
                SET "Level" = 1, "ConstructionCompletesAt" = NULL
                WHERE "Type" IN ({FunctionalBuildings});
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Незворотна: колишні рівні й будинки не зберігались — відкат лишає дані як є
        }
    }
}
