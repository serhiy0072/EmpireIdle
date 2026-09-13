using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MarchHero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HeroId",
                table: "Marches",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Marches" m
                SET "HeroId" = h."Id"
                FROM "Heroes" h
                WHERE h."StationedGarrisonId" = m."GarrisonId" AND h."IsLeader";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Marches_HeroId",
                table: "Marches",
                column: "HeroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Marches_HeroId",
                table: "Marches");

            migrationBuilder.DropColumn(
                name: "HeroId",
                table: "Marches");
        }
    }
}
