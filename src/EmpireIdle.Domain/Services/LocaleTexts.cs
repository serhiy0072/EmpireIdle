using System.Diagnostics.CodeAnalysis;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Тексти конфігів однією мовою. Конфіги написані мовою за замовчуванням,
    /// локаль лише перекриває їх: чого в ній немає, показується з конфіга.
    ///
    /// Ключ — «розділ.ключ» для назви й «розділ.ключ.поле» для решти
    /// (description, lore, weapon). Перелік допустимих ключів — у
    /// <see cref="LocalizableKeys"/>, ним же користується валідатор.
    /// </summary>
    public sealed class LocaleTexts
    {
        public const string DescriptionField = "description";
        public const string LoreField = "lore";
        public const string WeaponField = "weapon";

        private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>();

        private readonly IReadOnlyDictionary<string, string> _texts;

        public LocaleTexts(GameConfig config, string language)
        {
            Language = language;
            _texts = config.Locales.GetValueOrDefault(language)?.Texts ?? None;
        }

        public string Language { get; }

        /// <summary>Назва запису розділу.</summary>
        public string Name(string section, string key, string fallback)
            => _texts.GetValueOrDefault($"{section}.{key}", fallback);

        /// <summary>Інше поле запису: опис, лор, назва зброї. null у конфігу лишається null.</summary>
        [return: NotNullIfNotNull(nameof(fallback))]
        public string? Field(string section, string key, string field, string? fallback)
            => fallback is null ? null : _texts.GetValueOrDefault($"{section}.{key}.{field}", fallback);

        /// <summary>Усі ключі, які локаль може перекривати: назви й поля кожного запису з текстом для гравця.</summary>
        public static HashSet<string> LocalizableKeys(GameConfig config)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            void Named(string section, IEnumerable<string> entryKeys, params string[] fields)
            {
                foreach (var key in entryKeys)
                {
                    keys.Add($"{section}.{key}");

                    foreach (var field in fields)
                        keys.Add($"{section}.{key}.{field}");
                }
            }

            Named("building", config.Buildings.Select(b => b.Key));
            Named("resource", config.Resources.Select(r => r.Key));
            Named("unit", config.Units.Select(u => u.Key));
            Named("monster", config.Monsters.Select(m => m.Key));
            Named("beast", config.Beasts.Types.Select(b => b.Key));
            Named("quest", config.Quests.Select(q => q.Key));
            Named("hero", config.Heroes.Select(h => h.Key), DescriptionField, LoreField, WeaponField);
            Named("skill", config.Heroes.SelectMany(h => h.Skills).Select(s => s.Key), DescriptionField);
            Named("item", config.Items.Select(i => i.Key), DescriptionField);
            Named("artifactSet", config.Equipment.ArtifactSets.Select(s => s.Key), DescriptionField);
            Named("artifactSlot", config.Equipment.ArtifactSlots.Select(s => s.Key));
            Named("dungeon", config.Dungeons.Dungeons.Select(d => d.Key), DescriptionField);
            Named("dungeonEnemy", config.Dungeons.Dungeons.SelectMany(d => d.Waves.Concat(d.Boss)).Select(e => e.Key));
            Named("gemPack", config.Shop.GemPacks.Select(p => p.Key));
            Named("banner", config.Shop.Banners.Select(b => b.Key));
            Named("bannerDrop", config.Shop.Banners.SelectMany(b => b.Drops).Select(d => d.Key));
            Named("towerArchetype", config.Tower.Archetypes.Select(a => a.Key));
            Named("towerSetStep", Enumerable.Range(0, config.Tower.SetSteps.Count).Select(i => i.ToString()));

            return keys;
        }
    }
}
