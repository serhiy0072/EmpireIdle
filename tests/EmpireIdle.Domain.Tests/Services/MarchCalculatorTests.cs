using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    public class MarchCalculatorTests
    {
        private static MapConfig MapConfig() => new()
        {
            Width = 200,
            Height = 200,
            TerrainSeed = 777,
            Terrains = new List<TerrainConfig>
            {
                new() { Type = "plain", Weight = 100, Passable = true, MoveCost = 1.0, Habitable = true }
            }
        };

        private static List<UnitConfig> Units() => new()
        {
            new UnitConfig
            {
                Key = "cavalry",
                DisplayName = "Cavalry",
                BaseTrainMinutes = 3,
                Stats = new Dictionary<string, double> { ["Speed"] = 8 }
            },
            new UnitConfig
            {
                Key = "infantry",
                DisplayName = "Infantry",
                BaseTrainMinutes = 2,
                Stats = new Dictionary<string, double> { ["Speed"] = 4 }
            },
            new UnitConfig
            {
                Key = "siege",
                DisplayName = "Siege",
                BaseTrainMinutes = 10,
                Stats = new Dictionary<string, double> { ["Speed"] = 1 }
            }
        };

        private static GameCatalog Catalog() => new(new GameConfig
        {
            Units = Units(),
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }]
        });

        private static MarchCalculator Calculator()
            => new(new TerrainGenerator(MapConfig()), Catalog());

        /// <summary>Далі — довше: час зростає з відстанню.</summary>
        [Fact]
        public void CalculateDuration_ShouldGrowWithDistance()
        {
            var calc = Calculator();
            var army = new Dictionary<string, int> { ["infantry"] = 10 };

            var near = calc.CalculateDuration(1, 100, 100, 105, 100, army);
            var far = calc.CalculateDuration(1, 100, 100, 150, 100, army);

            Assert.True(far > near, $"Expected longer march to take more time: near={near}, far={far}");
        }

        /// <summary>
        /// Швидкість колони = швидкість найповільнішого юніта:
        /// сама кіннота йде швидше, ніж кіннота разом із облоговими.
        /// </summary>
        [Fact]
        public void CalculateDuration_ShouldUseSlowestUnitSpeed()
        {
            var calc = Calculator();

            var cavalryOnly = calc.CalculateDuration(1, 100, 100, 150, 100,
                new Dictionary<string, int> { ["cavalry"] = 10 });

            var withSiege = calc.CalculateDuration(1, 100, 100, 150, 100,
                new Dictionary<string, int> { ["cavalry"] = 10, ["siege"] = 1 });

            Assert.True(withSiege > cavalryOnly,
                $"Siege must slow the column down: cavalry={cavalryOnly}, withSiege={withSiege}");
        }

        /// <summary>Похід у ту саму клітину не займає часу.</summary>
        [Fact]
        public void CalculateDuration_ShouldBeZero_ForSameCell()
        {
            var calc = Calculator();

            var duration = calc.CalculateDuration(1, 50, 50, 50, 50,
                new Dictionary<string, int> { ["infantry"] = 1 });

            Assert.Equal(TimeSpan.Zero, duration);
        }

        /// <summary>Складний рельєф сповільнює: та сама відстань, дорожчі клітини — більше часу.</summary>
        [Fact]
        public void CalculateDuration_ShouldAccountForTerrainMoveCost()
        {
            var army = new Dictionary<string, int> { ["infantry"] = 5 };

            var easyConfig = MapConfig();
            var easy = new MarchCalculator(new TerrainGenerator(easyConfig), Catalog())
                .CalculateDuration(1, 10, 10, 60, 10, army);

            var hardConfig = MapConfig();
            hardConfig.Terrains = new List<TerrainConfig>
            {
                new() { Type = "swamp", Weight = 100, Passable = true, MoveCost = 3.0, Habitable = false }
            };
            var hard = new MarchCalculator(new TerrainGenerator(hardConfig), Catalog())
                .CalculateDuration(1, 10, 10, 60, 10, army);

            Assert.True(hard > easy, $"Rough terrain must slow the march: easy={easy}, hard={hard}");
        }

        // ---------- Швидкість героя ----------

        /// <summary>
        /// Герой у рахунку нарівні з юнітами: повільний герой гальмує
        /// швидку колону так само, як облогова машина.
        /// </summary>
        [Fact]
        public void CalculateDuration_ShouldFollowTheSlowestParticipant()
        {
            var calc = Calculator();
            var cavalry = new Dictionary<string, int> { ["cavalry"] = 10 };

            var alone = calc.CalculateDuration(1, 100, 100, 150, 100, cavalry);
            var withSlowHero = calc.CalculateDuration(1, 100, 100, 150, 100, cavalry, heroSpeed: 2);

            Assert.True(withSlowHero > alone,
                $"A slow hero must hold the column back: alone={alone}, withHero={withSlowHero}");
        }

        /// <summary>
        /// Швидкий герой колону не пришвидшує: він не несе на собі
        /// облогові машини.
        /// </summary>
        [Fact]
        public void CalculateDuration_ShouldIgnoreAFasterHero()
        {
            var calc = Calculator();
            var infantry = new Dictionary<string, int> { ["infantry"] = 10 };

            var alone = calc.CalculateDuration(1, 100, 100, 150, 100, infantry);
            var withFastHero = calc.CalculateDuration(1, 100, 100, 150, 100, infantry, heroSpeed: 20);

            Assert.Equal(alone, withFastHero);
        }

        /// <summary>
        /// Підкріплення з самого героя йде його швидкістю. До цієї зміни
        /// порожній склад давав базову одиницю, тобто вчетверо повільніше
        /// за піхоту — герой-одинак приходив би пізніше за колону.
        /// </summary>
        [Fact]
        public void CalculateDuration_ShouldUseTheHeroSpeed_WhenThereAreNoUnits()
        {
            var calc = Calculator();
            var empty = new Dictionary<string, int>();

            var solo = calc.CalculateDuration(1, 100, 100, 150, 100, empty, heroSpeed: 8);
            var withCavalry = calc.CalculateDuration(1, 100, 100, 150, 100,
                new Dictionary<string, int> { ["cavalry"] = 1 }, heroSpeed: 8);

            Assert.Equal(withCavalry, solo);
        }

        /// <summary>
        /// Без героя поведінка та сама, що й до зміни: старі виклики
        /// з чотирма аргументами читаються однаково.
        /// </summary>
        [Fact]
        public void CalculateDuration_ShouldMatchTheOldResult_WhenNoHeroIsGiven()
        {
            var calc = Calculator();
            var infantry = new Dictionary<string, int> { ["infantry"] = 10 };

            Assert.Equal(
                calc.CalculateDuration(1, 100, 100, 150, 100, infantry),
                calc.CalculateDuration(1, 100, 100, 150, 100, infantry, heroSpeed: null));
        }

        /// <summary>Нульова або від'ємна швидкість героя не вішає похід назавжди.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void CalculateDuration_ShouldIgnoreANonPositiveHeroSpeed(double heroSpeed)
        {
            var calc = Calculator();
            var infantry = new Dictionary<string, int> { ["infantry"] = 10 };

            Assert.Equal(
                calc.CalculateDuration(1, 100, 100, 150, 100, infantry),
                calc.CalculateDuration(1, 100, 100, 150, 100, infantry, heroSpeed));
        }
    }
}
