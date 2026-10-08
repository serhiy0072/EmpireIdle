using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Вміння героїв із перезарядкою замість шкали енергії (GDD §6.1, рішення 08.10.2026).
    /// Модель БД не змінилась — змінився вміст збереженого бою: унікальний стат артефакту
    /// EnergyOnAttack тепер CooldownReduction. Без перейменування ключа старий бій не прочитався б
    /// (невідоме значення enum), і незавершений забіг завис би. Поле energy читач просто ігнорує,
    /// а вміння в таких боях порожні — герої добивають забіг звичайними ударами.
    /// </summary>
    public partial class DungeonSkillCooldowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "DungeonRuns"
                SET "Battle" = replace("Battle"::text, '"EnergyOnAttack"', '"CooldownReduction"')::jsonb
                WHERE "Battle"::text LIKE '%"EnergyOnAttack"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "DungeonRuns"
                SET "Battle" = replace("Battle"::text, '"CooldownReduction"', '"EnergyOnAttack"')::jsonb
                WHERE "Battle"::text LIKE '%"CooldownReduction"%';
                """);
        }
    }
}
