using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Зброя — частина героя, а не предмет (GDD §6.4, рішення 08.10.2026): зброя-предмети й їхні лоти
    /// на ринку видаляються без компенсації — гра в режимі тестів (рішення Сергія 09.10.2026).
    /// Стати й журнал роллів ідуть каскадом. Назад не повертаємо: видалене не відновити.
    /// </summary>
    public partial class RemoveWeaponItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Спершу лоти: вони посилаються на предмет, а категорія ціни зброї зникає разом зі зброєю
            migrationBuilder.Sql(@"DELETE FROM ""MarketListings"" WHERE ""PricingKey"" = 'weapon';");
            migrationBuilder.Sql(@"DELETE FROM ""MarketPriceSnapshots"" WHERE ""PricingKey"" = 'weapon';");
            migrationBuilder.Sql(@"DELETE FROM ""EquipmentItems"" WHERE ""Slot"" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
