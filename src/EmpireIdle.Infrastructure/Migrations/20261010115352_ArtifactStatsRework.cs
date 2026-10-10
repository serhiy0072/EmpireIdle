using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Стати артефакта переходять на пласку базу й бонуси заточки (GDD §9.12). Старі рольовані стати
    /// й журнал роллів у новій моделі сенсу не мають — видаляються; заточка обнуляється без компенсації
    /// (гра в тестах, рішення Сергія 10.10.2026). Вкладений досвід лишається, а рівень перераховується
    /// за новою кривою round10(100 + 2.2 · (n − 1)^1.55) до стелі 80. Down повертає лише схему:
    /// видалені стати не відновлюються.
    /// </summary>
    public partial class ArtifactStatsRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Level",
                table: "EquipmentRolls",
                newName: "Mastery");

            migrationBuilder.RenameIndex(
                name: "IX_EquipmentRolls_EquipmentItemId_Level",
                table: "EquipmentRolls",
                newName: "IX_EquipmentRolls_EquipmentItemId_Mastery");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "EquipmentStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                DELETE FROM "EquipmentStats";
                DELETE FROM "EquipmentRolls";

                UPDATE "EquipmentItems" AS e SET
                    "Mastery" = 0,
                    "Level" = (
                        SELECT COUNT(*)
                        FROM (
                            SELECT SUM(ROUND((100 + 2.2 * power(n - 1, 1.55)) / 10) * 10) OVER (ORDER BY n) AS reach
                            FROM generate_series(1, 80) AS n) AS curve
                        WHERE curve.reach <= e."Experience");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Position",
                table: "EquipmentStats");

            migrationBuilder.RenameColumn(
                name: "Mastery",
                table: "EquipmentRolls",
                newName: "Level");

            migrationBuilder.RenameIndex(
                name: "IX_EquipmentRolls_EquipmentItemId_Mastery",
                table: "EquipmentRolls",
                newName: "IX_EquipmentRolls_EquipmentItemId_Level");
        }
    }
}
