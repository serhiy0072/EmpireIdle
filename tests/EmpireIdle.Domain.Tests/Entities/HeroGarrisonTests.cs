using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>Проживання героя в гарнізоні й лідерський слот.</summary>
    public class HeroGarrisonTests
    {
        private static readonly DateTime Now = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        private static readonly Guid Garrison = Guid.NewGuid();

        private static Hero Stationed(bool asLeader)
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "archer_lyra", Garrison, asLeader, Now);

        [Fact]
        public void Deploy_ShouldClearGarrisonAndLeadership()
        {
            var hero = Stationed(asLeader: true);

            hero.Deploy(Now);

            Assert.Null(hero.StationedGarrisonId);
            Assert.False(hero.IsLeader);
            Assert.Equal(HeroState.Deployed, hero.State);
        }

        /// <summary>
        /// Слот зайняли, поки герой ішов. Він повертається рядовим,
        /// інакше на SaveChanges прилетіло б порушення індексу.
        /// </summary>
        [Fact]
        public void ReturnHome_ShouldNotRestoreLeadership_WhenTheSlotIsTaken()
        {
            var hero = Stationed(asLeader: true);
            hero.Deploy(Now);

            hero.Arrive(Garrison, leaderSlotFree: false, Now);

            Assert.Equal(Garrison, hero.StationedGarrisonId);
            Assert.False(hero.IsLeader);
            Assert.True(hero.IsAvailable);
        }

        [Fact]
        public void ReturnHome_ShouldRestoreLeadership_WhenTheSlotIsFree()
        {
            var hero = Stationed(asLeader: true);
            hero.Deploy(Now);

            hero.Arrive(Garrison, leaderSlotFree: true, Now);

            Assert.True(hero.IsLeader);
        }

        [Fact]
        public void AppointLeader_ShouldThrow_WhenTheHeroIsOnTheMove()
        {
            var hero = Stationed(asLeader: false);
            hero.Deploy(Now);

            Assert.Throws<InvalidStateException>(() => hero.AppointLeader(Now));
        }

        [Fact]
        public void StationIn_ShouldThrow_WhenTheHeroIsOnTheMove()
        {
            var hero = Stationed(asLeader: false);
            hero.Deploy(Now);

            Assert.Throws<InvalidStateException>(() => hero.StationIn(Garrison, asLeader: false, Now));
        }
    }
}
