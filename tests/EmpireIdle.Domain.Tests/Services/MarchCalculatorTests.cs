using EmpireIdle.Domain.Services;

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
    }
}
