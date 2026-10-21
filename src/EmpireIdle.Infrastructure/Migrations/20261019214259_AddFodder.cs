using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFodder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Корм — новий ресурс звірів (рішення 20.10.2026). Рядок ресурсу є в кожного села з дня створення,
            // тож наявним селам додаємо порожній — інакше ціна в кормі не знайшла б, звідки списати
            migrationBuilder.Sql("""
                INSERT INTO "VillageResources" ("VillageId", "ResourceType", "Amount")
                SELECT v."Id", 'fodder', 0
                FROM "Villages" v
                WHERE NOT EXISTS (SELECT 1 FROM "VillageResources" r WHERE r."VillageId" = v."Id" AND r."ResourceType" = 'fodder');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "VillageResources" WHERE "ResourceType" = 'fodder';""");
        }
    }
}
