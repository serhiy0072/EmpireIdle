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

        /// <summary>Ключ головної будівлі — гейт для решти.</summary>
        public string MainBuildingKey { get; }

        public GameCatalog(GameConfig config)
        {
            // Валідація ПЕРЕД побудовою словників: інакше дублікат ключів
            // кине ArgumentException із ToDictionary, а відсутня головна будівля —
            // з Single(), і обидва повідомлення нічого не пояснять
            Validate(config);

            Config = config;

            Buildings = config.Buildings.ToDictionary(b => b.Key);
            Units = config.Units.ToDictionary(u => u.Key);
            Resources = config.Resources.ToDictionary(r => r.Key);
            Monsters = config.Monsters.ToDictionary(m => m.Key);
            Items = config.Items.ToDictionary(i => i.Key);
            Quests = config.Quests.ToDictionary(q => q.Key);
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

        /// <summary>
        /// Перевіряє те, що не ловить ні компілятор, ні десеріалізація:
        /// узгодженість секцій конфіга між собою.
        /// </summary>
        private static void Validate(GameConfig config)
        {
            ValidateKeys(config);
            ValidateEconomy(config);
            ValidateQuests(config);
            ValidateGeometry(config);
            ValidateRating(config);
            ValidatePreview(config);
        }

        /// <summary>Унікальність ключів і рівно одна головна будівля.</summary>
        private static void ValidateKeys(GameConfig config)
        {
            RequireUniqueKeys(config.Buildings.Select(b => b.Key), "Buildings");
            RequireUniqueKeys(config.Units.Select(u => u.Key), "Units");
            RequireUniqueKeys(config.Resources.Select(r => r.Key), "Resources");
            RequireUniqueKeys(config.Monsters.Select(m => m.Key), "Monsters");
            RequireUniqueKeys(config.Items.Select(i => i.Key), "Items");
            RequireUniqueKeys(config.Quests.Select(q => q.Key), "Quests");

            var mainBuildings = config.Buildings.Count(b => b.IsMainBuilding);

            if (mainBuildings != 1)
                throw new InvalidOperationException(
                    $"Exactly one building must be marked IsMainBuilding, found {mainBuildings}. "
                    + "It gates every other building, so there is no sensible default.");
        }

        /// <summary>Вартості, виробництво, сховища, стартові ресурси, світи.</summary>
        private static void ValidateEconomy(GameConfig config)
        {
            // 0 означає «не задано» — мінімальні фікстури в тестах не описують
            // криві вартості. Помилка тільки при явно хибному значенні.
            var badGrowth = config.Buildings
                .Where(b => b.UpgradeCostGrowth != 0 && b.UpgradeCostGrowth < 1.0)
                .Select(b => b.Key)
                .ToList();

            if (badGrowth.Count > 0)
                throw new InvalidOperationException(
                    $"UpgradeCostGrowth below 1.0 makes upgrades cheaper with level: {string.Join(", ", badGrowth)}.");

            var resourceKeys = config.Resources.Select(r => r.Key).ToHashSet();

            var unknownStarting = config.StartingResources.Keys
                .Where(k => !resourceKeys.Contains(k))
                .ToList();

            if (unknownStarting.Count > 0)
                throw new InvalidOperationException(
                    $"StartingResources reference unknown resources: {string.Join(", ", unknownStarting)}.");

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

            // Кожен вироблюваний ресурс має сховище, інакше він накопичується
            // без ліміту й тихо ламає економіку складу
            var stored = config.Buildings
                .Where(b => b.StoresResources is not null)
                .SelectMany(b => b.StoresResources!)
                .ToHashSet();

            var unstored = producedAt.Keys.Where(r => !stored.Contains(r)).ToList();

            if (unstored.Count > 0)
                throw new InvalidOperationException(
                    $"Produced resources have no storage building: {string.Join(", ", unstored)}.");

            if (!config.ActiveServerIds.Contains(config.DefaultServerId))
                throw new InvalidOperationException(
                    $"DefaultServerId {config.DefaultServerId} is not in ActiveServerIds — "
                    + "new players would land on a world that does not run.");
        }

        /// <summary>Передумови квестів і посилання нагород.</summary>
        private static void ValidateQuests(GameConfig config)
        {
            var questKeys = config.Quests.Select(q => q.Key).ToHashSet();

            var brokenPrerequisites = config.Quests
                .Where(q => q.Prerequisite is not null && !questKeys.Contains(q.Prerequisite))
                .Select(q => $"{q.Key} → {q.Prerequisite}")
                .ToList();

            if (brokenPrerequisites.Count > 0)
                throw new InvalidOperationException(
                    $"Quests reference missing prerequisites: {string.Join("; ", brokenPrerequisites)}.");

            var resourceKeys = config.Resources.Select(r => r.Key).ToHashSet();
            var itemKeys = config.Items.Select(i => i.Key).ToHashSet();

            var rewards = config.Quests.SelectMany(q => q.Rewards.Select(r => (Quest: q.Key, Reward: r)));

            var brokenRewards = rewards
                .Where(x => x.Reward.Type switch
                {
                    "Resource" => x.Reward.Key is null || !resourceKeys.Contains(x.Reward.Key),
                    "Item" => x.Reward.Key is null || !itemKeys.Contains(x.Reward.Key),
                    _ => false
                })
                .Select(x => $"{x.Quest} → {x.Reward.Type} '{x.Reward.Key ?? "(no key)"}'")
                .ToList();

            if (brokenRewards.Count > 0)
                throw new InvalidOperationException(
                    $"Quest rewards reference unknown keys: {string.Join("; ", brokenRewards)}.");
        }

        /// <summary>Кільця карти й туман.</summary>
        private static void ValidateGeometry(GameConfig config)
        {
            var geometry = config.Map.Geometry;

            // Порожня геометрія — конфіг її не описує (мінімальні фікстури в тестах).
            // Валідуємо лише те, що задано.
            if (geometry.RingBoundaries.Count == 0 && geometry.RingMultipliers.Count == 0)
                return;

            if (geometry.RingBoundaries.Count != geometry.RingMultipliers.Count - 1)
                throw new InvalidOperationException(
                    "RingBoundaries needs exactly one entry fewer than RingMultipliers: "
                    + "the last ring is everything beyond the last boundary.");

            if (geometry.RingBoundaries.Count == 0 || geometry.RingBoundaries[0] <= 0)
                throw new InvalidOperationException("The innermost ring boundary must be greater than 0.");

            if (geometry.RingBoundaries[^1] > 1.0)
                throw new InvalidOperationException("Ring boundaries are shares of the radius and cannot exceed 1.0.");

            for (var i = 1; i < geometry.RingBoundaries.Count; i++)
            {
                if (geometry.RingBoundaries[i] <= geometry.RingBoundaries[i - 1])
                    throw new InvalidOperationException("Ring boundaries must increase outward.");
            }

            if (geometry.FogMinShare <= 0 || geometry.FogMinShare > geometry.FogMaxShare || geometry.FogMaxShare > 1.0)
                throw new InvalidOperationException("Fog shares must satisfy 0 < FogMinShare ≤ FogMaxShare ≤ 1.");
        }

        /// <summary>Ваги й орієнтири рейтингу.</summary>
        private static void ValidateRating(GameConfig config)
        {
            var rating = config.Rating;
            var weightSum = rating.PowerWeight + rating.DevelopmentWeight + rating.ActivityWeight;

            // Ваги — частки рейтингу, тому сума має бути одиницею: інакше
            // «максимальний рейтинг» перестає бути передбачуваним числом
            if (Math.Abs(weightSum - 1.0) > 0.001)
                throw new InvalidOperationException(
                    $"Rating weights must sum to 1.0, got {weightSum:F3}.");

            if (rating.PowerReference <= 0 || rating.DevelopmentReference <= 0 || rating.ActivityReference <= 0)
                throw new InvalidOperationException(
                    "Rating references must be positive — they are the denominators of normalisation.");

            if (rating.Scale <= 0)
                throw new InvalidOperationException("Rating.Scale must be positive.");
        }

        private static void RequireUniqueKeys(IEnumerable<string> keys, string section)
        {
            var duplicates = keys
                .GroupBy(k => k)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
                throw new InvalidOperationException(
                    $"{section} has duplicate keys: {string.Join(", ", duplicates)}.");
        }

        /// <summary>Пороги прев'ю бою.</summary>
        private static void ValidatePreview(GameConfig config)
        {
            var thresholds = config.Combat.PreviewOddsThresholds;

            // Порожньо — конфіг прев'ю не описує (мінімальні фікстури в тестах).
            // Перевіряємо лише те, що задано.
            if (thresholds.Count == 0)
                return;

            for (var i = 1; i < thresholds.Count; i++)
            {
                if (thresholds[i] >= thresholds[i - 1])
                    throw new InvalidOperationException(
                        "Combat.PreviewOddsThresholds must decrease: the first band is the strongest.");
            }
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
