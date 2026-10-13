using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCavalryWithSpearmen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Кінноти немає (Сергій, 12.10.2026): Стайня стає Двором списоносців, лучники — зі Стрільбища
            migrationBuilder.Sql(@"UPDATE ""Buildings"" SET ""Type"" = 'spearyard' WHERE ""Type"" = 'stable';");

            // Нова будівля стоїть у кожному селі з першого рівня — як і решта будівель при створенні села
            migrationBuilder.Sql(@"
                INSERT INTO ""Buildings"" (""Id"", ""Type"", ""Level"", ""VillageId"", ""LastCollectedAt"", ""LastAccruedAt"")
                SELECT gen_random_uuid(), 'range', 1, v.""Id"", now(), now()
                FROM ""Villages"" v
                WHERE NOT EXISTS (SELECT 1 FROM ""Buildings"" b WHERE b.""VillageId"" = v.""Id"" AND b.""Type"" = 'range');");

            // Кінноту видаляємо всюди, крім історії боїв: звіти — запис того, що було
            foreach (var table in new[]
                     {
                         "VillageUnits", "MarchUnits", "WoundedUnits", "RecoverableUnits", "ReinforcementUnits",
                         "UnitTrainingOrders", "UnitLevelUpOrders"
                     })
                migrationBuilder.Sql($@"DELETE FROM ""{table}"" WHERE ""UnitType"" = 'cavalry';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Видалену кінноту не повернути; повертаємо лише будівлі
            migrationBuilder.Sql(@"DELETE FROM ""Buildings"" WHERE ""Type"" = 'range';");
            migrationBuilder.Sql(@"UPDATE ""Buildings"" SET ""Type"" = 'stable' WHERE ""Type"" = 'spearyard';");
        }
    }
}
