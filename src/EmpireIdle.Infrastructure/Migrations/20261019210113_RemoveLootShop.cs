using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLootShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Торгову лавку злито з ринком (рішення 19.10.2026): банери, крамниця й ринок гравців —
            // вкладки однієї будівлі. Лавка функціональна, без рівнів, тож нічого не втрачається
            migrationBuilder.Sql("""DELETE FROM "Buildings" WHERE "Type" = 'lootshop';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Без зворотного кроку: лавки вже немає в конфізі, повертати рядки нема куди
        }
    }
}
