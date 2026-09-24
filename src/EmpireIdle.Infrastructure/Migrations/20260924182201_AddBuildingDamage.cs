using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingDamage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VillageFalls_AttackerPlayerId_OccurredAt",
                table: "VillageFalls");

            migrationBuilder.AddColumn<int>(
                name: "DefeatStreak",
                table: "Villages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DamageLevel",
                table: "Buildings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "DamagedProductionMultiplier",
                table: "Buildings",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DamagedUntil",
                table: "Buildings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefeatStreak",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "DamageLevel",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "DamagedProductionMultiplier",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "DamagedUntil",
                table: "Buildings");

            migrationBuilder.CreateIndex(
                name: "IX_VillageFalls_AttackerPlayerId_OccurredAt",
                table: "VillageFalls",
                columns: new[] { "AttackerPlayerId", "OccurredAt" });
        }
    }
}
