using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Падіння міста (GDD §2.6): запобіжники виселення й місце приземлення —
    /// «тебе зсунули», не «почни спочатку».
    /// </summary>
    public class CityFallTests
    {
        private static CityFallRules Rules(bool enabled = true) => new(new GameCatalog(new GameConfig
        {
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 }],
            Combat = new CombatConfig
            {
                CityFall = new CityFallConfig
                {
                    Enabled = enabled,
                    MaxPowerRatio = 2.0,
                    EvictionsPerAttacker = 3
                }
            }
        }));

        private static MapConfig Map() => new()
        {
            Width = 300,
            Height = 300,
            MaxServerLevel = 3,
            TerrainSeed = 4242,
            Terrains =
            [
                new TerrainConfig { Type = "plain", Weight = 3, Passable = true, MoveCost = 1.0, Habitable = true },
                new TerrainConfig { Type = "water", Weight = 1, Passable = false, MoveCost = 1.0, Habitable = false }
            ],
            Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.20, 0.50],
                RingMultipliers = [2.0, 1.4, 1.0],
                RingsAtFirstLevel = 0.40,
                FogMinShare = 0.40,
                FogMaxShare = 1.0
            }
        };

        // ---------- Запобіжники ----------

        [Theory]
        [InlineData(100, 100, 0, CityFallVerdict.Evict)]
        [InlineData(200, 100, 0, CityFallVerdict.Evict)]
        [InlineData(201, 100, 0, CityFallVerdict.AttackerTooStrong)]
        [InlineData(100, 100, 3, CityFallVerdict.LimitReached)]
        public void Judge_ShouldApplyThePowerRatioAndTheAttackerLimit(double attacker, double defender, int recent,
            CityFallVerdict expected)
        {
            Assert.Equal(expected, Rules().Judge(attacker, defender, recent));
        }

        /// <summary>Село без сили не виселяється: порівнювати немає з чим, це був би чистий griefing.</summary>
        [Fact]
        public void Judge_ShouldNotEvict_ADefenderWithoutPower()
        {
            Assert.Equal(CityFallVerdict.AttackerTooStrong, Rules().Judge(0, 0, 0));
        }

        [Fact]
        public void Judge_ShouldNotEvict_WhenTheMechanicIsOff()
        {
            Assert.Equal(CityFallVerdict.Disabled, Rules(enabled: false).Judge(100, 100, 0));
        }

        // ---------- Межі кілець ----------

        /// <summary>Межі кілець прилягають одна до одної без проміжків і перекриттів.</summary>
        [Fact]
        public void RingDistanceBounds_ShouldTileTheOpenRegion()
        {
            var geometry = new WorldGeometry(Map());

            var centre = geometry.RingDistanceBounds(0, serverLevel: 3)!.Value;
            var middle = geometry.RingDistanceBounds(1, serverLevel: 3)!.Value;
            var outer = geometry.RingDistanceBounds(2, serverLevel: 3)!.Value;

            Assert.Equal(0, centre.Min);
            Assert.Equal(centre.Max + 1, middle.Min);
            Assert.Equal(middle.Max + 1, outer.Min);
            Assert.Equal(geometry.SettlementBoundary(3), outer.Max);
        }

        /// <summary>Кільце, що цілком під туманом, не пропонує жодної відстані.</summary>
        [Fact]
        public void RingDistanceBounds_ShouldBeEmpty_ForARingStillUnderTheFog()
        {
            // Перший рівень: зовнішнє кільце починається з 31 клітини. Звичайний туман
            // (0.4 радіуса = 60) його вже відкриває, вузький (0.05 = 7) — ще ні
            var map = Map();
            map.Geometry.FogMinShare = 0.05;

            Assert.NotNull(new WorldGeometry(Map()).RingDistanceBounds(2, serverLevel: 1));
            Assert.Null(new WorldGeometry(map).RingDistanceBounds(2, serverLevel: 1));
        }

        // ---------- Місце приземлення ----------

        /// <summary>Виселене село лишається в тому ж кільці, якщо там є місце.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task FindSpotNearRing_ShouldLandInTheSameRing(int ring)
        {
            var map = Map();
            var terrain = new TerrainGenerator(map);
            var geometry = new WorldGeometry(map);
            var placer = new SettlementPlacer(terrain, geometry, new SystemRandomSource());

            for (var i = 0; i < 30; i++)
            {
                var spot = await placer.FindSpotNearRingAsync(1, serverLevel: 3, ring, (_, _) => Task.FromResult(false));

                Assert.NotNull(spot);
                var (x, y) = spot.Value;
                Assert.Equal(ring, geometry.RingAt(x, y, serverLevel: 3));
                Assert.True(terrain.IsHabitable(1, x, y));
                Assert.True(geometry.IsWithinFog(x, y, serverLevel: 3));
            }
        }

        /// <summary>Своє кільце зайняте — село сідає в сусіднє, а не лишається на місці.</summary>
        [Fact]
        public async Task FindSpotNearRing_ShouldFallBackToANeighbourRing_WhenItsOwnIsFull()
        {
            var map = Map();
            var terrain = new TerrainGenerator(map);
            var geometry = new WorldGeometry(map);
            var placer = new SettlementPlacer(terrain, geometry, new SystemRandomSource());

            var spot = await placer.FindSpotNearRingAsync(1, serverLevel: 3, ring: 0,
                (x, y) => Task.FromResult(geometry.RingAt(x, y, 3) == 0));

            Assert.NotNull(spot);
            Assert.Equal(1, geometry.RingAt(spot.Value.X, spot.Value.Y, serverLevel: 3));
        }

        /// <summary>Вільного місця немає ніде — пошук повертає null, а не кидає посеред бою.</summary>
        [Fact]
        public async Task FindSpotNearRing_ShouldReturnNull_WhenTheWorldIsFull()
        {
            var map = Map();
            var placer = new SettlementPlacer(new TerrainGenerator(map), new WorldGeometry(map), new SystemRandomSource());

            var spot = await placer.FindSpotNearRingAsync(1, serverLevel: 3, ring: 1,
                (_, _) => Task.FromResult(true), attemptsPerRing: 10);

            Assert.Null(spot);
        }
    }
}
