using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Перевіряє узгодженість конфіга між секціями: чи посилання ведуть
    /// на існуючі ключі, чи криві мають сенс, чи геометрія несуперечлива.
    ///
    /// Окремо від GameCatalog, бо росте з кожним конфігом, а каталог —
    /// це індекс, і валідація в ньому лише мешкала.
    ///
    /// Розподіл із перевіркою в Program лишається той самий: тут —
    /// «щось не сходиться», там — «щось порожнє або поза межами».
    /// </summary>
    public static class GameConfigValidator
    {
        /// <summary>
        /// Кидає InvalidOperationException на першій знайденій розбіжності.
        /// Викликається до побудови словників: інакше дублікат ключів упав би
        /// з ArgumentException, а відсутня головна будівля — із Single(),
        /// і жодне з повідомлень не пояснило б причини.
        /// </summary>
        public static void Validate(GameConfig config)
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
    }
}
