using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    public class TerrainGeneratorTests
    {
        private static MapConfig Config(int seed = 12345) => new()
        {
            Width = 1000,
            Height = 1000,
            TerrainSeed = seed,
            Terrains = new List<TerrainConfig>
            {
                new() { Type = "plain",    Weight = 35, Passable = true,  MoveCost = 1.0, Habitable = true },
                new() { Type = "forest",   Weight = 22, Passable = true,  MoveCost = 1.5, Habitable = true },
                new() { Type = "mountain", Weight = 18, Passable = true,  MoveCost = 2.0, Habitable = true },
                new() { Type = "water",    Weight = 13, Passable = false, MoveCost = 1.0, Habitable = false },
                new() { Type = "peaks",    Weight = 7,  Passable = false, MoveCost = 1.0, Habitable = false },
                new() { Type = "swamp",    Weight = 5,  Passable = true,  MoveCost = 2.5, Habitable = false }
            }
        };

        /// <summary>
        /// Той самий (serverId, x, y) завжди дає той самий терейн —
        /// це і робить непотрібним збереження карти в БД.
        /// </summary>
        [Fact]
        public void GetTerrain_ShouldBeDeterministic_ForSameCoordinates()
        {
            var generator = new TerrainGenerator(Config());

            var first = generator.GetTerrain(1, 137, 842);
            var second = generator.GetTerrain(1, 137, 842);

            Assert.Equal(first, second);
        }

        /// <summary>Різні сервери — різні карти при тих самих координатах (хоч інколи можуть збігтись).</summary>
        [Fact]
        public void GetTerrain_ShouldDifferAcrossServers_ForMostCells()
        {
            var generator = new TerrainGenerator(Config());
            var differences = 0;

            for (int x = 0; x < 100; x++)
                if (generator.GetTerrain(1, x, 0) != generator.GetTerrain(2, x, 0))
                    differences++;

            Assert.True(differences > 30, $"Expected server-dependent terrain, got only {differences}/100 differences.");
        }

        /// <summary>Новий сід — інша карта (інакше всі сервери були б однакові).</summary>
        [Fact]
        public void GetTerrain_ShouldDependOnSeed()
        {
            var a = new TerrainGenerator(Config(seed: 111));
            var b = new TerrainGenerator(Config(seed: 999));
            var differences = 0;

            for (int x = 0; x < 100; x++)
                if (a.GetTerrain(1, x, 0) != b.GetTerrain(1, x, 0))
                    differences++;

            Assert.True(differences > 30, $"Expected seed-dependent terrain, got only {differences}/100 differences.");
        }

        /// <summary>Генератор повертає лише оголошені в конфізі типи.</summary>
        [Fact]
        public void GetTerrain_ShouldReturnOnlyConfiguredTypes()
        {
            var config = Config();
            var generator = new TerrainGenerator(config);
            var allowed = config.Terrains.Select(t => t.Type).ToArray();

            for (int x = 0; x < 50; x++)
                for (int y = 0; y < 50; y++)
                    Assert.Contains(generator.GetTerrainType(1, x, y), allowed);
        }

        /// <summary>
        /// Розподіл приблизно відповідає вагам: plain (40%) має бути найчастішим,
        /// water (15%) — найрідшим. Допуск широкий — це не тест якості шуму.
        /// </summary>
        [Fact]
        public void GetTerrain_ShouldRoughlyFollowConfiguredWeights()
        {
            var generator = new TerrainGenerator(Config());
            var counts = new Dictionary<string, int>();

            for (int x = 0; x < 200; x++)
                for (int y = 0; y < 200; y++)
                {
                    var terrain = generator.GetTerrainType(1, x, y);
                    counts[terrain] = counts.GetValueOrDefault(terrain) + 1;
                }

            var total = 200 * 200;
            var plainShare = counts["plain"] / (double)total;
            var waterShare = counts["water"] / (double)total;

            Assert.InRange(plainShare, 0.25, 0.45); // очікуємо ~0.35
            Assert.InRange(waterShare, 0.06, 0.20); // очікуємо ~0.13
            Assert.True(counts["plain"] > counts["water"]);
        }

        /// <summary>
        /// Властивості клітини беруться з конфіга типу: вода непрохідна й незаселювана,
        /// болото проходиме, але жити там не можна.
        /// </summary>
        [Fact]
        public void GetTerrain_ShouldExposeTerrainProperties()
        {
            var generator = new TerrainGenerator(Config());

            // знайдемо на карті клітину з водою і клітину з болотом
            var water = FindCellWithTerrain(generator, "water");
            var swamp = FindCellWithTerrain(generator, "swamp");

            Assert.False(generator.IsPassable(1, water.X, water.Y));
            Assert.False(generator.IsHabitable(1, water.X, water.Y));

            Assert.True(generator.IsPassable(1, swamp.X, swamp.Y));
            Assert.False(generator.IsHabitable(1, swamp.X, swamp.Y));
            Assert.Equal(2.5, generator.GetMoveCost(1, swamp.X, swamp.Y));
        }

        /// <summary>Шукає першу клітину із заданим типом місцевості.</summary>
        private static (int X, int Y) FindCellWithTerrain(TerrainGenerator generator, string type)
        {
            for (int x = 0; x < 200; x++)
                for (int y = 0; y < 200; y++)
                    if (generator.GetTerrainType(1, x, y) == type)
                        return (x, y);

            throw new InvalidOperationException($"Terrain '{type}' not found in 200x200 area.");
        }

        /// <summary>Межі карти: (0,0) — валідна, координати за розміром — ні.</summary>
        [Theory]
        [InlineData(0, 0, true)]
        [InlineData(999, 999, true)]
        [InlineData(1000, 500, false)]
        [InlineData(-1, 0, false)]
        [InlineData(500, 1000, false)]
        public void IsInBounds_ShouldRespectMapDimensions(int x, int y, bool expected)
        {
            var generator = new TerrainGenerator(Config());

            Assert.Equal(expected, generator.IsInBounds(x, y));
        }
    }
}