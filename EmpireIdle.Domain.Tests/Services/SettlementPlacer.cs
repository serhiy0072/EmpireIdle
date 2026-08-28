using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    public class SettlementPlacerTests
    {
        private static MapConfig Config() => new()
        {
            Width = 200,
            Height = 200,
            TerrainSeed = 4242,
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

        /// <summary>Село ніколи не потрапляє на непридатну місцевість (вода, скелі, болото).</summary>
        [Fact]
        public async Task FindSpotAsync_ShouldAlwaysReturnHabitableCell()
        {
            var config = Config();
            var terrain = new TerrainGenerator(config);
            var geometry = new WorldGeometry(config); 
            var placer = new SettlementPlacer(terrain, geometry);

            // 50 спроб поспіль — щоб зловити випадковість
            for (var i = 0; i < 50; i++)
            {
                var (x, y) = await placer.FindSpotAsync(1, serverLevel: 1, (_, _) => Task.FromResult(false));

                Assert.True(terrain.IsHabitable(1, x, y),
                    $"Village placed on non-habitable terrain '{terrain.GetTerrainType(1, x, y)}' at ({x},{y}).");
            }
        }

        /// <summary>Координати завжди в межах карти.</summary>
        [Fact]
        public async Task FindSpotAsync_ShouldReturnCoordinatesWithinBounds()
        {
            var config = Config();
            var terrain = new TerrainGenerator(config);
            var geometry = new WorldGeometry(config); 
            var placer = new SettlementPlacer(terrain, geometry);

            for (var i = 0; i < 50; i++)
            {
                var (x, y) = await placer.FindSpotAsync(1, serverLevel: 1, (_, _) => Task.FromResult(false));

                Assert.True(terrain.IsInBounds(x, y), $"Coordinates ({x},{y}) are outside the map.");
            }
        }

        /// <summary>Зайняті клітини пропускаються — село не ставиться на чуже місце.</summary>
        [Fact]
        public async Task FindSpotAsync_ShouldSkipOccupiedCells()
        {
            var config = Config();
            var terrain = new TerrainGenerator(config);
            var geometry = new WorldGeometry(config); 
            var placer = new SettlementPlacer(terrain, geometry);

            var occupied = new HashSet<(int, int)>();

            // Заселяємо 20 сіл підряд, кожне наступне бачить попередні як зайняті
            for (var i = 0; i < 20; i++)
            {
                var (x, y) = await placer.FindSpotAsync(1, serverLevel: 1,
                    (cx, cy) => Task.FromResult(occupied.Contains((cx, cy))));

                Assert.DoesNotContain((x, y), occupied);
                occupied.Add((x, y));
            }

            Assert.Equal(20, occupied.Count); // усі позиції унікальні
        }

        /// <summary>
        /// Якщо вільних придатних клітин немає — кидає виняток, а не зациклюється.
        /// </summary>
        [Fact]
        public async Task FindSpotAsync_ShouldThrow_WhenNoFreeCellFound()
        {
            var config = Config();
            var terrain = new TerrainGenerator(config);
            var geometry = new WorldGeometry(config); 
            var placer = new SettlementPlacer(terrain, geometry);

            // усе зайнято
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                placer.FindSpotAsync(1, serverLevel: 1, (_, _) => Task.FromResult(true), maxAttempts: 10));
        }
    }
}
