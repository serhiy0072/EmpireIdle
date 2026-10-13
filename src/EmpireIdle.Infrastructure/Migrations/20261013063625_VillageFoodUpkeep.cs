using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VillageFoodUpkeep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FoodUpkeepAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHungry",
                table: "Villages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Наявні будівлі не голодують: повний темп, а не нуль за замовчуванням EF
            migrationBuilder.AddColumn<double>(
                name: "HungerProductionMultiplier",
                table: "Buildings",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FoodUpkeepAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "IsHungry",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "HungerProductionMultiplier",
                table: "Buildings");
        }
    }
}
