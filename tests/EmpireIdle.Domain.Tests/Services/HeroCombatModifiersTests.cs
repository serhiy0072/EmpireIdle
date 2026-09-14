using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Бонуси героя своєму війську. Головне тут — що множник завжди
    /// визначений: бойова формула не має розрізняти «герой є» і «ні».
    /// </summary>
    public class HeroCombatModifiersTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly Guid Garrison = Guid.NewGuid();

        /// <summary>
        /// Мінімальний конфіг, який проходить валідатор. Один тір навмисно:
        /// тоді не потрібні ні предмети еволюції, ні зростання множників,
        /// а пасивки — єдине, що тут справді перевіряється.
        /// </summary>
        private static HeroCombatModifiers Modifiers() => new(new GameCatalog(new GameConfig
        {
            Buildings =
            [
                new BuildingConfig { Key = "townhall", IsMainBuilding = true },
                new BuildingConfig { Key = "heroeshall" }
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
                Classes = ["warrior"],
                TierStatMultipliers = [1.0],
                EvolutionItemKeys = [],
                OverflowGems = new Dictionary<string, int> { ["Common"] = 0 }
            },
            Heroes =
            [
                new HeroConfig
                {
                    Key = "warrior_bran",
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
                    Passives =
                    [
                        new HeroPassiveConfig
                        {
                            Key = "shieldwall", Target = "infantry", Stat = "Defense",
                            UnlockConstellation = 0, BasePercent = 6, PercentPerConstellation = 2
                        },
                        new HeroPassiveConfig
                        {
                            Key = "hold_the_line", Target = "all", Stat = "Defense",
                            UnlockConstellation = 3, BasePercent = 4, PercentPerConstellation = 1.5
                        }
                    ]
                },
                new HeroConfig
                {
                    Key = "plain_hero",
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
                    ]
                }
            ]
        }));

        private static Hero Hero(string key = "warrior_bran", int constellation = 0)
        {
            var hero = new Hero(Guid.NewGuid(), Guid.NewGuid(), 1, key, Garrison, asLeader: true, Now);

            for (var i = 0; i < constellation; i++)
                hero.TryAddConstellation(maxConstellation: 6, Now);

            return hero;
        }

        [Fact]
        public void For_ShouldApplyAnUnlockedPassive()
        {
            var buff = Modifiers().For(Hero());

            Assert.Equal(1.06, buff.Defense("infantry"), 3);
        }

        /// <summary>Пасивка б'є лише по своїй цілі: лучникам вона нічого не дає.</summary>
        [Fact]
        public void For_ShouldNotLeakToOtherUnits()
        {
            var buff = Modifiers().For(Hero());

            Assert.Equal(1.0, buff.Defense("archer"), 3);
        }

        [Fact]
        public void For_ShouldIgnoreALockedPassive()
        {
            var buff = Modifiers().For(Hero(constellation: 2));

            // shieldwall: 6 + 2×2 = 10; hold_the_line ще закрита
            Assert.Equal(1.10, buff.Defense("infantry"), 3);
            Assert.Equal(1.0, buff.Defense("archer"), 3);
        }

        /// <summary>
        /// Бонус на все військо й бонус на тип складаються, а не множаться:
        /// 6+2×3 на піхоту плюс 4 на всіх дає 16%.
        /// </summary>
        [Fact]
        public void For_ShouldSumTheAllTargetWithTheSpecificOne()
        {
            var buff = Modifiers().For(Hero(constellation: 3));

            Assert.Equal(1.16, buff.Defense("infantry"), 3);
            Assert.Equal(1.04, buff.Defense("archer"), 3);
        }

        [Fact]
        public void For_ShouldNotTouchTheOtherStat()
        {
            var buff = Modifiers().For(Hero(constellation: 3));

            Assert.Equal(1.0, buff.Attack("infantry"), 3);
        }

        /// <summary>Поранений не дає нічого: у цьому й ціна поразки.</summary>
        [Fact]
        public void For_ShouldReturnNothing_WhenTheHeroIsWounded()
        {
            var hero = Hero(constellation: 3);
            hero.Wound(Now);

            var buff = Modifiers().For(hero);

            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        [Fact]
        public void For_ShouldReturnNothing_WhenThereIsNoHero()
        {
            var buff = Modifiers().For(null);

            Assert.Equal(1.0, buff.Attack("infantry"), 3);
            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        [Fact]
        public void For_ShouldReturnNothing_WhenTheHeroHasNoPassives()
        {
            var buff = Modifiers().For(Hero("plain_hero"));

            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        /// <summary>Невідомий тип юніта не валить формулу.</summary>
        [Fact]
        public void For_ShouldReturnOne_ForAnUnknownUnitType()
        {
            var buff = Modifiers().For(Hero());

            Assert.Equal(1.0, buff.Defense("siege_ram"), 3);
        }
    }
}
