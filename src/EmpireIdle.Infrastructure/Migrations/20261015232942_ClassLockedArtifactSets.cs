using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClassLockedArtifactSets : Migration
    {
        /// <summary>
        /// Мертві бонуси заточки замінено (рішення 15.10.2026): старий стат → новий і множник ступенів.
        /// Ступені нових бонусів пропорційні старим, тож сума ступенів на предметі переводиться множенням.
        /// </summary>
        private static readonly (string From, string To, double Factor)[] RenamedStats =
        [
            ("AttackSpeed", "MarchSpeed", 2.0),            // 2/4/6/8 → 4/8/12/16
            ("CooldownReduction", "Ward", 1.5),            // 2/4/6/8 → 3/6/9/12
            ("UnitCritChance", "TroopsAttack", 2.0),       // 1/2/3/4 → 2/4/6/8
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
            foreach (var (from, to, factor) in RenamedStats)
                migrationBuilder.Sql($@"
                    UPDATE ""EquipmentStats""
                    SET ""StatKey"" = '{to}', ""Value"" = round((""Value"" * {factor.ToString(System.Globalization.CultureInfo.InvariantCulture)})::numeric, 2)
                    WHERE ""StatKey"" = '{from}';");

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
            // Зняте з героїв назад не вдягнути — хто що носив, уже не відомо; повертаємо лише ключі статів
            foreach (var (from, to, factor) in RenamedStats)
                migrationBuilder.Sql($@"
                    UPDATE ""EquipmentStats""
                    SET ""StatKey"" = '{from}', ""Value"" = round((""Value"" / {factor.ToString(System.Globalization.CultureInfo.InvariantCulture)})::numeric, 2)
                    WHERE ""StatKey"" = '{to}';");
        }
    }
}
