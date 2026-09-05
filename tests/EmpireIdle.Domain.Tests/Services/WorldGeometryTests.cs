using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    public class WorldGeometryTests
    {
        private static MapConfig Config(int size = 300) => new()
        {
            Width = size,
            Height = size,
            MaxServerLevel = 3,
            Geometry = new MapGeometryConfig
            {
                RingBoundaries = new List<double> { 0.20, 0.50 },
                RingMultipliers = new List<double> { 2.0, 1.4, 1.0 },
                RingsAtFirstLevel = 0.40,
                FogMinShare = 0.40,
                FogMaxShare = 1.0
            }
        };

        /// <summary>
        /// Межі — частки радіуса, тому подвоєння карти подвоює відстані.
        /// Це головна вимога до геометрії: зміна розміру в конфізі не має
        /// потребувати переписування жодного числа.
        /// </summary>
        [Theory]
        [InlineData(300, 30, 75)]
        [InlineData(500, 50, 125)]
        public void RingBoundaries_ShouldScaleWithMapSize(int size, int expectedCentre, int expectedMiddle)
        {
            var geometry = new WorldGeometry(Config(size));
            var (cx, cy) = geometry.Centre;

            Assert.Equal(0, geometry.RingAt(cx + expectedCentre, cy, serverLevel: 3));
            Assert.Equal(1, geometry.RingAt(cx + expectedCentre + 1, cy, serverLevel: 3));
            Assert.Equal(1, geometry.RingAt(cx + expectedMiddle, cy, serverLevel: 3));
            Assert.Equal(2, geometry.RingAt(cx + expectedMiddle + 1, cy, serverLevel: 3));
        }

        /// <summary>
        /// Кільця вужчі на першому рівні сервера й розширюються до максимуму.
        /// Клітина, що була на околиці, з часом опиняється в центрі —
        /// саме це й задумано: місто росте, і його центр разом із ним.
        /// </summary>
        [Fact]
        public void RingAt_ShouldWidenWithServerLevel()
        {
            var geometry = new WorldGeometry(Config());
            var (cx, cy) = geometry.Centre;

            // 0.20 × 0.40 × 150 = 12 на першому рівні, 30 на третьому
            Assert.Equal(1, geometry.RingAt(cx + 20, cy, serverLevel: 1));
            Assert.Equal(0, geometry.RingAt(cx + 20, cy, serverLevel: 3));
        }

        /// <summary>Відстань Чебишева: кільця квадратні, кут і вісь рівноцінні.</summary>
        [Fact]
        public void DistanceToCentre_ShouldUseChebyshev()
        {
            var geometry = new WorldGeometry(Config());
            var (cx, cy) = geometry.Centre;

            Assert.Equal(30, geometry.DistanceToCentre(cx + 30, cy));
            Assert.Equal(30, geometry.DistanceToCentre(cx + 30, cy + 30));
        }

        /// <summary>Множник береться з конфіга за індексом кільця.</summary>
        [Fact]
        public void ProductionMultiplier_ShouldFollowTheRing()
        {
            var geometry = new WorldGeometry(Config());
            var (cx, cy) = geometry.Centre;

            Assert.Equal(2.0, geometry.ProductionMultiplierAt(cx, cy, serverLevel: 3));
            Assert.Equal(1.4, geometry.ProductionMultiplierAt(cx + 50, cy, serverLevel: 3));
            Assert.Equal(1.0, geometry.ProductionMultiplierAt(cx + 100, cy, serverLevel: 3));
        }

        /// <summary>
        /// Туман розширюється з рівнем сервера: 40% радіуса на першому,
        /// увесь радіус на максимальному.
        /// </summary>
        [Theory]
        [InlineData(1, 60)]
        [InlineData(3, 150)]
        public void SettlementBoundary_ShouldGrowWithServerLevel(int serverLevel, int expected)
        {
            var geometry = new WorldGeometry(Config());

            Assert.Equal(expected, geometry.SettlementBoundary(serverLevel));
        }

        /// <summary>Клітина за межею туману недоступна для заселення.</summary>
        [Fact]
        public void IsWithinFog_ShouldRejectCellsBeyondTheBoundary()
        {
            var geometry = new WorldGeometry(Config());
            var (cx, cy) = geometry.Centre;

            Assert.True(geometry.IsWithinFog(cx + 60, cy, serverLevel: 1));
            Assert.False(geometry.IsWithinFog(cx + 61, cy, serverLevel: 1));
        }

        /// <summary>
        /// Межа туману ніколи не виходить за карту: інакше гравці отримували б
        /// «вільні» клітини за краєм, і розміщення падало б без пояснення.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void SettlementBoundary_ShouldStayWithinTheMap(int serverLevel)
        {
            var geometry = new WorldGeometry(Config());

            Assert.InRange(geometry.SettlementBoundary(serverLevel), 1, geometry.Radius);
        }

        /// <summary>Близькість: 1.0 у центрі, 0.0 на околиці — незалежно від кількості кілець.</summary>
        [Fact]
        public void Proximity_ShouldSpanFromOneToZero()
        {
            var geometry = new WorldGeometry(Config());
            var (cx, cy) = geometry.Centre;

            Assert.Equal(1.0, geometry.Proximity(cx, cy, serverLevel: 3));
            Assert.Equal(0.0, geometry.Proximity(cx + 100, cy, serverLevel: 3));
        }
    }
}
