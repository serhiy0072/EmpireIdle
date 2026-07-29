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
            TerrainWeights = new Dictionary<string, int>
            {
                ["plain"] = 40,
                ["forest"] = 25,
                ["mountain"] = 20,
                ["water"] = 15
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
            var generator = new TerrainGenerator(Config());
            var allowed = new[] { "plain", "forest", "mountain", "water" };

            for (int x = 0; x < 50; x++)
                for (int y = 0; y < 50; y++)
                    Assert.Contains(generator.GetTerrain(1, x, y), allowed);
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
                    var terrain = generator.GetTerrain(1, x, y);
                    counts[terrain] = counts.GetValueOrDefault(terrain) + 1;
                }

            var total = 200 * 200;
            var plainShare = counts["plain"] / (double)total;
            var waterShare = counts["water"] / (double)total;

            Assert.InRange(plainShare, 0.30, 0.50); // очікуємо ~0.40
            Assert.InRange(waterShare, 0.08, 0.22); // очікуємо ~0.15
            Assert.True(counts["plain"] > counts["water"]);
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