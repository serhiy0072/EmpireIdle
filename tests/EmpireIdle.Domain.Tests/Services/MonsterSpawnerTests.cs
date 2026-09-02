using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    public class MonsterSpawnerTests
    {
        private static MapConfig Config() => new()
        {
            Width = 200,
            Height = 200,
            TerrainSeed = 4242,
            CellsPerMonster = 100,
            MaxServerLevel = 3,
            Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.20, 0.50],
                RingMultipliers = [2.0, 1.4, 1.0],
                RingsAtFirstLevel = 0.40,
                FogMinShare = 0.40,
                FogMaxShare = 1.0
            },
            Terrains =
            [
                new() { Type = "plain", Weight = 60, Passable = true, MoveCost = 1.0, Habitable = true },
                new() { Type = "water", Weight = 40, Passable = false, MoveCost = 1.0, Habitable = false }
            ]
        };

        private static GameCatalog Catalog() => new(new GameConfig
        {
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
            Monsters =
            [
                new MonsterConfig
                {
                    Key = "wolves", RequiresServerLevel = 0, MinLevel = 1, MaxLevel = 10,
                    UnitGrowth = 1.5, RewardGrowth = 1.3,
                    Units = [new UnitStack { UnitType = "infantry", Count = 10 }],
                    Rewards = []
                },
                new MonsterConfig
                {
                    Key = "trolls", RequiresServerLevel = 2, MinLevel = 5, MaxLevel = 15,
                    UnitGrowth = 1.5, RewardGrowth = 1.3,
                    Units = [new UnitStack { UnitType = "infantry", Count = 30 }],
                    Rewards = []
                }
            ]
        });

        private static MonsterSpawner Spawner(MapConfig config)
            => new(new TerrainGenerator(config), config, Catalog(), new WorldGeometry(config), new SystemRandomSource());

        /// <summary>
        /// Цільова кількість рахується від ВІДКРИТОЇ площі: на першому рівні
        /// доступно 16% карти, і рахунок від повної дав би вшестеро щільніший
        /// засів у зоні, куди гравці мають доступ.
        /// </summary>
        [Fact]
        public void GetTargetPopulation_ShouldGrowWithServerLevel()
        {
            var spawner = Spawner(Config());

            var atFirst = spawner.GetTargetPopulation(1);
            var atMax = spawner.GetTargetPopulation(3);

            Assert.True(atMax > atFirst,
                $"Population must grow with the fog: {atFirst} at level 1, {atMax} at level 3.");
        }

        /// <summary>Монстри не з'являються за межею туману — там гравцям їх не дістати.</summary>
        [Fact]
        public async Task TrySpawnAsync_ShouldStayWithinTheFogBoundary()
        {
            var config = Config();
            var spawner = Spawner(config);
            var geometry = new WorldGeometry(config);

            for (var i = 0; i < 50; i++)
            {
                var spawn = await spawner.TrySpawnAsync(1, serverLevel: 1, (_, _) => Task.FromResult(false));

                Assert.NotNull(spawn);
                Assert.True(geometry.IsWithinFog(spawn!.Value.X, spawn.Value.Y, 1),
                    $"Monster spawned outside the fog at ({spawn.Value.X},{spawn.Value.Y}).");
            }
        }

        /// <summary>Клітина завжди придатна — монстр у воді недосяжний.</summary>
        [Fact]
        public async Task TrySpawnAsync_ShouldOnlyUseHabitableCells()
        {
            var config = Config();
            var spawner = Spawner(config);
            var terrain = new TerrainGenerator(config);

            for (var i = 0; i < 50; i++)
            {
                var spawn = await spawner.TrySpawnAsync(1, serverLevel: 1, (_, _) => Task.FromResult(false));

                Assert.NotNull(spawn);
                Assert.True(terrain.IsHabitable(1, spawn!.Value.X, spawn.Value.Y));
            }
        }

        /// <summary>
        /// Коли вільного місця немає, спавнер здається, а не крутиться вічно:
        /// джоб має завершитись і на заповненій карті.
        /// </summary>
        [Fact]
        public async Task TrySpawnAsync_ShouldReturnNull_WhenEverythingIsOccupied()
        {
            var spawn = await Spawner(Config())
                .TrySpawnAsync(1, serverLevel: 1, (_, _) => Task.FromResult(true));

            Assert.Null(spawn);
        }

        /// <summary>Типи, що вимагають вищого рівня світу, не з'являються раніше.</summary>
        [Fact]
        public async Task TrySpawnAsync_ShouldOnlyUseTypesUnlockedAtTheServerLevel()
        {
            var spawner = Spawner(Config());

            for (var i = 0; i < 50; i++)
            {
                var spawn = await spawner.TrySpawnAsync(1, serverLevel: 1, (_, _) => Task.FromResult(false));

                Assert.Equal("wolves", spawn!.Value.Type);
            }
        }

        /// <summary>Рівень монстра завжди в межах діапазону типу.</summary>
        [Fact]
        public async Task TrySpawnAsync_ShouldKeepLevelWithinTheConfiguredRange()
        {
            var spawner = Spawner(Config());

            for (var i = 0; i < 50; i++)
            {
                var spawn = await spawner.TrySpawnAsync(1, serverLevel: 3, (_, _) => Task.FromResult(false));

                Assert.InRange(spawn!.Value.Level, 1, 15);
            }
        }
    }
}
