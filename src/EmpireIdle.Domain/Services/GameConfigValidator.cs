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
            ValidateMarket(config);
            ValidateBeasts(config);
            ValidateTeleports(config);
            ValidateSpeedUpItems(config);
            ValidateWeaponChests(config);
            ValidateLocalization(config);
            ValidateShopItems(config);
            ValidateEquipment(config);
            ValidateBanners(config);
            ValidateBuildingLayout(config);
            ValidateLoginRewards(config);
            ValidateClanTerritory(config);
            ValidateScouting(config);
            ValidateFlatBuildings(config);
            ValidateMonsters(config);
            ValidateTierGates(config);
        }

        /// <summary>
        /// Тір героя відкривається з рівнем світу (GDD §6.1): банер не може видати героя
        /// вищого тіру, ніж світ, з якого банер відкритий, а вікно продажу предмета апу
        /// не може пережити свій рівень світу — інакше його ніколи не закрила б ця модель.
        /// </summary>
        private static void ValidateTierGates(GameConfig config)
        {
            var heroes = config.Heroes.ToDictionary(h => h.Key);

            var early = config.Shop.Banners
                .SelectMany(b => b.Drops
                    .SelectMany(d => d.Rewards)
                    .Where(r => r.Type is "Hero" or "HeroShards" && r.Key is not null && heroes.TryGetValue(r.Key, out var hero)
                                && hero.NativeTier > b.RequiresServerLevel)
                    .Select(r => $"{b.Key} → {r.Key} (tier {heroes[r.Key!].NativeTier}, banner opens at {b.RequiresServerLevel})"))
                .ToList();

            if (early.Count > 0)
                throw new InvalidOperationException(
                    $"Banners hand out heroes above their world level: {string.Join(", ", early)}.");

            var badWindows = config.Shop.Items
                .Where(i => i.ServerLevel is { } level
                            && (level < 1 || i.WindowDays <= 0 || i.WindowDays > config.Map.Evolution.DaysPerLevel))
                .Select(i => $"{i.ItemKey} (level {i.ServerLevel}, {i.WindowDays} days)")
                .ToList();

            if (badWindows.Count > 0)
                throw new InvalidOperationException(
                    $"Shop windows need a world level ≥ 1 and 1–{config.Map.Evolution.DaysPerLevel} days "
                    + $"(no longer than a world level): {string.Join(", ", badWindows)}.");
        }

        /// <summary>
        /// Склад і нагорода монстра. Дубль юніта в загоні розколов би армію на два стеки
        /// одного типу, невідомий юніт дав би нульову силу, невідомий ресурс — нагороду,
        /// яку нікуди покласти.
        /// </summary>
        private static void ValidateMonsters(GameConfig config)
        {
            var unitKeys = config.Units.Select(u => u.Key).ToHashSet();
            var resourceKeys = config.Resources.Select(r => r.Key).ToHashSet();

            foreach (var monster in config.Monsters)
            {
                var duplicates = monster.Units
                    .GroupBy(u => u.UnitType)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicates.Count > 0)
                    throw new InvalidOperationException(
                        $"Monster '{monster.Key}' lists units more than once: {string.Join(", ", duplicates)}.");

                if (monster.Units.Any(u => u.Count <= 0))
                    throw new InvalidOperationException($"Monster '{monster.Key}' has a unit stack with non-positive Count.");

                // Порожній список юнітів чи ресурсів — фікстура їх не описує; перевіряємо лише задане
                var unknownUnits = unitKeys.Count == 0
                    ? []
                    : monster.Units.Where(u => !unitKeys.Contains(u.UnitType)).Select(u => u.UnitType).ToList();

                if (unknownUnits.Count > 0)
                    throw new InvalidOperationException(
                        $"Monster '{monster.Key}' references unknown units: {string.Join(", ", unknownUnits)}.");

                var brokenRewards = monster.Rewards
                    .Where(r => r.Amount <= 0 || (resourceKeys.Count > 0 && !resourceKeys.Contains(r.Resource)))
                    .Select(r => $"{r.Resource} ×{r.Amount}")
                    .ToList();

                if (brokenRewards.Count > 0)
                    throw new InvalidOperationException(
                        $"Monster '{monster.Key}' rewards unknown resources or non-positive amounts: {string.Join(", ", brokenRewards)}.");
            }
        }

        /// <summary>
        /// Будівля без рівнів (GDD §3.1) назавжди стоїть на рівні 1. Усе, що росте з рівнем,
        /// у неї було б мертвим числом, ціна апгрейду — ціною того, чого не купиш, а гейт
        /// юніта чи квест на вищий рівень — недосяжною ціллю.
        /// </summary>
        private static void ValidateFlatBuildings(GameConfig config)
        {
            var flat = config.Buildings.Where(b => !b.Upgradable).ToList();

            var levelled = flat
                .Where(b => b.IsMainBuilding
                            || b.ProducesResource is not null
                            || b.StoresResources is { Count: > 0 }
                            || b.Cost.Count > 0
                            || b.WoundedCapacityPerLevel > 0
                            || b.DefenceBonusPerLevel > 0
                            || b.ReinforcementSlotsPerLevel > 0
                            || b.BeastCapacityPerLevel > 0
                            || b.ProtectedStorage > 0)
                .Select(b => b.Key)
                .ToList();

            if (levelled.Count > 0)
                throw new InvalidOperationException(
                    "Buildings without levels cannot be the main building, produce, store, cost an upgrade "
                    + $"or grow anything with level: {string.Join(", ", levelled)}.");

            var flatKeys = flat.Select(b => b.Key).ToHashSet();

            var unreachable = config.Units
                .Where(u => u.RequiresBuilding is { } key && flatKeys.Contains(key) && u.RequiresBuildingLevel > 1)
                .Select(u => $"unit {u.Key}")
                .Concat(config.Quests.SelectMany(q => q.Objectives
                    .Where(o => o.Type == "BuildingUpgradeCompleted" && o.Target is { } key && flatKeys.Contains(key))
                    .Select(_ => $"quest {q.Key}")))
                .ToList();

            if (unreachable.Count > 0)
                throw new InvalidOperationException(
                    $"These require a level of a building that has no levels: {string.Join(", ", unreachable)}.");
        }

        /// <summary>
        /// Розвідка: будівля, що відправляє розвідників, мусить існувати, розвідники — обганяти
        /// будь-який юніт, а завіса від
        /// розвідки — мати строк: предмет без тривалості списувався б, нічого не ховаючи.
        /// </summary>
        private static void ValidateScouting(GameConfig config)
        {
            var building = config.Combat.Scouting.RequiredBuilding;

            if (building is not null && config.Buildings.All(b => b.Key != building))
                throw new InvalidOperationException(
                    $"Combat.Scouting.RequiredBuilding '{building}' is not in Buildings.");

            // Розвідка має обганяти будь-яку армію, інакше вона запізнюється до власного нападу
            var fastest = config.Units.Select(u => u.Stats.GetValueOrDefault("Speed", 1.0)).DefaultIfEmpty(0).Max();

            if (building is not null && config.Combat.Scouting.Speed <= fastest)
                throw new InvalidOperationException(
                    $"Combat.Scouting.Speed ({config.Combat.Scouting.Speed}) must exceed the fastest unit ({fastest}).");

            foreach (var veil in config.Items.Where(i => i.Type == "scoutveil" && i.DurationHours <= 0))
                throw new InvalidOperationException($"Scout veil '{veil.Key}' has no DurationHours.");
        }

        /// <summary>
        /// Кланова територія й кланові квести (GDD §7.2). Квест клану — одна накопичувальна
        /// ціль без порогу й без особистих нагород: нагорода йде клану очками вкладу.
        /// Слот відкриває рівно одна умова, і квест у ній мусить бути клановим.
        /// </summary>
        private static void ValidateClanTerritory(GameConfig config)
        {
            var clanQuests = config.Quests.Where(q => q.Scope == QuestScope.Clan).ToList();

            var brokenQuests = clanQuests
                .Where(q => q.Objectives.Count != 1
                            || q.Objectives[0].Mode == ObjectiveMode.Threshold
                            || q.Objectives[0].Count <= 0
                            || q.Window != QuestWindow.Chain
                            || q.Rewards.Count > 0
                            || q.RewardTiers.Count > 0
                            || q.ClanPoints < 0)
                .Select(q => q.Key)
                .ToList();

            if (brokenQuests.Count > 0)
                throw new InvalidOperationException(
                    "Clan quests need exactly one accumulating objective with a positive count, the Chain window, " +
                    $"no personal or tiered rewards and non-negative ClanPoints: {string.Join(", ", brokenQuests)}.");

            var territory = config.Clan.Territory;

            if (territory.StartingSlots > territory.MaxStructures)
                throw new InvalidOperationException(
                    $"Clan territory opens {territory.StartingSlots} starting slots but allows only {territory.MaxStructures} structures.");

            var clanQuestKeys = clanQuests.Select(q => q.Key).ToHashSet();

            var brokenUnlocks = territory.SlotUnlocks
                .Select((unlock, index) => (Unlock: unlock, Index: index + 1))
                .Where(x => (x.Unlock.MinMembers is null) == (x.Unlock.QuestKey is null)
                            || x.Unlock.MinMembers is <= 0
                            || (x.Unlock.QuestKey is { } key && !clanQuestKeys.Contains(key)))
                .Select(x => $"#{x.Index}")
                .ToList();

            if (brokenUnlocks.Count > 0)
                throw new InvalidOperationException(
                    "Every clan slot unlock needs exactly one condition — a positive MinMembers or the key of a clan quest: " +
                    $"{string.Join(", ", brokenUnlocks)}.");
        }

        /// <summary>Нагороди за вхід видає той самий диспетчер, що й квестові — ключі мусять існувати.</summary>
        private static void ValidateLoginRewards(GameConfig config)
        {
            var login = config.LoginRewards;

            var rewards = login.Daily
                .SelectMany((day, index) => day.Rewards.Select(r => (Where: $"day {index + 1}", Reward: r)))
                .Concat(login.Weekly.Select(r => (Where: "weekly", Reward: r)))
                .Concat(login.Monthly.Select(r => (Where: "monthly", Reward: r)));

            var resourceKeys = config.Resources.Select(r => r.Key).ToHashSet();
            var items = config.Items.ToDictionary(i => i.Key);
            var heroKeys = config.Heroes.Select(h => h.Key).ToHashSet();

            var broken = rewards
                .Where(x => x.Reward.Amount <= 0 || IsRewardBroken(x.Reward, resourceKeys, items, heroKeys))
                .Select(x => $"{x.Where} → {x.Reward.Type} '{x.Reward.Key ?? "(no key)"}' ×{x.Reward.Amount}")
                .ToList();

            if (broken.Count > 0)
                throw new InvalidOperationException(
                    $"Login rewards reference unknown keys, keys of the wrong kind or non-positive amounts: {string.Join("; ", broken)}.");

            // Порожній день розірвав би серію: гравець зайшов, а листа немає
            if (login.Daily.Any(day => day.Rewards.Count == 0))
                throw new InvalidOperationException("Every day of the login series needs at least one reward.");
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
            var resourceKeys = config.Resources.Select(r => r.Key).ToHashSet();

            foreach (var dungeon in dungeons.Dungeons)
            {
                // Як у монстрів: помилка в ключі ресурсу мовчки забрала б нагороду за забіг
                var brokenRewards = dungeon.Reward
                    .Where(r => r.Amount <= 0 || (resourceKeys.Count > 0 && !resourceKeys.Contains(r.Resource)))
                    .Select(r => $"{r.Resource} ×{r.Amount}")
                    .ToList();

                if (brokenRewards.Count > 0)
                    throw new InvalidOperationException(
                        $"Dungeon '{dungeon.Key}' rewards unknown resources or non-positive amounts: {string.Join(", ", brokenRewards)}.");

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

            ValidateArtifactSets(config);
        }

        /// <summary>
        /// Родини наборів: кожен данж має опис свого набору, рівень у межах
        /// множників, характерні стати — з пулу. Інакше артефакт тихо
        /// ролився б без рівня, а «характер» — у стат, якого не існує.
        /// </summary>
        private static void ValidateArtifactSets(GameConfig config)
        {
            var equipment = config.Equipment;

            RequireUniqueKeys(equipment.ArtifactSets.Select(s => s.Key), "Equipment.ArtifactSets");

            var families = equipment.ArtifactSets.ToDictionary(s => s.Key);

            var undescribed = config.Dungeons.Dungeons
                .Where(d => !families.ContainsKey(d.ArtifactSetKey))
                .Select(d => d.ArtifactSetKey)
                .Distinct()
                .ToList();

            if (undescribed.Count > 0)
                throw new InvalidOperationException(
                    $"Dungeon artifact sets have no Equipment.ArtifactSets entry: {string.Join(", ", undescribed)}.");

            var tiers = Math.Max(1, equipment.ArtifactTierMultipliers.Count);

            var badTiers = equipment.ArtifactSets
                .Where(s => s.Tier < 1 || s.Tier > tiers)
                .Select(s => $"{s.Key} ({s.Tier})")
                .ToList();

            if (badTiers.Count > 0)
                throw new InvalidOperationException(
                    $"Equipment.ArtifactSets have tiers outside 1–{tiers}: {string.Join(", ", badTiers)}.");
        }

        /// <summary>
        /// Ринок: будівля існує, кожен товар має якір ціни. Без якоря коридор
        /// рахувався б із першого ж продажу, і його задавав би вош.
        /// </summary>
        /// <summary>
        /// Звірі (GDD §5.10): кожен тип приручається з наявного монстра, один звір на монстра,
        /// шанс у (0; 1], є звіринець із місцями й корм-предмет — інакше приручати нікуди й годувати нічим.
        /// </summary>
        private static void ValidateBeasts(GameConfig config)
        {
            var beasts = config.Beasts;

            if (beasts.Types.Count == 0)
                return;

            RequireUniqueKeys(beasts.Types.Select(b => b.Key), "Beasts.Types");
            RequireUniqueKeys(beasts.Types.Select(b => b.MonsterKey), "Beasts.Types monster keys");

            var monsterKeys = config.Monsters.Select(m => m.Key).ToHashSet();

            var broken = beasts.Types
                .Where(b => !monsterKeys.Contains(b.MonsterKey) || b.TameChance <= 0 || b.TameChance > 1)
                .Select(b => b.Key)
                .ToList();

            if (broken.Count > 0)
                throw new InvalidOperationException(
                    $"Beasts need an existing monster and a tame chance in (0; 1]: {string.Join(", ", broken)}.");

            if (beasts.TameChancePerPenLevel < 0 || beasts.MaxTameChanceMultiplier < 1
                || beasts.PityWins < 1 || beasts.MaxRank < 1)
                throw new InvalidOperationException(
                    "Beasts need a non-negative chance per pen level, a chance cap multiplier of at least 1, "
                    + "PityWins and MaxRank of at least 1.");

            if (beasts.LevelsPerRank < 1 || beasts.BaseExperience < 1 || beasts.ExperienceGrowth < 1)
                throw new InvalidOperationException(
                    "Beasts need LevelsPerRank and BaseExperience of at least 1 and ExperienceGrowth of at least 1.");

            // Пасивка мусить щось давати, діяти й мати перезарядку, не коротшу за дію: інакше вона діяла б завжди
            var brokenPassives = beasts.Types
                .Where(b => b.Effect is not (EffectTarget.Production or EffectTarget.Attack or EffectTarget.Defense
                                or EffectTarget.MarchSpeed or EffectTarget.Carry)
                            || b.BaseBonus <= 0 || b.BonusPerLevel < 0 || b.DurationMinutes <= 0
                            || b.CooldownMinutes < b.DurationMinutes || b.ActivationFood <= 0)
                .Select(b => b.Key)
                .ToList();

            if (brokenPassives.Count > 0)
                throw new InvalidOperationException(
                    "Beast passives need a multiplier effect, a positive bonus, a duration, a cooldown no shorter "
                    + $"than the duration and a food price: {string.Join(", ", brokenPassives)}.");

            // Корм — стаковий предмет: спорядження поштучне й годувати ним не можна
            if (config.Items.FirstOrDefault(i => i.Key == beasts.FeedItemKey) is not { Slot: null })
                throw new InvalidOperationException(
                    $"Beasts.FeedItemKey '{beasts.FeedItemKey}' must be a stackable item from Items.");

            if (!config.Buildings.Any(b => b.BeastCapacityPerLevel > 0))
                throw new InvalidOperationException("Beasts are configured, but no building gives beast slots.");
        }

        /// <summary>Скриня зброї без героїв чи без шматків нічого б не відкривала (GDD §6.4).</summary>
        private static void ValidateWeaponChests(GameConfig config)
        {
            var heroKeys = config.Heroes.Select(h => h.Key).ToHashSet();

            foreach (var chest in config.Items.Where(i => i.Type == "weaponchest"))
            {
                if (chest.WeaponShards < 1)
                    throw new InvalidOperationException($"Weapon chest '{chest.Key}' needs positive WeaponShards.");

                if (chest.WeaponHeroes.Count == 0 || chest.WeaponHeroes.Any(key => !heroKeys.Contains(key)))
                    throw new InvalidOperationException($"Weapon chest '{chest.Key}' needs known WeaponHeroes.");
            }
        }

        /// <summary>Прискорення без хвилин нічого б не зрізало — предмет-пустушка в рюкзаку.</summary>
        private static void ValidateSpeedUpItems(GameConfig config)
        {
            var broken = config.Items
                .Where(i => i.Type == "speedup" && i.SpeedUpMinutes <= 0)
                .Select(i => i.Key)
                .ToList();

            if (broken.Count > 0)
                throw new InvalidOperationException($"Speed-up items need positive SpeedUpMinutes: {string.Join(", ", broken)}.");
        }

        /// <summary>Телепорт ближнього переїзду без радіусу не переносив би нікуди (GDD §8.9).</summary>
        private static void ValidateTeleports(GameConfig config)
        {
            var broken = config.Items
                .Where(i => i.Type == "teleport" && i.TeleportScope == TeleportScope.Nearby && i.TeleportRange <= 0)
                .Select(i => i.Key)
                .ToList();

            if (broken.Count > 0)
                throw new InvalidOperationException($"Nearby teleports need a positive TeleportRange: {string.Join(", ", broken)}.");
        }

        private static void ValidateMarket(GameConfig config)
        {
            var market = config.Market;

            // Ринок вимкнений — і правил для нього немає
            if (market.BuildingKey is null)
                return;

            if (!config.Buildings.Any(b => b.Key == market.BuildingKey))
                throw new InvalidOperationException($"Market.BuildingKey '{market.BuildingKey}' is not a known building.");

            var equipmentInvalid = config.Items
                .Where(i => i.Tradeable && i.Type == "equipment")
                .Select(i => i.Key)
                .ToList();

            if (equipmentInvalid.Count > 0)
                throw new InvalidOperationException(
                    $"Equipment is always tradeable; Tradeable is for stack items only: {string.Join(", ", equipmentInvalid)}.");

            var pricing = new MarketPricing(config);

            var categories = config.Items
                .Where(i => i.Tradeable)
                .Select(i => MarketPricing.CategoryOfItem(i.Key))
                .Concat(config.Items
                    .Where(i => i.Slot is not null)
                    .Select(i => MarketPricing.CategoryOf(i.Slot!.Value)))
                .Distinct()
                .ToList();

            var unanchored = categories.Where(c => pricing.AnchorPerUnit(c) is null).ToList();

            if (unanchored.Count > 0)
                throw new InvalidOperationException(
                    $"Market has no price anchor for: {string.Join(", ", unanchored)} "
                    + "(stack items need a shop price in gems, equipment and heroes a Market.GoldPerPower entry).");
        }

        /// <summary>
        /// Мови: без дублікатів, мова за замовчуванням серед них, російської немає
        /// (GDD §7.3) — і не з'явиться випадково з чужого шаблону конфіга.
        /// </summary>
        private static void ValidateLocalization(GameConfig config)
        {
            var localization = config.Localization;

            RequireUniqueKeys(localization.Languages, "Localization.Languages");

            if (!localization.SupportedLanguages.Contains(localization.DefaultLanguage))
                throw new InvalidOperationException(
                    $"Localization.DefaultLanguage '{localization.DefaultLanguage}' is not among Localization.Languages.");

            if (localization.SupportedLanguages.Any(language => string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Localization.Languages must not include Russian (GDD §7.3).");

            var unsupported = config.Locales.Keys.Where(language => !localization.SupportedLanguages.Contains(language)).ToList();

            if (unsupported.Count > 0)
                throw new InvalidOperationException(
                    $"Locales for languages outside Localization.Languages: {string.Join(", ", unsupported)}.");

            // Переклад назви, якої немає, тихо нічого б не перекладав — і приховав би друкарську помилку в ключі
            var known = LocalizableKeys(config);

            var unknown = config.Locales
                .SelectMany(locale => locale.Value.Names.Keys
                    .Where(key => !known.Contains(key))
                    .Select(key => $"{locale.Key}: {key}"))
                .ToList();

            if (unknown.Count > 0)
                throw new InvalidOperationException($"Locales name unknown keys: {string.Join(", ", unknown)}.");
        }

        /// <summary>Ключі «розділ.ключ» усього, що має назву для гравця й може перекладатись.</summary>
        public static HashSet<string> LocalizableKeys(GameConfig config)
            => config.Buildings.Select(b => $"building.{b.Key}")
                .Concat(config.Heroes.Select(h => $"hero.{h.Key}"))
                .Concat(config.Items.Select(i => $"item.{i.Key}"))
                .Concat(config.Resources.Select(r => $"resource.{r.Key}"))
                .Concat(config.Units.Select(u => $"unit.{u.Key}"))
                .Concat(config.Monsters.Select(m => $"monster.{m.Key}"))
                .Concat(config.Beasts.Types.Select(b => $"beast.{b.Key}"))
                .Concat(config.Equipment.ArtifactSets.Select(s => $"artifactSet.{s.Key}"))
                .Concat(config.Equipment.ArtifactSlots.Select(s => $"artifactSlot.{s.Key}"))
                .ToHashSet();

        /// <summary>
        /// Жоден поріг відкриття не вищий за ратушу, яку взагалі можна
        /// збудувати (MaxBuildingLevel). Інакше вміст
        /// недосяжний назавжди, а гравець бачить замок, який не відімкнеться.
        /// </summary>
        private static void ValidateUnlockThresholds(GameConfig config)
        {
            var ceiling = config.MaxBuildingLevel;

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

            // Кожен вироблюваний ресурс має сховище: стелі складу немає (GDD §4.1),
            // але без сховища в ресурсу не було б захищеного запасу від грабунку
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

        /// <summary>Ростер героїв: класи, тіри, вартість прокачки, лікування, вміння.</summary>
        private static void ValidateHeroes(GameConfig config)
        {
            // Порожній ростер — конфіг героїв не описує (мінімальні фікстури в тестах).
            // Перевіряємо лише те, що задано.
            if (config.Heroes.Count == 0)
                return;

            var settings = config.HeroSettings;

            // Крива досвіду героя (GDD §6.1): без росту пізні рівні коштували б як перші
            if (settings.MaxLevel < 1)
                throw new InvalidOperationException("HeroSettings.MaxLevel must be at least 1.");

            if (settings.ExperienceBase <= 0 || settings.ExperienceExponent <= 0)
                throw new InvalidOperationException(
                    "HeroSettings.ExperienceBase and ExperienceExponent must be positive — levels would cost nothing.");

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

            // Тір має бути відчутним, а ап — не вигіднішим за рідного героя (GDD §6.1)
            if (settings.TierGrowth <= 1.0)
                throw new InvalidOperationException(
                    "HeroSettings.TierGrowth must be above 1 — otherwise a higher tier is not stronger.");

            if (settings.EvolutionPenalty is <= 0.0 or > 1.0)
                throw new InvalidOperationException(
                    "HeroSettings.EvolutionPenalty must be in (0, 1] — an evolved hero cannot outgrow a native one.");

            var badTiers = config.Heroes
                .Where(h => h.NativeTier < 1 || h.NativeTier > settings.MaxTier)
                .Select(h => $"{h.Key} ({h.NativeTier})")
                .ToList();

            if (badTiers.Count > 0)
                throw new InvalidOperationException(
                    $"Heroes have a NativeTier outside 1–{settings.MaxTier}: {string.Join(", ", badTiers)}.");

            var itemKeys = config.Items.Select(i => i.Key).ToHashSet();

            var unknownItems = settings.EvolutionItemKeys
                .Where(k => !itemKeys.Contains(k))
                .ToList();

            if (unknownItems.Count > 0)
                throw new InvalidOperationException(
                    $"HeroSettings.EvolutionItemKeys reference unknown items: {string.Join(", ", unknownItems)}.");

            // Надлишок понад стелю сузір'я стає печатками призову. Забутий ранг означав би,
            // що дублікат зникає без сліду — саме те, чого ми уникали.
            // Зірки (GDD §6.1): ціна кожної частинки, інакше заповнення впало б на першій дірці
            if (settings.SummonShards < 1)
                throw new InvalidOperationException("HeroSettings.SummonShards must be at least 1 — summoning would be free.");

            if (settings.MaxStars < 1 || settings.PartsPerStar < 1)
                throw new InvalidOperationException("HeroSettings.MaxStars and PartsPerStar must be at least 1.");

            if (settings.StarPartCosts.Count != settings.MaxStars
                || settings.StarPartCosts.Any(star => star.Count != settings.PartsPerStar || star.Any(cost => cost < 1)))
                throw new InvalidOperationException(
                    $"HeroSettings.StarPartCosts needs {settings.MaxStars} stars of {settings.PartsPerStar} positive part costs.");

            if (settings.StarPartBonus < 0)
                throw new InvalidOperationException("HeroSettings.StarPartBonus cannot be negative — a star never weakens a hero.");

            // Зброя героя (GDD §6.4): бонус на кожен рівень і не спадає — інакше прокачка послаблювала б героя
            if (settings.WeaponShardCosts.Any(cost => cost < 1))
                throw new InvalidOperationException("HeroSettings.WeaponShardCosts must be positive — a weapon level would be free.");

            foreach (var (rank, percents) in settings.WeaponBonusPercents)
                if (percents.Count != settings.WeaponShardCosts.Count
                    || percents.Any(p => p < 0)
                    || percents.Zip(percents.Skip(1)).Any(pair => pair.Second < pair.First))
                    throw new InvalidOperationException(
                        $"HeroSettings.WeaponBonusPercents[{rank}] needs {settings.WeaponShardCosts.Count} non-decreasing, non-negative values.");

            var rankNames = Enum.GetNames<Rarity>().ToHashSet();

            // Обмін на вищу рідкість: з найвищої міняти нікуди
            var badUpgrades = settings.UniversalShardUpgrade
                .Where(u => !rankNames.Contains(u.Key) || u.Key == Rarity.Unique.ToString() || u.Value < 1)
                .Select(u => $"{u.Key} ({u.Value})")
                .ToList();

            if (badUpgrades.Count > 0)
                throw new InvalidOperationException(
                    $"HeroSettings.UniversalShardUpgrade has invalid entries: {string.Join(", ", badUpgrades)}.");

            // Кожна рідкість ростеру — зі своїм універсальним осколком, інакше надлишок прокачаного героя нікуди подіти
            var universal = config.Items
                .Where(item => item.Type == "universalshard")
                .GroupBy(item => item.Rarity)
                .ToDictionary(g => g.Key, g => g.Count());

            var badUniversal = config.Heroes
                .Select(h => h.Rank)
                .Distinct()
                .Where(rank => universal.GetValueOrDefault(rank) != 1)
                .Select(rank => rank.ToString())
                .ToList();

            if (badUniversal.Count > 0)
                throw new InvalidOperationException(
                    "Every hero rarity needs exactly one universal shard item (Type = universalshard): "
                    + $"{string.Join(", ", badUniversal)}.");

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

            // Вміння (GDD §6.1, рішення 08.10.2026): стеля рівня й склад за рідкістю
            if (settings.MaxSkillLevel < 1)
                throw new InvalidOperationException("HeroSettings.MaxSkillLevel must be at least 1.");

            var badLayouts = settings.SkillLayouts
                .Where(l => !rankNames.Contains(l.Key)
                    || l.Value.Attack < 1 || l.Value.Defense < 0
                    || l.Value.Utility < 0 || l.Value.Utility > l.Value.Defense)
                .Select(l => l.Key)
                .ToList();

            if (badLayouts.Count > 0)
                throw new InvalidOperationException(
                    "HeroSettings.SkillLayouts needs a known rarity, at least the active skill in Attack "
                    + $"and no more utility skills than Defense holds: {string.Join(", ", badLayouts)}.");

            // Конвої (GDD §6.1): кожна роль веде відомий тип юнітів, крива росте від першого рівня героя
            var badRoles = settings.RoleUnits
                .Where(r => !settings.Classes.Contains(r.Key) || !unitKeys.Contains(r.Value))
                .Select(r => $"{r.Key} → {r.Value}")
                .ToList();

            if (badRoles.Count > 0)
                throw new InvalidOperationException(
                    $"HeroSettings.RoleUnits must map known classes to known units: {string.Join(", ", badRoles)}.");

            if (settings.RoleUnits.Count > 0 && settings.Classes.Any(c => !settings.RoleUnits.ContainsKey(c)))
                throw new InvalidOperationException(
                    "HeroSettings.RoleUnits must cover every hero class — a hero without a unit type could lead nobody.");

            if (settings.ConvoySize < 1)
                throw new InvalidOperationException("HeroSettings.ConvoySize must be at least 1.");

            var steps = settings.ConvoysByLevel;

            if (steps.Count > 0
                && (steps[0].Level != 1
                    || steps.Any(s => s.Convoys < 1 || s.Level > settings.MaxLevel)
                    || steps.Zip(steps.Skip(1)).Any(p => p.Second.Level <= p.First.Level || p.Second.Convoys < p.First.Convoys)))
                throw new InvalidOperationException(
                    "HeroSettings.ConvoysByLevel must start at hero level 1, rise in level up to MaxLevel "
                    + "and never lose convoys — a hero cannot lead fewer troops as he grows.");

            // Навчальний табір (GDD §6.1): опорна п'ятірка має бути, слоти — відкриватися по порядку
            var camp = settings.TrainingCamp;

            if (camp.ReferenceSize < 1)
                throw new InvalidOperationException("HeroSettings.TrainingCamp.ReferenceSize must be at least 1.");

            if (camp.FreeSlotTownHallLevels.Any(level => level < 1)
                || camp.FreeSlotTownHallLevels.Zip(camp.FreeSlotTownHallLevels.Skip(1)).Any(pair => pair.Second < pair.First))
                throw new InvalidOperationException(
                    "HeroSettings.TrainingCamp.FreeSlotTownHallLevels must be town hall levels from 1 up, never decreasing — "
                    + "a later slot cannot open before an earlier one.");

            if (camp.ExtraSlotPricesGems.Any(price => price < 1))
                throw new InvalidOperationException("HeroSettings.TrainingCamp.ExtraSlotPricesGems must all be positive.");

            if (camp.SlotCooldownHours < 0 || camp.SkipCooldownGems < 1)
                throw new InvalidOperationException(
                    "HeroSettings.TrainingCamp needs a non-negative SlotCooldownHours and a positive SkipCooldownGems.");

            foreach (var hero in config.Heroes)
            {
                if (hero.Speed is <= 0)
                    throw new InvalidOperationException($"Hero '{hero.Key}' has non-positive Speed — its marches would never arrive.");

                ValidateHeroSkills(hero, settings, unitKeys, statKeys);
            }

            ValidateSkillBooks(config);
        }

        /// <summary>
        /// Книги вмінь (GDD §6.1): кожна — предмет-книга своєї рідкості для відомої ролі, по одній
        /// на роль × рідкість × половину, і кожен герой має книги для обох своїх половин —
        /// інакше його вміння застрягли б на першому рівні.
        /// </summary>
        private static void ValidateSkillBooks(GameConfig config)
        {
            var books = config.HeroSettings.SkillBooks;

            // Книги не описані — мінімальні фікстури; у грі без книг вміння просто не качаються
            if (books.Count == 0)
                return;

            var items = config.Items.ToDictionary(i => i.Key);
            var classes = config.HeroSettings.Classes.ToHashSet();

            var broken = books
                .Where(b => !items.TryGetValue(b.ItemKey ?? string.Empty, out var item)
                    || item.Type != "skillbook" || item.Rarity != b.Rarity || !classes.Contains(b.Class))
                .Select(b => b.ItemKey)
                .ToList();

            if (broken.Count > 0)
                throw new InvalidOperationException(
                    "HeroSettings.SkillBooks must point at skillbook items of the same rarity for a known class: "
                    + $"{string.Join(", ", broken)}.");

            var duplicates = books
                .GroupBy(b => (b.Class, b.Rarity, b.Half))
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.Class}/{g.Key.Rarity}/{g.Key.Half}")
                .ToList();

            if (duplicates.Count > 0)
                throw new InvalidOperationException(
                    $"HeroSettings.SkillBooks has more than one book for: {string.Join(", ", duplicates)}.");

            var missing = config.Heroes
                .SelectMany(h => h.Skills.Select(s => s.Half).Distinct().Select(half => (Hero: h, Half: half)))
                .Where(x => !books.Any(b => b.Class == x.Hero.Class && b.Rarity == x.Hero.Rank && b.Half == x.Half))
                .Select(x => $"{x.Hero.Key} ({x.Half})")
                .ToList();

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"Heroes without a skill book for their skill half: {string.Join(", ", missing)}.");
        }

        /// <summary>
        /// Вміння одного героя. Саме вони, а не стати, рухають армійську формулу й бій у данжі,
        /// тож описка в цілі, статі чи довжині списку мовчки знеструмила б героя.
        /// </summary>
        private static void ValidateHeroSkills(HeroConfig hero, HeroesConfig settings,
            IReadOnlySet<string> unitKeys, IReadOnlySet<string> statKeys)
        {
            RequireUniqueKeys(hero.Skills.Select(s => s.Key).ToList(), $"Heroes['{hero.Key}'].Skills");

            foreach (var skill in hero.Skills)
            {
                var name = $"Hero '{hero.Key}' skill '{skill.Key}'";

                if (skill.UnlockLevel < 1 || skill.UnlockLevel > settings.MaxLevel)
                    throw new InvalidOperationException(
                        $"{name} unlocks at hero level {skill.UnlockLevel}, outside 1..{settings.MaxLevel}.");

                // Кожен вид має рівно свої частини: бойове без бонусу війську нічого не дало б у марші,
                // небойове з ним стало б бойовим
                var parts = skill.Kind switch
                {
                    SkillKind.Active or SkillKind.Periodic => skill.Troops is not null && skill.Battle is not null && skill.Utility is null,
                    SkillKind.Passive => skill.Troops is not null && skill.Battle is null && skill.Utility is null,
                    SkillKind.Utility => skill.Utility is not null && skill.Troops is null && skill.Battle is null,
                    _ => false,
                };

                if (!parts)
                    throw new InvalidOperationException(
                        $"{name} ({skill.Kind}) has the wrong parts: Active and Periodic need Troops and Battle, "
                        + "Passive only Troops, Utility only Utility.");

                // Без активного з першого рівня новий герой у данжі б'є лише звичайним ударом
                if (skill.Kind == SkillKind.Active && (skill.Half != SkillHalf.Attack || skill.UnlockLevel != 1))
                    throw new InvalidOperationException($"{name} is active — it belongs to the Attack half and unlocks at level 1.");

                if (skill.Kind == SkillKind.Utility && skill.Half != SkillHalf.Defense)
                    throw new InvalidOperationException($"{name} is a utility skill — it belongs to the Defense half.");

                if (skill.Troops is { } troops)
                {
                    if (troops.Target != HeroCombatModifiers.AllUnits && !unitKeys.Contains(troops.Target))
                        throw new InvalidOperationException($"{name} targets unknown unit '{troops.Target}'.");

                    if (!statKeys.Contains(troops.Stat ?? string.Empty))
                        throw new InvalidOperationException(
                            $"{name} affects unknown stat '{troops.Stat}' — combat knows Attack and Defense.");

                    RequireLevels(troops.Percents, settings, $"{name} Troops.Percents", allowZero: true);
                }

                if (skill.Utility is { } utility)
                {
                    if (!SkillUtilityConfig.KnownEffects.Contains(utility.Effect ?? string.Empty))
                        throw new InvalidOperationException(
                            $"{name} has unknown utility effect '{utility.Effect}' — known: "
                            + $"{string.Join(", ", SkillUtilityConfig.KnownEffects)}.");

                    RequireLevels(utility.Percents, settings, $"{name} Utility.Percents", allowZero: true);
                }

                if (skill.Battle is { } battle)
                {
                    if (battle.Cooldown < 1)
                        throw new InvalidOperationException($"{name} has Cooldown {battle.Cooldown} — it needs at least one turn.");

                    if (battle.DamageMultiplier <= 0 && battle.HealPercent <= 0 && battle.ShieldPercent <= 0 && battle.Status is null)
                        throw new InvalidOperationException($"{name} does nothing in battle — no damage, heal, shield or status.");

                    // Щит без тривалості згасає в тому ж ході, в якому його наклали: стан тікає
                    // наприкінці ходу носія, тож на себе потрібно щонайменше 2
                    if (battle.ShieldPercent > 0 && battle.ShieldTurns < (battle.Target == AbilityTarget.Self ? 2 : 1))
                        throw new InvalidOperationException($"{name} shield vanishes before it can absorb anything — raise ShieldTurns.");

                    RequireLevels(battle.LevelScale, settings, $"{name} Battle.LevelScale", allowZero: false);
                }
            }

            if (hero.Skills.Count(s => s.Kind == SkillKind.Active) > 1)
                throw new InvalidOperationException($"Hero '{hero.Key}' has more than one active skill.");

            // Склад за рідкістю перевіряється, лише якщо рідкість описана — так фікстури тримають
            // героїв з одним-двома вміннями
            if (!settings.SkillLayouts.TryGetValue(hero.Rank.ToString(), out var layout))
                return;

            var attack = hero.Skills.Count(s => s.Half == SkillHalf.Attack);
            var defense = hero.Skills.Count(s => s.Half == SkillHalf.Defense);
            var utilities = hero.Skills.Count(s => s.Kind == SkillKind.Utility);
            var actives = hero.Skills.Count(s => s.Kind == SkillKind.Active);

            if (attack != layout.Attack || defense != layout.Defense || utilities != layout.Utility || actives != 1)
                throw new InvalidOperationException(
                    $"Hero '{hero.Key}' ({hero.Rank}) needs {layout.Attack} attack skills with one active, "
                    + $"{layout.Defense} defense skills of which {layout.Utility} utility — has {attack} attack "
                    + $"({actives} active) and {defense} defense ({utilities} utility).");
        }

        /// <summary>Пер-рівневий список: рівно MaxSkillLevel значень, жодне не від'ємне (і не нульове, якщо так сказано).</summary>
        private static void RequireLevels(IReadOnlyList<double> values, HeroesConfig settings, string name, bool allowZero)
        {
            if (values.Count != settings.MaxSkillLevel || values.Any(v => v < 0 || (!allowZero && v == 0)))
                throw new InvalidOperationException(
                    $"{name} needs {settings.MaxSkillLevel} {(allowZero ? "non-negative" : "positive")} values, one per skill level.");
        }

        /// <summary>Спорядження: слоти, класи зброї, набори, ціни.</summary>
        private static void ValidateEquipment(GameConfig config)
        {
            var equipment = config.Items.Where(i => i.Type == "equipment").ToList();

            if (equipment.Count == 0)
                return;

            var slots = config.Equipment.ArtifactSlots;

            if (slots.Count < 1)
                throw new InvalidOperationException("Equipment.ArtifactSlots must list at least one slot.");

            RequireUniqueKeys(slots.Select(s => s.Key), "Equipment.ArtifactSlots");

            // Артефакт без відомого типу слота нікуди не вдягнути
            var slotless = equipment
                .Where(i => i.Slot == EquipmentSlot.Artifact && config.Equipment.ArtifactSlotIndex(i.ArtifactSlot) is null)
                .Select(i => $"{i.Key} ({i.ArtifactSlot ?? "none"})")
                .ToList();

            if (slotless.Count > 0)
                throw new InvalidOperationException(
                    $"Artifacts without a known ArtifactSlot: {string.Join(", ", slotless)}.");

            if (config.Equipment.MaxLevel < 1 || config.Equipment.MaxMastery < 0)
                throw new InvalidOperationException("Equipment.MaxLevel must be at least 1 and MaxMastery non-negative.");


            // Рівень качається згодовуванням (GDD §6.4): без досвіду за рідкість артефакт не вирости
            if (config.Equipment.FeedExperience.Count == 0 || config.Equipment.FeedExperience.Values.Any(xp => xp < 1))
                throw new InvalidOperationException("Equipment.FeedExperience needs positive experience for fed rarities.");

            if (!config.Buildings.Select(b => b.Key).Contains(config.Equipment.ForgeBuildingKey))
                throw new InvalidOperationException(
                    $"Equipment.ForgeBuildingKey '{config.Equipment.ForgeBuildingKey}' is not a known building.");

            ValidateArtifactStats(config, equipment);

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

                // Герой носить по одному артефакту кожного типу: частини одного
                // слота не вдягнути разом, тож рахуються лише різні слоти
                var wearable = config.Items
                    .Where(i => i.SetKey == bonus.SetKey)
                    .Select(i => i.ArtifactSlot)
                    .Distinct()
                    .Count();

                if (bonus.RequiredPieces > wearable)
                    throw new InvalidOperationException(
                        $"Set bonus '{bonus.SetKey}' needs {bonus.RequiredPieces} pieces but they fit only "
                        + $"{wearable} distinct artifact slots.");

                if (bonus.Stats.Count == 0)
                    throw new InvalidOperationException($"Set bonus '{bonus.SetKey}' grants nothing.");
            }

            foreach (var item in equipment)
            {
                if (item.Slot is null)
                    throw new InvalidOperationException($"Item '{item.Key}' is equipment but has no Slot.");

                if (item.Slot == EquipmentSlot.Artifact && item.BaseStats.Count > 0)
                    throw new InvalidOperationException(
                        $"Artifact '{item.Key}' has BaseStats — artifact base comes from Equipment.ArtifactBase.");
            }
        }

        /// <summary>
        /// Стати артефактів (GDD §9.12): база для кожної рідкості, що трапляється, сталі бонуси —
        /// лише базові стати, пули — з відомих бонусів і не менше двох, ступенів стільки ж,
        /// скільки шансів, і шанси заточки покривають усю стелю.
        /// </summary>
        private static void ValidateArtifactStats(GameConfig config, IReadOnlyCollection<ItemConfig> equipment)
        {
            var settings = config.Equipment;

            var baseless = equipment
                .Where(i => i.Slot == EquipmentSlot.Artifact && !settings.ArtifactBase.ContainsKey(i.Rarity))
                .Select(i => i.Rarity)
                .Distinct()
                .ToList();

            if (baseless.Count > 0)
                throw new InvalidOperationException(
                    $"Equipment.ArtifactBase has no entry for rarities: {string.Join(", ", baseless)}.");

            var unknownClasses = settings.ArtifactClassMultipliers.Keys
                .Where(c => !config.HeroSettings.Classes.Contains(c))
                .ToList();

            if (unknownClasses.Count > 0)
                throw new InvalidOperationException(
                    $"Equipment.ArtifactClassMultipliers name unknown hero classes: {string.Join(", ", unknownClasses)}.");

            RequireUniqueKeys(settings.ArtifactBonuses.Select(b => b.Stat), "Equipment.ArtifactBonuses");

            // Ступенів менше, ніж шансів, — і кидок вийде за межі списку
            var badSteps = settings.ArtifactBonuses
                .Where(b => b.Steps.Count != settings.BonusStepChances.Count
                            || b.Steps.Any(v => v < 0)
                            || b.Steps.Zip(b.Steps.Skip(1)).Any(pair => pair.Second < pair.First))
                .Select(b => b.Stat)
                .ToList();

            if (badSteps.Count > 0)
                throw new InvalidOperationException(
                    $"Equipment.ArtifactBonuses need {settings.BonusStepChances.Count} non-decreasing, non-negative steps: "
                    + string.Join(", ", badSteps));

            var bonuses = settings.ArtifactBonuses.Select(b => b.Stat).ToHashSet();

            foreach (var slot in settings.ArtifactSlots)
            {
                if (slot.FixedBonuses.Count != 2 || slot.FixedBonuses.Distinct().Count() != 2)
                    throw new InvalidOperationException($"Artifact slot '{slot.Key}' needs two distinct FixedBonuses.");

                // Сталий бонус підсилює базу предмета — інший стат йому нема чого підсилювати
                var badFixed = slot.FixedBonuses
                    .Where(stat => !ArtifactStats.BaseStats.Contains(stat) || !bonuses.Contains(stat))
                    .ToList();

                if (badFixed.Count > 0)
                    throw new InvalidOperationException(
                        $"Artifact slot '{slot.Key}' FixedBonuses must be configured base stats: {string.Join(", ", badFixed)}.");

                var pool = slot.RandomBonuses.Select(e => e.Stat).ToList();

                // Два випадкові бонуси без повтору — у пулі має бути з чого обрати другий
                if (pool.Count < 2 || pool.Distinct().Count() != pool.Count)
                    throw new InvalidOperationException(
                        $"Artifact slot '{slot.Key}' needs at least two distinct RandomBonuses.");

                var badPool = slot.RandomBonuses
                    .Where(e => e.Weight <= 0 || !bonuses.Contains(e.Stat) || ArtifactStats.BaseStats.Contains(e.Stat))
                    .Select(e => e.Stat)
                    .ToList();

                if (badPool.Count > 0)
                    throw new InvalidOperationException(
                        $"Artifact slot '{slot.Key}' RandomBonuses must be configured hero bonuses with positive weight: "
                        + string.Join(", ", badPool));
            }

            if (settings.MasterySuccessChances.Count < settings.MaxMastery)
                throw new InvalidOperationException(
                    $"Equipment.MasterySuccessChances lists {settings.MasterySuccessChances.Count} attempts, "
                    + $"but MaxMastery is {settings.MaxMastery}.");

            // Порожній список — поломок немає взагалі; неповний означав би тиху «останню» поломку на пізніх рангах
            if (settings.MasteryBreakChances.Count > 0 && settings.MasteryBreakChances.Count < settings.MaxMastery)
                throw new InvalidOperationException(
                    $"Equipment.MasteryBreakChances lists {settings.MasteryBreakChances.Count} attempts, "
                    + $"but MaxMastery is {settings.MaxMastery}.");

            // Ремкомплект, якого немає в предметах, не видати й не витратити — ремонт лишився б лише за gems мовчки
            if (!config.Items.Any(i => i.Key == settings.RepairKitItemKey))
                throw new InvalidOperationException(
                    $"Equipment.RepairKitItemKey '{settings.RepairKitItemKey}' is not a known item.");
        }

        /// <summary>Чи посилається нагорода на неіснуючий ключ або на ключ не того виду.</summary>
        private static bool IsRewardBroken(RewardConfig reward, HashSet<string> resourceKeys,
            Dictionary<string, ItemConfig> items, HashSet<string> heroKeys)
            => reward.Type?.ToLowerInvariant() switch
            {
                null => true,
                "resource" => reward.Key is null || !resourceKeys.Contains(reward.Key),
                "hero" or "heroshards" => reward.Key is null || !heroKeys.Contains(reward.Key),
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

                    // Банер видає осколки героя (GDD §6.1) — 10 осколків це герой; цілий герой теж годиться
                    if (drop.Kind == BannerKind.Hero
                        && !drop.Rewards.Any(r => string.Equals(r.Type, "Hero", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(r.Type, "HeroShards", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' is marked as a hero drop but grants no hero.");

                    // Лот категорії — це те, чим платить гарантія й 50/50, тож він мусить давати цілого героя.
                    // Пачки осколків менші за призов — звичайний лут (GDD §6.1, рішення 08.10.2026)
                    var partial = drop.Rewards
                        .Where(r => string.Equals(r.Type, "HeroShards", StringComparison.OrdinalIgnoreCase)
                            && r.Amount < config.HeroSettings.SummonShards)
                        .ToList();

                    if (drop.Kind == BannerKind.Hero && partial.Count > 0)
                        throw new InvalidOperationException(
                            $"Banner '{banner.Key}' drop '{drop.Key}' is a hero drop with fewer than "
                            + $"{config.HeroSettings.SummonShards} shards — a pity payout must be a whole hero; "
                            + "smaller bundles go in as filler (no Kind).");

                    // Рідкість лота — обіцянка гравцю; предмет в інвентарі несе рідкість зі свого конфіга.
                    // Розбіжність означала б «унікальний» на вітрині і «звичайний» у руках
                    foreach (var reward in drop.Rewards.Where(r =>
                                 string.Equals(r.Type, "Equipment", StringComparison.OrdinalIgnoreCase) && r.Key is not null))
                    {
                        if (items.TryGetValue(reward.Key!, out var equipment) && equipment.Rarity != drop.Rarity)
                            throw new InvalidOperationException(
                                $"Banner '{banner.Key}' drop '{drop.Key}' is {drop.Rarity}, but item '{reward.Key}' is {equipment.Rarity} in Items.");
                    }
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
