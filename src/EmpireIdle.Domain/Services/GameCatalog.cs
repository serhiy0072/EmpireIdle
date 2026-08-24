namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Незмінний індексований довідник ігрового балансу.
    /// Будується один раз на старті — словники не перебудовуються на кожен запит.
    ///
    /// Конструктор також валідує конфіг. Помилка балансу тут падає при старті
    /// застосунку, а не через тиждень у гравця: конфіг редагується руками,
    /// і компілятор його не перевіряє.
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

        /// <summary>Ключ головної будівлі — гейт для решти.</summary>
        public string MainBuildingKey { get; }

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

            Validate(config);
        }

        /// <summary>
        /// Перевіряє те, що не ловить ні компілятор, ні десеріалізація:
        /// узгодженість чисел балансу між собою.
        /// </summary>
        private static void Validate(GameConfig config)
        {
            var badGrowth = config.Buildings
                .Where(b => b.UpgradeCostGrowth < 1.0)
                .Select(b => b.Key)
                .ToList();

            if (badGrowth.Count > 0)
                throw new InvalidOperationException(
                    $"UpgradeCostGrowth below 1.0 makes upgrades cheaper with level: {string.Join(", ", badGrowth)}.");

            // Вартість не може згадувати ресурс, який гравець ще не виробляє:
            // стартового запасу вистачить ненадовго, і будівля стане недосяжною
            // до відкриття відповідної шахти
            var producedAt = config.Buildings
                .Where(b => b.ProducesResource is not null)
                .GroupBy(b => b.ProducesResource!)
                .ToDictionary(g => g.Key, g => g.Min(b => b.RequiresMainBuildingLevel));

            var unreachable = config.Buildings
                .SelectMany(b => b.Cost.Select(c => (Building: b, c.Resource)))
                .Where(x => producedAt.TryGetValue(x.Resource, out var unlockedAt)
                            && unlockedAt > x.Building.RequiresMainBuildingLevel)
                .Select(x => $"{x.Building.Key} costs {x.Resource}")
                .Distinct()
                .ToList();

            if (unreachable.Count > 0)
                throw new InvalidOperationException(
                    $"Buildings cost resources unlocked later: {string.Join("; ", unreachable)}.");

            // Кожне сховище має покривати рівно ті ресурси, які хтось виробляє,
            // інакше ресурс без сховища накопичується без ліміту
            var stored = config.Buildings
                .Where(b => b.StoresResources is not null)
                .SelectMany(b => b.StoresResources!)
                .ToHashSet();

            var unstored = producedAt.Keys.Where(r => !stored.Contains(r)).ToList();

            if (unstored.Count > 0)
                throw new InvalidOperationException(
                    $"Produced resources have no storage building: {string.Join(", ", unstored)}.");
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
