using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBeastPassives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Марші в дорозі йшли без бонусу швидкості: 1.0, а не 0 — інакше зворотна дорога ділилась би на нуль
            migrationBuilder.AddColumn<double>(
                name: "SpeedMultiplier",
                table: "Marches",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "Beasts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActiveUntil",
                table: "Beasts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CooldownUntil",
                table: "Beasts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpeedMultiplier",
                table: "Marches");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "Beasts");

            migrationBuilder.DropColumn(
                name: "ActiveUntil",
                table: "Beasts");

            migrationBuilder.DropColumn(
                name: "CooldownUntil",
                table: "Beasts");
        }
    }
}
