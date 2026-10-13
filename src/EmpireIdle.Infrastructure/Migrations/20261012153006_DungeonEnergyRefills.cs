using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DungeonEnergyRefills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RefillDay",
                table: "DungeonEnergy",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefillsOnDay",
                table: "DungeonEnergy",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefillDay",
                table: "DungeonEnergy");

            migrationBuilder.DropColumn(
                name: "RefillsOnDay",
                table: "DungeonEnergy");
        }
    }
}
