using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Enums;
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
            ValidateHeroes(config);
            ValidateLossBands(config);
            ValidateDungeons(config);
            ValidateUnlockThresholds(config);
            ValidateShopItems(config);
            ValidateEquipment(config);
            ValidateBanners(config);
            ValidateBuildingLayout(config);
        }

        /// <summary>
        /// Данжі: унікальні ключі, набір артефактів під кожну рідкість, бос і хвилі.
        /// Данж без боса чи без набору віддав би гравцю порожній забіг.
        /// </summary>
        private static void ValidateDungeons(GameConfig config)
        {
            var dungeons = config.Dungeons;

            if (dungeons.Dungeons.Count == 0)
                return;

            RequireUniqueKeys(dungeons.Dungeons.Select(d => d.Key), "Dungeons");

            if (dungeons.EnergyPerRun <= 0 || dungeons.MaxEnergy < dungeons.EnergyPerRun)
                throw new InvalidOperationException(
                    "Dungeons.EnergyPerRun must be positive and fit into MaxEnergy — otherwise no run is ever affordable.");

            if (dungeons.LevelPowerMultipliers.Count < dungeons.MaxLevel || dungeons.LevelRewardMultipliers.Count < dungeons.MaxLevel)
                throw new InvalidOperationException(
                    $"Dungeons has fewer level multipliers than MaxLevel {dungeons.MaxLevel}.");

            var setKeys = config.Items
                .Where(i => i.SetKey is not null)
                .Select(i => i.SetKey!)
                .ToHashSet();

            var rarities = Enum.GetNames<Rarity>().Select(r => r.ToLowerInvariant()).ToList();

            foreach (var dungeon in dungeons.Dungeons)
            {
                if (dungeon.Boss.Count == 0)
                    throw new InvalidOperationException($"Dungeon '{dungeon.Key}' has no boss wave.");

                if (dungeon.Waves.Count == 0)
                    throw new InvalidOperationException($"Dungeon '{dungeon.Key}' has no regular waves.");

                // Рівень забігу обирає рідкість набору, тож бракує хоч одного — і нагорода зникає
                foreach (var rarity in rarities)
                    if (!setKeys.Contains($"{dungeon.ArtifactSetKey}_{rarity}"))
                        throw new InvalidOperationException(
                            $"Dungeon '{dungeon.Key}' has no artifact set '{dungeon.ArtifactSetKey}_{rarity}' in Items.");

                foreach (var enemy in dungeon.Waves.Concat(dungeon.Boss))
                    if (enemy.Health <= 0 || enemy.Attack <= 0)
                        throw new InvalidOperationException(
                            $"Dungeon '{dungeon.Key}' enemy '{enemy.Key}' has non-positive attack or health.");
            }
        }

        /// <summary>
        /// Жоден поріг відкриття не вищий за ратушу, яку взагалі можна
        /// збудувати (MaxServerLevel × BuildingLevelsPerTier). Інакше вміст
        /// недосяжний назавжди, а гравець бачить замок, який не відімкнеться.
        /// </summary>
        private static void ValidateUnlockThresholds(GameConfig config)
        {
            var ceiling = config.Map.MaxServerLevel * config.BuildingLevelsPerTier;

            var unreachable = config.Buildings
                .Where(b => b.RequiresMainBuildingLevel > ceiling)
                .Select(b => $"building {b.Key} ({b.RequiresMainBuildingLevel})")
                .Concat(config.Resources
                    .Where(r => r.RequiresMainBuildingLevel > ceiling)
                    .Select(r => $"resource {r.Key} ({r.RequiresMainBuildingLevel})"))
                .Concat(config.Dungeons.Dungeons
                    .Where(d => d.RequiresMainBuildingLevel > ceiling)
                    .Select(d => $"dungeon {d.Key} ({d.RequiresMainBuildingLevel})"))
                .ToList();

            if (unreachable.Count > 0)
                throw new InvalidOperationException(
                    $"Unlock thresholds above the town hall ceiling {ceiling}: {string.Join(", ", unreachable)}.");
        }

        /// <summary>Кожен товар крамниці — існуючий предмет; спорядження продає кузня за золото, не крамниця.</summary>
        private static void ValidateShopItems(GameConfig config)
        {
            RequireUniqueKeys(config.Shop.Items.Select(i => i.ItemKey), "Shop.Items");

            var items = config.Items.ToDictionary(i => i.Key);

            foreach (var offer in config.Shop.Items)
            {
                if (!items.TryGetValue(offer.ItemKey, out var item))
                    throw new InvalidOperationException($"Shop item '{offer.ItemKey}' is not in Items.");

                if (item.Type == "equipment")
                    throw new InvalidOperationException($"Shop item '{offer.ItemKey}' is equipment — weapons are sold by the forge for gold.");
            }
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
            RequireUniqueKeys(config.Heroes.Select(h => h.Key), "Heroes");

            var mainBuildings = config.Buildings.Count(b => b.IsMainBuilding);
            var heroKeys = config.Heroes.Select(h => h.Key).ToHashSet();

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
            var items = config.Items.ToDictionary(i => i.Key);
            var heroKeys = config.Heroes.Select(h => h.Key).ToHashSet();

            // Ярусні нагороди серверних квестів видає той самий диспетчер
            var rewards = config.Quests.SelectMany(q => q.Rewards
                .Concat(q.RewardTiers.SelectMany(t => t.Rewards))
                .Select(r => (Quest: q.Key, Reward: r)));

            // Регістр ігнорується, як у RewardDispatcher. Невідомий тип тут не ловиться:
            // список грантерів живе в Application
            var brokenRewards = rewards
                .Where(x => IsRewardBroken(x.Reward, resourceKeys, items, heroKeys))
                .Select(x => $"{x.Quest} → {x.Reward.Type} '{x.Reward.Key ?? "(no key)"}'")
                .ToList();

            if (brokenRewards.Count > 0)
                throw new InvalidOperationException(
                    $"Quest rewards reference unknown keys or keys of the wrong kind: {string.Join("; ", brokenRewards)}.");
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

        /// <summary>
        /// Смуги втрат. Головне тут не межі 0–1, а відсутність інверсії:
        /// якщо нижня межа програшу опускається під верхню межу перемоги,
        /// то за певного співвідношення сил програти стає дешевше, ніж
        /// перемогти, і оптимальною стратегією стає навмисна поразка.
        /// </summary>
        private static void ValidateLossBands(GameConfig config)
        {
            var bands = new (string Name, LossBand Band)[]
            {
                ("AttackerWinLosses", config.Combat.AttackerWinLosses),
                ("AttackerLossLosses", config.Combat.AttackerLossLosses),
                ("DefenderWinLosses", config.Combat.DefenderWinLosses),
                ("DefenderLossLosses", config.Combat.DefenderLossLosses)
            };

            foreach (var (name, band) in bands)
            {
                if (band.Min < 0 || band.Max > 1 || band.Min > band.Max)
                    throw new InvalidOperationException(
                        $"Combat.{name} must satisfy 0 <= Min <= Max <= 1.");
            }

            if (config.Combat.AttackerLossLosses.Min < config.Combat.AttackerWinLosses.Max)
                throw new InvalidOperationException(
                    "Combat.AttackerLossLosses.Min must not be below AttackerWinLosses.Max: losing would cost less than winning.");

            if (config.Combat.DefenderLossLosses.Min < config.Combat.DefenderWinLosses.Max)
                throw new InvalidOperationException(
                    "Combat.DefenderLossLosses.Min must not be below DefenderWinLosses.Max: losing would cost less than winning.");
        }

        /// <summary>Ростер героїв: класи, тіри, вартість прокачки, лікування, пасивки.</summary>
        private static void ValidateHeroes(GameConfig config)
        {
            // Порожній ростер — конфіг героїв не описує (мінімальні фікстури в тестах).
            // Перевіряємо лише те, що задано.
            if (config.Heroes.Count == 0)
                return;

            var settings = config.HeroSettings;

            if (settings.MaxTier < 1)
                throw new InvalidOperationException("HeroSettings.MaxTier must be at least 1.");

            if (settings.LevelsPerTier < 1)
                throw new InvalidOperationException("HeroSettings.LevelsPerTier must be at least 1.");

            if (settings.MaxMarches < 1)
                throw new InvalidOperationException(
                    "HeroSettings.MaxMarches must be at least 1 — otherwise a player with heroes still has no marches.");

            if (settings.Classes.Count == 0)
                throw new InvalidOperationException(
                    "HeroSettings.Classes is empty — every hero references a class, and weapons fit by class.");

            RequireUniqueKeys(settings.Classes, "HeroSettings.Classes");

            var classes = settings.Classes.ToHashSet();

            var unknownClasses = config.Heroes
                .Where(h => !classes.Contains(h.Class))
                .Select(h => $"{h.Key} → '{h.Class}'")
                .ToList();

            if (unknownClasses.Count > 0)
                throw new InvalidOperationException(
                    $"Heroes reference unknown classes: {string.Join(", ", unknownClasses)}.");

            var buildingKeys = config.Buildings.Select(b => b.Key).ToHashSet();

            if (!buildingKeys.Contains(settings.BuildingKey ?? string.Empty))
                throw new InvalidOperationException(
                    $"HeroSettings.BuildingKey '{settings.BuildingKey}' is not a known building — "
                    + "heroes would have nowhere to be summoned.");

            if (!buildingKeys.Contains(settings.HealBuildingKey ?? string.Empty))
                throw new InvalidOperationException(
                    $"HeroSettings.HealBuildingKey '{settings.HealBuildingKey}' is not a known building — "
                    + "wounded heroes would have nowhere to be healed.");

            if (settings.TierStatMultipliers.Count != settings.MaxTier)
                throw new InvalidOperationException(
                    $"HeroSettings.TierStatMultipliers needs exactly MaxTier entries ({settings.MaxTier}), "
                    + $"got {settings.TierStatMultipliers.Count}.");

            for (var i = 1; i < settings.TierStatMultipliers.Count; i++)
            {
                if (settings.TierStatMultipliers[i] <= settings.TierStatMultipliers[i - 1])
                    throw new InvalidOperationException(
                        "HeroSettings.TierStatMultipliers must increase: otherwise evolution raises only the "
                        + "ceiling, and two heroes of different tiers are identical at the same level.");
            }

            // Переходів рівно на один менше, ніж тірів: 1→2 і 2→3 для трьох тірів
            if (settings.EvolutionItemKeys.Count != settings.MaxTier - 1)
                throw new InvalidOperationException(
                    $"HeroSettings.EvolutionItemKeys needs MaxTier - 1 entries ({settings.MaxTier - 1}), "
                    + $"got {settings.EvolutionItemKeys.Count}.");

            var itemKeys = config.Items.Select(i => i.Key).ToHashSet();

            var unknownItems = settings.EvolutionItemKeys
                .Where(k => !itemKeys.Contains(k))
                .ToList();

            if (unknownItems.Count > 0)
                throw new InvalidOperationException(
                    $"HeroSettings.EvolutionItemKeys reference unknown items: {string.Join(", ", unknownItems)}.");

            // Надлишок понад стелю сузір'я стає печатками призову. Забутий ранг означав би,
            // що дублікат зникає без сліду — саме те, чого ми уникали.
            var rankNames = Enum.GetNames<Rarity>().ToHashSet();

            var unknownRanks = settings.OverflowSeals.Keys
                .Where(k => !rankNames.Contains(k))
                .ToList();

            if (unknownRanks.Count > 0)
                throw new InvalidOperationException(
                    $"HeroSettings.OverflowSeals has keys that are not ranks: {string.Join(", ", unknownRanks)}.");

            var missingRanks = config.Heroes
                .Select(h => h.Rank.ToString())
                .Distinct()
                .Where(r => !settings.OverflowSeals.ContainsKey(r))
                .ToList();

            if (missingRanks.Count > 0)
                throw new InvalidOperationException(
                    "HeroSettings.OverflowSeals has no entry for ranks in the roster: "
                    + $"{string.Join(", ", missingRanks)}.");

            var resourceKeys = config.Resources.Select(r => r.Key).ToHashSet();
            var unitKeys = config.Units.Select(u => u.Key).ToHashSet();
            var statKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Attack", "Defense" };

            // Порожня вартість означає безкоштовне лікування, і поразка
            // перестає щось коштувати взагалі
            if (settings.HealCostPerLevel.Count == 0)
                throw new InvalidOperationException(
                    "HeroSettings.HealCostPerLevel is empty — healing a hero would be free.");

            var brokenHealCost = settings.HealCostPerLevel
                .Where(c => !resourceKeys.Contains(c.Resource) || c.Amount < 1)
                .Select(c => $"'{c.Resource}' × {c.Amount}")
                .ToList();

            if (brokenHealCost.Count > 0)
                throw new InvalidOperationException(
                    $"HeroSettings.HealCostPerLevel has invalid entries: {string.Join(", ", brokenHealCost)}.");

            if (settings.DefaultMarchSpeed <= 0)
                throw new InvalidOperationException("HeroSettings.DefaultMarchSpeed must be above zero.");

            foreach (var hero in config.Heroes)
            {
                // Смуги вартості прокачки. Набір ресурсів міняється з рівнем, тож діра
                // між смугами вилізла б лише тоді, коли до неї дійшов би гравець.
                if (hero.LevelUpCosts.Count == 0)
                    throw new InvalidOperationException(
                        $"Hero '{hero.Key}' has no LevelUpCosts — it could never be levelled.");

                if (hero.LevelUpCosts.All(b => b.FromLevel > 1))
                    throw new InvalidOperationException(
                        $"Hero '{hero.Key}' has no cost band starting at level 1.");

                RequireUniqueKeys(
                    hero.LevelUpCosts.Select(b => b.FromLevel.ToString()),
                    $"Hero '{hero.Key}' LevelUpCosts.FromLevel");

                var brokenLines = hero.LevelUpCosts
                    .SelectMany(b => b.Cost.Select(c => (b.FromLevel, c.Resource, c.Amount)))
                    .Where(x => !resourceKeys.Contains(x.Resource) || x.Amount < 1)
                    .Select(x => $"from {x.FromLevel}: '{x.Resource}' × {x.Amount}")
                    .ToList();

                if (brokenLines.Count > 0)
                    throw new InvalidOperationException(
                        $"Hero '{hero.Key}' has invalid LevelUpCosts entries: {string.Join(", ", brokenLines)}.");

                if (hero.Speed is <= 0)
                    throw new InvalidOperationException($"Hero '{hero.Key}' has non-positive Speed — its marches would never arrive.");

                // Пасивки: саме вони, а не стати героя, рухають бойову формулу,
                // тож описка в цілі або статі мовчки знеструмила б героя
                RequireUniqueKeys(hero.Passives.Select(p => p.Key).ToList(), $"Heroes['{hero.Key}'].Passives");

                foreach (var passive in hero.Passives)
                {
                    if (passive.Target != HeroCombatModifiers.AllUnits && !unitKeys.Contains(passive.Target))
                        throw new InvalidOperationException(
                            $"Hero '{hero.Key}' passive '{passive.Key}' targets unknown unit '{passive.Target}'.");

                    if (!statKeys.Contains(passive.Stat ?? string.Empty))
                        throw new InvalidOperationException(
                            $"Hero '{hero.Key}' passive '{passive.Key}' affects unknown stat '{passive.Stat}' — "
                            + "combat knows Attack and Defense.");

                    if (passive.UnlockConstellation < 0 || passive.UnlockConstellation > settings.MaxConstellation)
                        throw new InvalidOperationException(
                            $"Hero '{hero.Key}' passive '{passive.Key}' unlocks at constellation "
                            + $"{passive.UnlockConstellation}, outside 0..{settings.MaxConstellation}.");

                    if (passive.BasePercent < 0 || passive.PercentPerConstellation < 0)
                        throw new InvalidOperationException(
                            $"Hero '{hero.Key}' passive '{passive.Key}' has negative percentages — "
                            + "a passive never weakens its own army.");
                }
            }

            // Звичайні герої купуються уламками за золото — це основний щоденний
            // стік золота. Решта приходить із банерів цілими, ціни не має.
            var brokenShards = config.Heroes
                .Where(h => h.Rank == Rarity.Common && (h.SummonShards < 1 || h.ShardPriceGold < 1))
                .Select(h => h.Key)
                .ToList();

            if (brokenShards.Count > 0)
                throw new InvalidOperationException(
                    "Common heroes need SummonShards and ShardPriceGold above zero: "
                    + $"{string.Join(", ", brokenShards)}.");
        }

        /// <summary>Спорядження: слоти, класи зброї, набори, ціни.</summary>
        private static void ValidateEquipment(GameConfig config)
        {
            var equipment = config.Items.Where(i => i.Type == "equipment").ToList();

            if (equipment.Count == 0)
                return;

            if (config.Equipment.ArtifactSlots < 1)
                throw new InvalidOperationException("Equipment.ArtifactSlots must be at least 1.");

            if (config.Equipment.MaxEnhancement < 1)
                throw new InvalidOperationException("Equipment.MaxEnhancement must be at least 1.");

            if (config.Equipment.EnhancementBonusPerLevel <= 0)
                throw new InvalidOperationException(
                    "Equipment.EnhancementBonusPerLevel must be above zero — otherwise enhancing changes nothing.");

            if (!config.Buildings.Select(b => b.Key).Contains(config.Equipment.ForgeBuildingKey))
                throw new InvalidOperationException(
                    $"Equipment.ForgeBuildingKey '{config.Equipment.ForgeBuildingKey}' is not a known building.");

            if (config.Equipment.ArtifactStats.Count < config.Equipment.ArtifactBaseStats)
                throw new InvalidOperationException(
                    "Equipment.ArtifactStats has fewer entries than ArtifactBaseStats — "
                    + "a new artifact could not be filled.");

            var setKeys = config.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.SetKey))
                .Select(i => i.SetKey!)
                .ToHashSet();

            RequireUniqueKeys(config.Equipment.SetBonuses.Select(b => b.SetKey).ToList(), "Equipment.SetBonuses");

            foreach (var bonus in config.Equipment.SetBonuses)
            {
                if (!setKeys.Contains(bonus.SetKey))
                    throw new InvalidOperationException(
                        $"Set bonus '{bonus.SetKey}' has no items — nobody could ever collect it.");

                var pieces = config.Items.Count(i => i.SetKey == bonus.SetKey);

                // Комплект, більший за кількість предметів або за кількість
                // слотів, недосяжний: правило є, спрацювати не може
                if (bonus.RequiredPieces < 1 || bonus.RequiredPieces > pieces)
                    throw new InvalidOperationException(
                        $"Set bonus '{bonus.SetKey}' needs {bonus.RequiredPieces} pieces but only {pieces} exist.");

                if (bonus.RequiredPieces > config.Equipment.ArtifactSlots)
                    throw new InvalidOperationException(
                        $"Set bonus '{bonus.SetKey}' needs {bonus.RequiredPieces} pieces but a hero has only "
                        + $"{config.Equipment.ArtifactSlots} artifact slots.");

                if (bonus.Stats.Count == 0)
                    throw new InvalidOperationException($"Set bonus '{bonus.SetKey}' grants nothing.");
            }

            var brokenBands = config.Equipment.ArtifactStats
                .Where(s => s.Min > s.Max || s.UpgradeMin > s.UpgradeMax || s.Min < 0 || s.UpgradeMin < 0)
                .Select(s => s.Stat)
                .ToList();

            if (brokenBands.Count > 0)
                throw new InvalidOperationException(
                    $"Equipment.ArtifactStats has invalid bands: {string.Join(", ", brokenBands)}.");

            RequireUniqueKeys(config.Equipment.ArtifactStats.Select(s => s.Stat).ToList(), "Equipment.ArtifactStats");

            if (config.Equipment.DoubleUpgradeChance is < 0 or > 1)
                throw new InvalidOperationException("Equipment.DoubleUpgradeChance must be within 0..1.");

            // Рівні поза стелею означають правило, яке ніколи не спрацює
            var unreachable = config.Equipment.ArtifactStatLevels
                .Concat(config.Equipment.ArtifactUpgradeLevels)
                .Where(l => l < 1 || l > config.Equipment.MaxEnhancement)
                .ToList();

            if (unreachable.Count > 0)
                throw new InvalidOperationException(
                    $"Equipment artifact levels outside 1..{config.Equipment.MaxEnhancement}: "
                    + string.Join(", ", unreachable));

            var classes = config.HeroSettings.Classes.ToHashSet();

            foreach (var item in equipment)
            {
                if (item.Slot is null)
                    throw new InvalidOperationException($"Item '{item.Key}' is equipment but has no Slot.");

                var unknownClasses = item.WeaponClasses.Where(c => !classes.Contains(c)).ToList();

                if (unknownClasses.Count > 0)
                    throw new InvalidOperationException(
                        $"Item '{item.Key}' fits unknown hero classes: {string.Join(", ", unknownClasses)}.");

                // Зброя купується в кузні, артефакти падають у данжах
                if (item.Slot == EquipmentSlot.Weapon && item.PriceGold < 1)
                    throw new InvalidOperationException($"Weapon '{item.Key}' needs a positive PriceGold.");

                if (item.Slot == EquipmentSlot.Artifact && item.BaseStats.Count > 0)
                    throw new InvalidOperationException(
                        $"Artifact '{item.Key}' has BaseStats — artifact stats are rolled per instance.");
            }
        }

        /// <summary>Чи посилається нагорода на неіснуючий ключ або на ключ не того виду.</summary>
        private static bool IsRewardBroken(RewardConfig reward, HashSet<string> resourceKeys,
            Dictionary<string, ItemConfig> items, HashSet<string> heroKeys)
            => reward.Type?.ToLowerInvariant() switch
            {
                null => true,
                "resource" => reward.Key is null || !resourceKeys.Contains(reward.Key),
                "hero" => reward.Key is null || !heroKeys.Contains(reward.Key),
                "item" => reward.Key is null || items.GetValueOrDefault(reward.Key) is not { Slot: null },
                "equipment" => reward.Key is null || items.GetValueOrDefault(reward.Key) is not { Slot: not null },
                _ => false
            };

        /// <summary>Банери: категорія, пул, пороги pity й посилання лотів.</summary>
        private static void ValidateBanners(GameConfig config)
        {
            if (config.Shop.Banners.Count == 0)
                return;

            RequireUniqueKeys(config.Shop.Banners.Select(b => b.Key), "Shop.Banners");

            var resourceKeys = config.Resources.Select(r => r.Key).ToHashSet();
            var items = config.Items.ToDictionary(i => i.Key);
            var heroes = config.Heroes.ToDictionary(h => h.Key);
            var heroKeys = heroes.Keys.ToHashSet();

            foreach (var banner in config.Shop.Banners)
            {
                if (!Enum.IsDefined(banner.Kind))
                    throw new InvalidOperationException($"Banner '{banner.Key}' has no Kind.");

                if (string.IsNullOrWhiteSpace(banner.PityGroup))
                    throw new InvalidOperationException(
                        $"Banner '{banner.Key}' has no PityGroup — pity carries between banners of one group, "
                        + "so an empty group would reset progress with every new banner.");

                if (banner.PriceGems < 1)
                    throw new InvalidOperationException($"Banner '{banner.Key}' needs a positive PriceGems.");

                if (banner.RarePity < 1 || banner.UniquePity < 1)
                    throw new InvalidOperationException($"Banner '{banner.Key}' has a non-positive pity threshold.");

                if (banner.RarePity >= banner.UniquePity)
                    throw new InvalidOperationException(
                        $"Banner '{banner.Key}' has RarePity {banner.RarePity} at or above UniquePity "
                        + $"{banner.UniquePity} — the rare guarantee would never pay out on its own.");

                if (banner.StartsAt is { } start && banner.EndsAt is { } end && start >= end)
                    throw new InvalidOperationException($"Banner '{banner.Key}' ends before it starts.");

                RequireUniqueKeys(banner.Drops.Select(d => d.Key), $"Banner '{banner.Key}' drops");

                // Гарантія платить у категорії банера, тож пул цієї категорії
                // мусить мати чим заплатити на обох порогах
                foreach (var rarity in new[] { Rarity.Rare, Rarity.Unique })
                    if (!banner.Drops.Any(d => BannerRoller.Counts(banner, d) && d.Rarity >= rarity))
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' has no {rarity} {banner.Kind} drop — its pity could never pay out.");

                if (banner.PriceGems <= 0 && banner.PriceSeals <= 0)
                    throw new InvalidOperationException($"Banner '{banner.Key}' has no price in gems nor in seals.");

                // Стандартний банер — без промо за визначенням: рівні шанси на будь-кого
                if (banner.Kind == BannerKind.Standard && banner.FeaturedKey is not null)
                    throw new InvalidOperationException($"Banner '{banner.Key}' is standard and cannot feature a drop.");

                if (banner.FeaturedKey is { } featuredKey)
                {
                    var featured = banner.Drops.FirstOrDefault(d => d.Key == featuredKey)
                        ?? throw new InvalidOperationException(
                            $"Banner '{banner.Key}' points at a featured drop '{featuredKey}' that is not in its pool.");

                    if (featured.Rarity != Rarity.Unique || featured.Kind != banner.Kind)
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' has a featured drop that is not a unique {banner.Kind} — "
                            + "50/50 applies to the banner's own category only.");
                }

                foreach (var drop in banner.Drops)
                {
                    if (drop.Kind is { } kind && !Enum.IsDefined(kind))
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' has an unknown Kind.");

                    if (drop.Weight < 1)
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' has no weight and could never come out.");

                    if (drop.Rewards.Count == 0)
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' grants nothing.");

                    var broken = drop.Rewards
                        .Where(r => IsRewardBroken(r, resourceKeys, items, heroKeys))
                        .Select(r => $"{r.Type} '{r.Key ?? "(no key)"}'")
                        .ToList();

                    if (broken.Count > 0)
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' references unknown keys "
                            + $"or keys of the wrong kind: {string.Join("; ", broken)}.");

                    // Лот чужої категорії — це філер. Рідкісний філер виглядав би
                    // як виплата гарантії, якою він не є
                    if (!BannerRoller.Counts(banner, drop) && drop.Rarity != Rarity.Common)
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' is a {drop.Rarity} filler — "
                            + "only the banner's own category carries rare and unique drops.");

                    if (drop.Kind == BannerKind.Hero
                        && !drop.Rewards.Any(r => string.Equals(r.Type, "Hero", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' is marked as a hero drop but grants no hero.");

                    if (drop.Kind == BannerKind.Weapon
                        && !drop.Rewards.Any(r => string.Equals(r.Type, "Equipment", StringComparison.OrdinalIgnoreCase)
                            && r.Key is not null
                            && items.GetValueOrDefault(r.Key) is { Slot: EquipmentSlot.Weapon }))
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' is marked as a weapon drop but grants no weapon.");

                    // Рідкість лота — обіцянка гравцю; предмет в інвентарі несе рідкість зі свого конфіга.
                    // Розбіжність означала б «унікальний» на вітрині і «звичайний» у руках
                    foreach (var reward in drop.Rewards.Where(r =>
                                 string.Equals(r.Type, "Equipment", StringComparison.OrdinalIgnoreCase) && r.Key is not null))
                    {
                        if (items.TryGetValue(reward.Key!, out var equipment) && equipment.Rarity != drop.Rarity)
                            throw new InvalidOperationException(
                                $"Banner '{banner.Key}' drop '{drop.Key}' is {drop.Rarity}, but item '{reward.Key}' is {equipment.Rarity} in Items.");
                    }

                    // Звичайні герої купуються за золото в залі (§6.1).
                    // У пулі за gems вони перетворили б банер на лотерею із золотим дном
                    var commonHero = drop.Rewards
                        .Where(r => string.Equals(r.Type, "Hero", StringComparison.OrdinalIgnoreCase) && r.Key is not null)
                        .FirstOrDefault(r => heroes[r.Key!].Rank == Rarity.Common);

                    if (commonHero is not null)
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' grants the common hero '{commonHero.Key}' — "
                            + "banners carry rare and unique heroes only.");
                }
            }
        }

        /// <summary>
        /// Мінімальна відстань між центрами будівель на плані за будь-якою віссю.
        /// Клієнт малює будівлю квадратом із півстороною до 6, тож два силуети
        /// не перетинаються, коли центри рознесені щонайменше на 12.
        /// </summary>
        private const double MinBuildingSpacing = 12;

        /// <summary>Будівлі на плані села не налазять одна на одну.</summary>
        private static void ValidateBuildingLayout(GameConfig config)
        {
            var placed = config.Buildings.Where(b => b.Position is not null).ToList();

            var collisions = placed
                .SelectMany((first, index) => placed.Skip(index + 1).Select(second => (first, second)))
                .Where(pair => Math.Max(
                    Math.Abs(pair.first.Position!.X - pair.second.Position!.X),
                    Math.Abs(pair.first.Position.Y - pair.second.Position.Y)) < MinBuildingSpacing)
                .Select(pair => $"{pair.first.Key} ↔ {pair.second.Key}")
                .ToList();

            if (collisions.Count > 0)
                throw new InvalidOperationException(
                    $"Buildings are placed closer than {MinBuildingSpacing} on the village plan: {string.Join(", ", collisions)}.");
        }
    }
}
