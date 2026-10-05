using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

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
            var placer = new SettlementPlacer(terrain, geometry, new SystemRandomSource());

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
            var placer = new SettlementPlacer(terrain, geometry, new SystemRandomSource());

            for (var i = 0; i < 50; i++)
            {
                var (x, y) = await placer.FindSpotAsync(1, serverLevel: 1, (_, _) => Task.FromResult(false));

                Assert.True(terrain.IsInBounds(x, y), $"Coordinates ({x},{y}) are outside the map.");
            }
        }

        /// <summary>
        /// На максимальному рівні межа туману дорівнює радіусу: centre + radius = Width,
        /// тобто клітина вже поза картою. Такий кидок пропускається, а не повертається.
        /// </summary>
        [Fact]
        public async Task FindSpotAsync_ShouldSkipTheCellBeyondTheMapEdge_AtMaxServerLevel()
        {
            var config = Config();
            config.MaxServerLevel = 3;
            config.Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.20, 0.50], RingMultipliers = [2.0, 1.4, 1.0],
                RingsAtFirstLevel = 0.40, FogMinShare = 0.40, FogMaxShare = 1.0
            };
            config.Terrains = [new() { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }];
            var terrain = new TerrainGenerator(config);
            var placer = new SettlementPlacer(terrain, new WorldGeometry(config), new EdgeFirstRandom());

            var (x, y) = await placer.FindSpotAsync(1, serverLevel: 3, (_, _) => Task.FromResult(false));

            Assert.True(terrain.IsInBounds(x, y), $"Coordinates ({x},{y}) are outside the map.");
        }

        /// <summary>Зайняті клітини пропускаються — село не ставиться на чуже місце.</summary>
        [Fact]
        public async Task FindSpotAsync_ShouldSkipOccupiedCells()
        {
            var config = Config();
            var terrain = new TerrainGenerator(config);
            var geometry = new WorldGeometry(config); 
            var placer = new SettlementPlacer(terrain, geometry, new SystemRandomSource());

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
            var placer = new SettlementPlacer(terrain, geometry, new SystemRandomSource());

            // усе зайнято
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                placer.FindSpotAsync(1, serverLevel: 1, (_, _) => Task.FromResult(true), maxAttempts: 10));
        }

        private const int LeaderX = 100;
        private const int LeaderY = 100;

        private static (TerrainGenerator Terrain, SettlementPlacer Placer) NearPlacer()
        {
            var config = Config();
            var terrain = new TerrainGenerator(config);
            return (terrain, new SettlementPlacer(terrain, new WorldGeometry(config), new SystemRandomSource()));
        }

        /// <summary>Зайняті клітини квадрата з набору — так, як їх віддала б БД.</summary>
        private static Func<int, int, int, int, Task<IReadOnlyCollection<(int X, int Y)>>> OccupiedFrom(
            HashSet<(int X, int Y)> occupied)
            => (minX, minY, maxX, maxY) => Task.FromResult<IReadOnlyCollection<(int X, int Y)>>(
                occupied.Where(c => c.X >= minX && c.X <= maxX && c.Y >= minY && c.Y <= maxY).ToList());

        private static int Distance2((int X, int Y) cell) => (cell.X - LeaderX) * (cell.X - LeaderX) + (cell.Y - LeaderY) * (cell.Y - LeaderY);

        /// <summary>Телепорт до лідера ставить село на найближчу вільну придатну клітину — ближчої не лишається.</summary>
        [Fact]
        public async Task FindSpotNearAsync_ShouldPickTheNearestFreeHabitableCell()
        {
            var (terrain, placer) = NearPlacer();
            var occupied = new HashSet<(int X, int Y)> { (LeaderX, LeaderY) };

            var spot = await placer.FindSpotNearAsync(1, 1, LeaderX, LeaderY, OccupiedFrom(occupied));

            Assert.NotNull(spot);
            Assert.True(terrain.IsHabitable(1, spot.Value.X, spot.Value.Y));
            Assert.DoesNotContain(spot.Value, occupied);

            var closer = Enumerable.Range(-5, 11)
                .SelectMany(dy => Enumerable.Range(-5, 11).Select(dx => (X: LeaderX + dx, Y: LeaderY + dy)))
                .Where(c => !occupied.Contains(c) && terrain.IsHabitable(1, c.X, c.Y) && Distance2(c) < Distance2(spot.Value));
            Assert.Empty(closer);
        }

        /// <summary>Гравці, що прилітають один за одним, займають різні клітини й щільно оточують лідера.</summary>
        [Fact]
        public async Task FindSpotNearAsync_ShouldSurroundTheLeaderTightly_WhenSeveralArriveInTurn()
        {
            var (_, placer) = NearPlacer();
            var occupied = new HashSet<(int X, int Y)> { (LeaderX, LeaderY) };
            var previous = 0;

            for (var i = 0; i < 12; i++)
            {
                var spot = await placer.FindSpotNearAsync(1, 1, LeaderX, LeaderY, OccupiedFrom(occupied));

                Assert.NotNull(spot);
                Assert.True(occupied.Add(spot.Value), $"Cell {spot} was handed out twice.");
                Assert.True(Distance2(spot.Value) >= previous, "A later arrival landed closer than an earlier one.");
                previous = Distance2(spot.Value);
            }
        }

        /// <summary>Сусідство лідера заселене щільно — пошук розширюється, а не відмовляє.</summary>
        [Fact]
        public async Task FindSpotNearAsync_ShouldWidenTheSearch_WhenTheNeighbourhoodIsFull()
        {
            var (terrain, placer) = NearPlacer();
            var occupied = Enumerable.Range(-10, 21)
                .SelectMany(dy => Enumerable.Range(-10, 21).Select(dx => (X: LeaderX + dx, Y: LeaderY + dy)))
                .ToHashSet();
            var squares = new List<int>();
            var query = OccupiedFrom(occupied);

            var spot = await placer.FindSpotNearAsync(1, 1, LeaderX, LeaderY, (minX, minY, maxX, maxY) =>
            {
                squares.Add((maxX - minX) / 2);
                return query(minX, minY, maxX, maxY);
            });

            Assert.NotNull(spot);
            Assert.True(Math.Max(Math.Abs(spot.Value.X - LeaderX), Math.Abs(spot.Value.Y - LeaderY)) > 10);
            Assert.True(terrain.IsHabitable(1, spot.Value.X, spot.Value.Y));
            Assert.Equal([4, 8, 16], squares);
        }

        /// <summary>Уся відкрита зона зайнята — null, а не нескінченний пошук.</summary>
        [Fact]
        public async Task FindSpotNearAsync_ShouldReturnNull_WhenEverythingIsTaken()
        {
            var (_, placer) = NearPlacer();

            var spot = await placer.FindSpotNearAsync(1, 1, LeaderX, LeaderY, (minX, minY, maxX, maxY) =>
                Task.FromResult<IReadOnlyCollection<(int X, int Y)>>(
                    Enumerable.Range(minY, maxY - minY + 1)
                        .SelectMany(y => Enumerable.Range(minX, maxX - minX + 1).Select(x => (x, y)))
                        .ToList()));

            Assert.Null(spot);
        }
    }
}
