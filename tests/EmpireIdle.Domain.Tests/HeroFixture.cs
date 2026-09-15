using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests
{
    /// <summary>
    /// Мінімальний конфіг героїв, що проходить валідатор, і збірка бонусів
    /// через нього.
    ///
    /// Один тір навмисно: тоді не потрібні ні предмети еволюції, ні
    /// зростання множників, і в фікстурі лишається тільки те, що тести
    /// справді перевіряють.
    ///
    /// StackBuff вручну не складається — його конструктор внутрішній, і це
    /// правильно: тест мусить іти тим самим шляхом, що й бій, від пасивки
    /// в конфізі до множника.
    /// </summary>
    public static class HeroFixture
    {
        public const string Buffer = "buffer";
        public const string Plain = "plain_hero";
        public const string Forge = "forge";
        public const string Weapon = "sword_iron";
        public const string SetPiece1 = "dawn_amulet";
        public const string SetPiece2 = "dawn_ring";
        public const string SetPiece3 = "dawn_sigil";
        public const string SetPiece4 = "dawn_chime";
        public const string LooseArtifact = "lone_charm";
        public const string SetKey = "dawn";

        public const double SetBonusAttack = 25;

        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Конфіг із заданими пасивками в героя <see cref="Buffer"/>.
        /// Другий герой, <see cref="Plain"/>, лишається без пасивок —
        /// він потрібен для перевірки «бонусу немає».
        /// </summary>
        public static GameConfig Config(params HeroPassiveConfig[] passives) => new()
        {
            Buildings =
            [
                new BuildingConfig { Key = "townhall", IsMainBuilding = true },
                new BuildingConfig { Key = "heroeshall" },
                new BuildingConfig { Key = "hospital" },
                new BuildingConfig { Key = Forge }
            ],
            Items =
            [
                new ItemConfig { Key = "hero_essence_t2" },
                new ItemConfig { Key = "hero_essence_t3" },
                new ItemConfig
                {
                    Key = Weapon,
                    Type = "equipment",
                    Slot = EquipmentSlot.Weapon,
                    WeaponClasses = ["warrior"],
                    BaseStats = new Dictionary<string, double> { ["Attack"] = 12 },
                    PriceGold = 500
                },
                new ItemConfig { Key = SetPiece1, Type = "equipment", Slot = EquipmentSlot.Artifact, SetKey = SetKey },
                new ItemConfig { Key = SetPiece2, Type = "equipment", Slot = EquipmentSlot.Artifact, SetKey = SetKey },
                new ItemConfig { Key = SetPiece3, Type = "equipment", Slot = EquipmentSlot.Artifact, SetKey = SetKey },
                new ItemConfig { Key = SetPiece4, Type = "equipment", Slot = EquipmentSlot.Artifact, SetKey = SetKey },
                new ItemConfig { Key = LooseArtifact, Type = "equipment", Slot = EquipmentSlot.Artifact }
            ],
            Equipment = new EquipmentConfig
            {
                ArtifactSlots = 4,
                MaxEnhancement = 20,
                EnhancementBonusPerLevel = 0.1,
                ForgeBuildingKey = Forge,
                EnhanceBaseGold = 200,
                EnhanceCostGrowth = 1.35,
                SafeEnhancementLevel = 5,
                SuccessDropPerLevel = 0.05,
                MinSuccessChance = 0.25,
                BreakChanceOnFailure = 0.2,
                RepairCostShare = 0.5,
                ArtifactBaseStats = 2,
                ArtifactStatLevels = [4, 8],
                ArtifactUpgradeLevels = [12, 16, 20],
                DoubleUpgradeChance = 0.1,
                ArtifactStats =
                [
                    new ArtifactStatConfig { Stat = "Attack", Min = 4, Max = 12, UpgradeMin = 1, UpgradeMax = 3 },
                    new ArtifactStatConfig { Stat = "Defense", Min = 5, Max = 14, UpgradeMin = 1, UpgradeMax = 4 }
                ],
                ArtifactRarityMultipliers = new Dictionary<string, double> { ["Common"] = 1.0 },
                SetBonuses =
                [
                    new SetBonusConfig
                    {
                        SetKey = SetKey,
                        RequiredPieces = 4,
                        Stats = new Dictionary<string, double> { ["Attack"] = SetBonusAttack }
                    }
                ]
            },
            Resources = [new ResourceConfig { Key = "food" }],
            Units =
            [
                new UnitConfig { Key = "infantry" },
                new UnitConfig { Key = "archer" }
            ],
            HeroSettings = new HeroesConfig
            {
                MaxTier = 3,
                LevelsPerTier = 10,
                MaxMarches = 3,
                MaxConstellation = 6,
                BuildingKey = "heroeshall",
                HealBuildingKey = "hospital",
                HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
                Classes = ["warrior"],
                TierStatMultipliers = [1.0, 1.5, 2.0],
                EvolutionItemKeys = ["hero_essence_t2", "hero_essence_t3"],
                OverflowGems = new Dictionary<string, int> { ["Common"] = 0 }
            },
            Heroes =
            [
                Hero(Buffer, passives),
                Hero(Plain)
            ]
        };

        /// <summary>Пасивка на захист: найчастіший випадок у бойових тестах.</summary>
        public static HeroPassiveConfig Defence(double percent, string target = "infantry",
            int unlockConstellation = 0, double perConstellation = 0) => new()
            {
                Key = $"defence_{target}_{unlockConstellation}",
                Target = target,
                Stat = "Defense",
                UnlockConstellation = unlockConstellation,
                BasePercent = percent,
                PercentPerConstellation = perConstellation
            };

        /// <summary>Пасивка на атаку.</summary>
        public static HeroPassiveConfig Attack(double percent, string target = "infantry",
            int unlockConstellation = 0, double perConstellation = 0) => new()
            {
                Key = $"attack_{target}_{unlockConstellation}",
                Target = target,
                Stat = "Attack",
                UnlockConstellation = unlockConstellation,
                BasePercent = percent,
                PercentPerConstellation = perConstellation
            };

        /// <summary>Готовий множник: конфіг, герой, сузір'я — і StackBuff на виході.</summary>
        public static StackBuff Buff(int constellation = 0, params HeroPassiveConfig[] passives)
        {
            var config = Config(passives);
            var hero = HeroWith(Buffer, constellation, config.HeroSettings.MaxConstellation);

            return new HeroCombatModifiers(new GameCatalog(config)).For(hero);
        }

        /// <summary>Герой заданого типу із проставленим сузір'ям.</summary>
        public static Hero HeroWith(string heroKey, int constellation = 0, int maxConstellation = 6)
        {
            var hero = new Hero(Guid.NewGuid(), Guid.NewGuid(), 1, heroKey, Guid.NewGuid(),
                asLeader: true, Now);

            for (var i = 0; i < constellation; i++)
                hero.TryAddConstellation(maxConstellation, Now);

            return hero;
        }

        private static HeroConfig Hero(string key, params HeroPassiveConfig[] passives) => new()
        {
            Key = key,
            Class = "warrior",
            SummonShards = 10,
            ShardPriceGold = 100,
            BaseStats = new Dictionary<string, double> { ["Attack"] = 100, ["Defense"] = 40 },
            StatGrowth = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 4 },
            LevelUpCosts =
            [
                 new HeroLevelCostBand
                        {
                            FromLevel = 1,
                            Cost = [new ResourceCost { Resource = "food", Amount = 50 }]
                        }
            ],
            Passives = [.. passives]
        };

        /// <summary>Екземпляр спорядження із заданими статами.</summary>
        public static EquipmentItem Item(string itemKey, EquipmentSlot slot, params (string Stat, double Value)[] stats)
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, itemKey, slot, Rarity.Common,
                stats.Length > 0 ? stats : [("Attack", 10.0)], Now);

        /// <summary>Герой заданого рівня й тіру.</summary>
        public static Hero HeroAt(int level = 1, int tier = 1, string heroKey = Plain)
        {
            var hero = new Hero(Guid.NewGuid(), Guid.NewGuid(), 1, heroKey, Guid.NewGuid(), asLeader: true, Now);

            for (var i = 1; i < level; i++)
                hero.GainLevel(level, Now);

            for (var i = 1; i < tier; i++)
                hero.EvolveTier(tier, Now);

            return hero;
        }
    }
}
