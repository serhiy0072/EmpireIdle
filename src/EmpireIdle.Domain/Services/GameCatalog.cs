namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Незмінний індексований довідник ігрового балансу.
    /// Будується один раз на старті — словники не перебудовуються на кожен запит.
    /// </summary>
    public class GameCatalog
    {        /// <summary>Повний конфіг — для секцій без ключа (Map, Combat, Shop, Monetization).</summary>
        public GameConfig Config { get; }

        public IReadOnlyDictionary<string, BuildingConfig> Buildings { get; }
        public IReadOnlyDictionary<string, UnitConfig> Units { get; }
        public IReadOnlyDictionary<string, ResourceConfig> Resources { get; }
        public IReadOnlyDictionary<string, MonsterConfig> Monsters { get; }
        public IReadOnlyDictionary<string, ItemConfig> Items { get; }
        public IReadOnlyDictionary<string, QuestConfig> Quests { get; }

        /// <summary>Ключ головної будівлі — гейт для решти.</summary>
        public string MainBuildingKey { get; }

        /// <summary>Ресурси-місткості (населення) — не списуються повторно при лікуванні.</summary>
        public IReadOnlySet<string> CapacityResourceKeys { get; }

        public GameCatalog(GameConfig config)
        {
            Config = config;

            Buildings = config.Buildings.ToDictionary(b => b.Key);
            Units = config.Units.ToDictionary(u => u.Key);
            Resources = config.Resources.ToDictionary(r => r.Key);
            Monsters = config.Monsters.ToDictionary(m => m.Key);
            Items = config.Items.ToDictionary(i => i.Key);
            Quests = config.Quests.ToDictionary(q => q.Key);
            MainBuildingKey = config.Buildings.Single(b => b.IsMainBuilding).Key;
            CapacityResourceKeys = config.Resources.Where(r => r.IsCapacity).Select(r => r.Key).ToHashSet();
        }

        /// <summary>Будівля за ключем або виняток із зрозумілим текстом.</summary>
        public BuildingConfig Building(string key) => Buildings.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Building '{key}' is not defined in the catalog.");

        public UnitConfig Unit(string key) => Units.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Unit '{key}' is not defined in the catalog.");

        public ItemConfig Item(string key) => Items.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Item '{key}' is not defined in the catalog.");

        public QuestConfig Quest(string key) => Quests.TryGetValue(key, out var c)
            ? c : throw new InvalidOperationException($"Quest '{key}' is not defined in the catalog.");
    }
}
