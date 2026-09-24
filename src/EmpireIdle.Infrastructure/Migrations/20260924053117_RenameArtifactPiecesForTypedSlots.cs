using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Артефакти отримали слоти за типом (намисто, корона, кільце, пояс),
    /// а частини наборів — ключі своїх слотів. Модель не змінилась, тож
    /// міграція лише переносить дані: без неї видані раніше артефакти
    /// лишились би з ключами, яких у конфігу вже немає.
    /// </summary>
    public partial class RenameArtifactPiecesForTypedSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Спершу знімаємо всі вдягнені артефакти: у вільних слотах герой міг
            // носити два кільця, а слоти за типом такого не вміщують
            migrationBuilder.Sql("""
                UPDATE "EquipmentItems"
                SET "EquippedByHeroId" = NULL, "SlotIndex" = 0, "UpdatedAt" = now()
                WHERE "Slot" = 2 AND "EquippedByHeroId" IS NOT NULL;
                """);

            // Частини наборів отримують ключі своїх слотів; кільце лишається кільцем
            migrationBuilder.Sql("""
                UPDATE "EquipmentItems"
                SET "ItemKey" = regexp_replace("ItemKey", '_amulet_(common|rare|unique)$', '_necklace_\1')
                WHERE "Slot" = 2 AND "ItemKey" ~ '_amulet_(common|rare|unique)$';

                UPDATE "EquipmentItems"
                SET "ItemKey" = regexp_replace("ItemKey", '_sigil_(common|rare|unique)$', '_crown_\1')
                WHERE "Slot" = 2 AND "ItemKey" ~ '_sigil_(common|rare|unique)$';

                UPDATE "EquipmentItems"
                SET "ItemKey" = regexp_replace("ItemKey", '_chime_(common|rare|unique)$', '_belt_\1')
                WHERE "Slot" = 2 AND "ItemKey" ~ '_chime_(common|rare|unique)$';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ключі повертаються; зняті артефакти не вдягаються назад —
            // куди саме вони були вдягнені, міграція не зберігала
            migrationBuilder.Sql("""
                UPDATE "EquipmentItems"
                SET "ItemKey" = regexp_replace("ItemKey", '_necklace_(common|rare|unique)$', '_amulet_\1')
                WHERE "Slot" = 2 AND "ItemKey" ~ '_necklace_(common|rare|unique)$';

                UPDATE "EquipmentItems"
                SET "ItemKey" = regexp_replace("ItemKey", '_crown_(common|rare|unique)$', '_sigil_\1')
                WHERE "Slot" = 2 AND "ItemKey" ~ '_crown_(common|rare|unique)$';

                UPDATE "EquipmentItems"
                SET "ItemKey" = regexp_replace("ItemKey", '_belt_(common|rare|unique)$', '_chime_\1')
                WHERE "Slot" = 2 AND "ItemKey" ~ '_belt_(common|rare|unique)$';
                """);
        }
    }
}
