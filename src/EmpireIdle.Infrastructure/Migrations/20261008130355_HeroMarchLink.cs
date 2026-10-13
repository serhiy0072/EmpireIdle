using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Зв'язок героя з маршем тримає герой (GDD §6.1): Heroes.MarchId замість Marches.HeroId.
    /// Герой у дорозі переходить на новий ключ до того, як старий зникне.
    /// </summary>
    public partial class HeroMarchLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MarchId",
                table: "Heroes",
                type: "uuid",
                nullable: true);

            // Герой у дорозі (без гарнізону) лишається при своєму незавершеному марші
            migrationBuilder.Sql("""
                UPDATE "Heroes" h SET "MarchId" = m."Id"
                FROM "Marches" m
                WHERE m."HeroId" = h."Id" AND m."State" <> 3 AND h."StationedGarrisonId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_MarchId",
                table: "Heroes",
                column: "MarchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Heroes_Marches_MarchId",
                table: "Heroes",
                column: "MarchId",
                principalTable: "Marches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropIndex(
                name: "IX_Marches_HeroId",
                table: "Marches");

            migrationBuilder.DropColumn(
                name: "HeroId",
                table: "Marches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HeroId",
                table: "Marches",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Marches" m SET "HeroId" = h."Id"
                FROM "Heroes" h
                WHERE h."MarchId" = m."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Marches_HeroId",
                table: "Marches",
                column: "HeroId");

            migrationBuilder.DropForeignKey(
                name: "FK_Heroes_Marches_MarchId",
                table: "Heroes");

            migrationBuilder.DropIndex(
                name: "IX_Heroes_MarchId",
                table: "Heroes");

            migrationBuilder.DropColumn(
                name: "MarchId",
                table: "Heroes");
        }
    }
}
