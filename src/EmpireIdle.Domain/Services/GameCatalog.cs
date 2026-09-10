using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Незмінний індексований довідник ігрового балансу.
    /// Будується один раз на старті — словники не перебудовуються на кожен запит.
    ///
    /// Конструктор також валідує конфіг. Розподіл між цією перевіркою і тією,
    /// що в Program: тут — узгодженість між секціями («щось не сходиться»),
    /// там — наповненість і межі окремих полів («щось порожнє»).
    /// Правило пошуку однозначне, і наступна перевірка не піде навмання.
    /// </summary>
    public class GameCatalog
    {
        /// <summary>Повний конфіг — для секцій без ключа (Map, Combat, Shop, Monetization).</summary>
        public GameConfig Config { get; }

        public IReadOnlyDictionary<string, BuildingConfig> Buildings { get; }
        public IReadOnlyDictionary<string, UnitConfig> Units { get; }
        public IReadOnlyDictionary<string, ResourceConfig> Resources { get; }
        public IReadOnlyDictionary<string, MonsterConfig> Monsters { get; }
        public IReadOnlyDictionary<string, ItemConfig> Items { get; }
        public IReadOnlyDictionary<string, QuestConfig> Quests { get; }
        public IReadOnlyDictionary<string, HeroConfig> Heroes { get; }

        /// <summary>Ключ головної будівлі — гейт для решти.</summary>
        public string MainBuildingKey { get; }

        public GameCatalog(GameConfig config)
        {
            // Валідація ПЕРЕД побудовою словників: інакше дублікат ключів
            // кине ArgumentException із ToDictionary, а відсутня головна будівля —
            // з Single(), і обидва повідомлення нічого не пояснять
            GameConfigValidator.Validate(config);

            Config = config;

            Buildings = config.Buildings.ToDictionary(b => b.Key);
            Units = config.Units.ToDictionary(u => u.Key);
            Resources = config.Resources.ToDictionary(r => r.Key);
            Monsters = config.Monsters.ToDictionary(m => m.Key);
            Items = config.Items.ToDictionary(i => i.Key);
            Quests = config.Quests.ToDictionary(q => q.Key);
            Heroes = config.Heroes.ToDictionary(h => h.Key);
            MainBuildingKey = config.Buildings.Single(b => b.IsMainBuilding).Key;
        }

        /// <summary>
        /// Юніт за ключем, що прийшов від гравця. null означає «такого немає» —
        /// викликач сам вирішує, це 404 чи щось інше.
        ///
        /// Окремо від Unit(), який кидає: там ключ береться з наших даних,
        /// і його відсутність означає поломку розгортання, тобто 500.
        /// </summary>
        public UnitConfig? FindUnit(string key) => Units.GetValueOrDefault(key);

        /// <inheritdoc cref="FindUnit"/>
        public ItemConfig? FindItem(string key) => Items.GetValueOrDefault(key);

        /// <inheritdoc cref="FindUnit"/>
        public HeroConfig? FindHero(string key) => Heroes.GetValueOrDefault(key);

        /// <summary>Будівля за ключем або виняток із зрозумілим текстом.</summary>
        public BuildingConfig Building(string key) => Buildings.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Building '{key}' is not defined in the catalog.");

        public UnitConfig Unit(string key) => Units.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Unit '{key}' is not defined in the catalog.");

        public ItemConfig Item(string key) => Items.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Item '{key}' is not defined in the catalog.");

        public QuestConfig Quest(string key) => Quests.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Quest '{key}' is not defined in the catalog.");

        public HeroConfig Hero(string key) => Heroes.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Hero '{key}' is not defined in the catalog.");
    }
}
