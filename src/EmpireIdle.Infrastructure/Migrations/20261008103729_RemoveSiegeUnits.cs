using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Облогових юнітів більше немає (GDD §6.1, рішення 08.10.2026): три ролі героїв — три типи юнітів.
    /// Модель БД не змінилась — видаляються облогові юніти гравців усюди, де вони лежать: гарнізони, марші,
    /// підкріплення, госпіталь, черги тренування й прокачки. Без компенсації (рішення Сергія).
    /// Звіти боїв лишаються як історія. Down даних не повертає.
    /// </summary>
    public partial class RemoveSiegeUnits : Migration
    {
        private static readonly string[] Tables =
        [
            "VillageUnits",
            "MarchUnits",
            "ReinforcementUnits",
            "WoundedUnits",
            "RecoverableUnits",
            "UnitTrainingOrders",
            "UnitLevelUpOrders",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
                migrationBuilder.Sql($"""DELETE FROM "{table}" WHERE "UnitType" = 'siege';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
