using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    public class MonsterArmyBuilderTests
    {
        private static MonsterArmyBuilder Builder() => new(new GameCatalog(new GameConfig
        {
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
            Monsters =
            [
                new MonsterConfig
                {
                    Key = "wolves",
                    MinLevel = 1,
                    MaxLevel = 10,
                    UnitGrowth = 1.5,
                    RewardGrowth = 1.3,
                    Units = [new UnitStack { UnitType = "infantry", Count = 10 }],
                    Rewards = [new ResourceCost { Resource = "food", Amount = 100 }]
                }
            ]
        }));

        /// <summary>На мінімальному рівні склад дорівнює конфігу — множник ще не діє.</summary>
        [Fact]
        public void BuildArmy_ShouldMatchConfig_AtMinimumLevel()
        {
            var army = Builder().BuildArmy("wolves", level: 1);

            Assert.Equal(10, army["infantry"]);
        }

        /// <summary>Кількість росте геометрично: 10 × 1.5^(рівень−1).</summary>
        [Theory]
        [InlineData(2, 15)]
        [InlineData(3, 22)]   // 10 × 2.25 = 22.5 → 22 (Math.Round до парного)
        [InlineData(4, 34)]   // 10 × 3.375 = 33.75 → 34
        public void BuildArmy_ShouldGrowWithLevel(int level, int expected)
        {
            Assert.Equal(expected, Builder().BuildArmy("wolves", level)["infantry"]);
        }

        /// <summary>
        /// Загін ніколи не порожній: монстр із нульовою армією програвав би
        /// без бою, а гравець отримував би нагороду задарма.
        /// </summary>
        [Fact]
        public void BuildArmy_ShouldNeverReturnZeroUnits()
        {
            var builder = new MonsterArmyBuilder(new GameCatalog(new GameConfig
            {
                Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
                Monsters =
                [
                    new MonsterConfig
                    {
                        Key = "rats",
                        MinLevel = 1,
                        MaxLevel = 5,
                        UnitGrowth = 0.1,
                        RewardGrowth = 1.0,
                        Units = [new UnitStack { UnitType = "infantry", Count = 1 }],
                        Rewards = []
                    }
                ]
            }));

            Assert.Equal(1, builder.BuildArmy("rats", level: 5)["infantry"]);
        }

        /// <summary>Нагорода росте своєю кривою, окремою від складу армії.</summary>
        [Theory]
        [InlineData(1, 100)]
        [InlineData(3, 169)]   // 100 × 1.69
        public void BuildRewards_ShouldGrowWithLevel(int level, int expected)
        {
            var rewards = Builder().BuildRewards("wolves", level);

            Assert.Equal(expected, rewards.Single(r => r.Resource == "food").Amount);
        }

        /// <summary>Невідомий тип — поломка конфіга, а не доменне правило.</summary>
        [Fact]
        public void BuildArmy_ShouldThrow_ForUnknownMonsterType()
        {
            Assert.Throws<InvalidOperationException>(() => Builder().BuildArmy("dragon", level: 1));
        }
    }
}
