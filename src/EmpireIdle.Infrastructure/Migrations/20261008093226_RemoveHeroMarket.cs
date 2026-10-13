using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Торгівлю героями прибрано (GDD §6.1, рішення 07.10.2026): їх замінили осколки.
    /// Герої з активних лотів повертаються в гарнізон рідного села власника (без лідерства —
    /// місце лідера за час лота могли зайняти), лоти героїв і їхні ціни видаляються.
    /// Податок за виставлення не повертається. Down відновлює лише схему, не дані.
    /// </summary>
    public partial class RemoveHeroMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Стан 4 — колишній OnMarket; такий герой стоїть лише на ринку, тож гарнізон шукаємо за власником
            migrationBuilder.Sql("""
                UPDATE "Heroes" AS h
                SET "State" = 1, "StationedGarrisonId" = g."Id", "IsLeader" = FALSE
                FROM "Villages" AS v
                JOIN "Garrisons" AS g ON g."HostKind" = 1 AND g."HostId" = v."Id"
                WHERE h."State" = 4 AND v."PlayerId" = h."PlayerId";
                """);

            // Kind 2 — колишній Hero
            migrationBuilder.Sql("""DELETE FROM "MarketListings" WHERE "Kind" = 2;""");
            migrationBuilder.Sql("""DELETE FROM "MarketPriceSnapshots" WHERE "PricingKey" LIKE 'hero.%';""");

            migrationBuilder.DropIndex(
                name: "IX_MarketListings_HeroId",
                table: "MarketListings");

            migrationBuilder.DropColumn(
                name: "HeroId",
                table: "MarketListings");

            migrationBuilder.DropColumn(
                name: "ResaleLockedUntil",
                table: "Heroes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HeroId",
                table: "MarketListings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResaleLockedUntil",
                table: "Heroes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_HeroId",
                table: "MarketListings",
                column: "HeroId",
                unique: true,
                filter: "\"HeroId\" IS NOT NULL AND \"State\" = 1");
        }
    }
}
