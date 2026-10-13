using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMonsterExperienceLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonsterRewardsDay",
                table: "HeroExperience");

            migrationBuilder.DropColumn(
                name: "MonsterRewardsToday",
                table: "HeroExperience");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "MonsterRewardsDay",
                table: "HeroExperience",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonsterRewardsToday",
                table: "HeroExperience",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
