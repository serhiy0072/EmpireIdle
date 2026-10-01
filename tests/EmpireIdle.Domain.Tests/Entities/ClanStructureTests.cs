using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Tests.Entities
{
    public class ClanStructureTests
    {
        private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan BuildDuration = TimeSpan.FromHours(10);

        private static ClanStructure Structure(int x = 50, int y = 50) => new(Guid.NewGuid(), 1, Guid.NewGuid(), x, y,
            Guid.NewGuid(), Guid.NewGuid(), BuildDuration, Now);

        [Fact]
        public void NewStructure_ShouldBecomeActive_OnlyWhenTheBuildTimeRunsOut()
        {
            var structure = Structure();

            Assert.False(structure.IsActiveAt(Now));
            Assert.False(structure.IsActiveAt(Now + BuildDuration - TimeSpan.FromSeconds(1)));
            Assert.True(structure.IsActiveAt(Now + BuildDuration));
        }

        /// <summary>Частка маршу зрізає відповідну частину повного будівництва.</summary>
        [Fact]
        public void Accelerate_ShouldCutItsShareOfTheFullBuild()
        {
            var structure = Structure();

            var applied = structure.Accelerate(0.25, maxShare: 1.0, Now);

            Assert.Equal(0.25, applied, 6);
            Assert.Equal(Now + TimeSpan.FromHours(7.5), structure.CompletesAt);
        }

        /// <summary>Стеля ділиться між усіма маршами: понад неї нічого не зрізається.</summary>
        [Fact]
        public void Accelerate_ShouldStopAtTheCap()
        {
            var structure = Structure();

            structure.Accelerate(0.3, maxShare: 0.4, Now);
            var second = structure.Accelerate(0.3, maxShare: 0.4, Now);
            var third = structure.Accelerate(0.3, maxShare: 0.4, Now);

            Assert.Equal(0.1, second, 6);
            Assert.Equal(0, third);
            Assert.Equal(0.4, structure.AcceleratedShare, 6);
        }

        /// <summary>Клан у повній явці добудовує миттєво — але не в минулому.</summary>
        [Fact]
        public void Accelerate_ShouldFinishNow_WhenTheFullBuildIsCovered()
        {
            var structure = Structure();
            var later = Now + TimeSpan.FromHours(4);

            structure.Accelerate(1.0, maxShare: 1.0, later);

            Assert.Equal(later, structure.CompletesAt);
            Assert.True(structure.IsActiveAt(later));
        }

        [Fact]
        public void Accelerate_ShouldDoNothing_OnAFinishedStructure()
        {
            var structure = Structure();
            var afterBuild = Now + BuildDuration;

            var applied = structure.Accelerate(0.5, maxShare: 1.0, afterBuild);

            Assert.Equal(0, applied);
            Assert.Equal(Now + BuildDuration, structure.CompletesAt);
        }

        /// <summary>Радіус — квадрат, як кільця карти: радіус 5 покриває 11×11.</summary>
        [Theory]
        [InlineData(55, 55, true)]
        [InlineData(45, 45, true)]
        [InlineData(56, 50, false)]
        [InlineData(50, 44, false)]
        public void Covers_ShouldUseASquareRadius(int x, int y, bool expected)
        {
            var structure = Structure(50, 50);

            Assert.Equal(expected, structure.Covers(x, y, radius: 5));
        }
    }
}
