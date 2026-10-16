using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClassLockedArtifactSets : Migration
    {
        /// <summary>
        /// Нові ступені заточки (рішення 16.10.2026): старий стат → новий і множник ступенів. Значення на
        /// предметі — сума випалих ступенів, тож переводиться множенням на середнє відношення нових до старих.
        /// Порядок важливий: CritChance множиться раніше, ніж до нього додається колишній крит юнітів.
        /// </summary>
        private static readonly (string From, string To, double Factor)[] RescaledStats =
        [
            ("AttackSpeed", "AttackSpeed", 2.0),            // 2/4/6/8 → 5/8/11/15
            ("CritChance", "CritChance", 4.75),             // 1/2/3/4 → 5/9/14/20
            ("UnitCritChance", "CritChance", 4.75),         // крит юнітів стає критом героя
            ("UnitHealthPercent", "TroopsDefense", 2.0 / 3) // 3/6/9/12 → 2/4/6/8
        ];

        /// <summary>Клас кожної родини наборів — знімок items.json на момент міграції.</summary>
        private const string SetClasses = @"
            ('flint', 'warrior'), ('frost', 'warrior'), ('horn', 'warrior'), ('ash', 'warrior'), ('obsidian', 'warrior'),
            ('dawn', 'knight'), ('amber', 'knight'), ('thorn', 'knight'), ('hollow', 'knight'), ('zenith', 'knight'),
            ('ember', 'archer'), ('tide', 'archer'), ('storm', 'archer'), ('moon', 'archer'), ('gale', 'archer')";

        /// <summary>Клас кожного героя — знімок heroes.json: з ключа його не вгадати (mage_iselle — лучниця).</summary>
        private const string HeroClasses = @"
            ('warrior_bran', 'warrior'), ('warrior_hedda', 'warrior'), ('warrior_torvald', 'warrior'), ('warrior_maren', 'warrior'),
            ('warrior_gunnar', 'warrior'), ('mage_sabine', 'warrior'),
            ('knight_ordwin', 'knight'), ('knight_aldric', 'knight'), ('knight_serah', 'knight'), ('knight_elowen', 'knight'),
            ('mage_calder', 'knight'), ('mage_vael', 'knight'),
            ('archer_lyra', 'archer'), ('archer_finn', 'archer'), ('archer_ysolde', 'archer'), ('archer_rook', 'archer'),
            ('archer_kestrel', 'archer'), ('mage_iselle', 'archer'), ('mage_nerys', 'archer')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (from, to, factor) in RescaledStats)
                Rescale(migrationBuilder, from, to, factor);

            // Набір вдягає лише свій клас (рішення 15.10.2026): чуже знімаємо в рюкзак — предмет лишається гравцеві
            migrationBuilder.Sql($@"
                UPDATE ""EquipmentItems"" e
                SET ""EquippedByHeroId"" = NULL, ""SlotIndex"" = 0, ""UpdatedAt"" = now()
                FROM ""Heroes"" h,
                     (VALUES {SetClasses}) AS sets(family, class),
                     (VALUES {HeroClasses}) AS heroes(hero_key, class)
                WHERE e.""EquippedByHeroId"" = h.""Id""
                  AND split_part(e.""ItemKey"", '_', 1) = sets.family
                  AND h.""HeroKey"" = heroes.hero_key
                  AND sets.class <> heroes.class;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Зняте з героїв назад не вдягнути — хто що носив, уже не відомо; повертаємо лише стати.
            // Колишній крит юнітів злився з критом героя й лишається ним: розрізнити їх уже нічим
            Rescale(migrationBuilder, "TroopsDefense", "UnitHealthPercent", 1.5);
            Rescale(migrationBuilder, "CritChance", "CritChance", 1 / 4.75);
            Rescale(migrationBuilder, "AttackSpeed", "AttackSpeed", 0.5);
        }

        private static void Rescale(MigrationBuilder migrationBuilder, string from, string to, double factor)
            => migrationBuilder.Sql($@"
                UPDATE ""EquipmentStats""
                SET ""StatKey"" = '{to}', ""Value"" = round((""Value"" * {factor.ToString("R", System.Globalization.CultureInfo.InvariantCulture)})::numeric, 2)
                WHERE ""StatKey"" = '{from}';");
    }
}
