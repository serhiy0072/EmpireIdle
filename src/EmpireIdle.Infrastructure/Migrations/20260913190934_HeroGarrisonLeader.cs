using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HeroGarrisonLeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLeader",
                table: "Heroes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StationedGarrisonId",
                table: "Heroes",
                type: "uuid",
                nullable: true);

            // Наявні герої оселяються в гарнізоні свого власника. Без цього
            // вони лишились би «в дорозі» назавжди й не боронили б нікого.
            migrationBuilder.Sql("""
                UPDATE "Heroes" h
                SET "StationedGarrisonId" = g."Id"
                FROM "Villages" v
                JOIN "Garrisons" g ON g."VillageId" = v."Id"
                WHERE v."PlayerId" = h."PlayerId"
                  AND h."State" <> 1;
                """);

            // Лідером стає найраніше отриманий: слот один, і вибір мусить
            // бути детермінованим, інакше індекс відкине решту рядків
            migrationBuilder.Sql("""
                UPDATE "Heroes" h
                SET "IsLeader" = TRUE
                WHERE h."Id" = (
                    SELECT x."Id" FROM "Heroes" x
                    WHERE x."PlayerId" = h."PlayerId"
                      AND x."StationedGarrisonId" = h."StationedGarrisonId"
                    ORDER BY x."AcquiredAt", x."Id"
                    LIMIT 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_StationedGarrisonId",
                table: "Heroes",
                column: "StationedGarrisonId");

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_StationedGarrisonId_PlayerId",
                table: "Heroes",
                columns: new[] { "StationedGarrisonId", "PlayerId" },
                unique: true,
                filter: "\"IsLeader\" AND \"StationedGarrisonId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Heroes_StationedGarrisonId",
                table: "Heroes");

            migrationBuilder.DropIndex(
                name: "IX_Heroes_StationedGarrisonId_PlayerId",
                table: "Heroes");

            migrationBuilder.DropColumn(
                name: "IsLeader",
                table: "Heroes");

            migrationBuilder.DropColumn(
                name: "StationedGarrisonId",
                table: "Heroes");
        }
    }
}
