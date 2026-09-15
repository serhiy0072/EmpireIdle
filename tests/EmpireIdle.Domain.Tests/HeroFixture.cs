using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
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
                new BuildingConfig { Key = "hospital" }
            ],
            Resources = [new ResourceConfig { Key = "food" }],
            Units =
            [
                new UnitConfig { Key = "infantry" },
                new UnitConfig { Key = "archer" }
            ],
            HeroSettings = new HeroesConfig
            {
                MaxTier = 1,
                LevelsPerTier = 10,
                MaxMarches = 3,
                MaxConstellation = 6,
                BuildingKey = "heroeshall",
                HealBuildingKey = "hospital",
                HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
                Classes = ["warrior"],
                TierStatMultipliers = [1.0],
                EvolutionItemKeys = [],
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
    }
}
