using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>
    /// Щит після падіння міста (GDD §2.6): ставиться разом із падінням,
    /// спадає сам за часом або від власного нападу.
    /// </summary>
    public class VillageShieldTests
    {
        private static readonly DateTime Now = TestKit.Entities.Now;

        private static VillageFall FallTo(Village village, int x, int y)
            => new(Guid.NewGuid(), 1, village.PlayerId, Guid.NewGuid(), "Нападник", village.X, village.Y, x, y,
                Now.AddHours(24), Now);

        private static Village Fallen()
        {
            var village = TestKit.Entities.Village(x: 4, y: 7);
            var fall = FallTo(village, 10, 12);

            village.RelocateTo(10, 12, Now);
            village.MarkFallen(fall, Now);

            return village;
        }

        [Fact]
        public void MarkFallen_ShouldRaiseTheShieldAndTheEvent()
        {
            var village = Fallen();

            Assert.True(village.IsShieldedAt(Now.AddHours(23)));
            Assert.False(village.IsShieldedAt(Now.AddHours(24)));
            Assert.Single(village.DomainEvents.OfType<VillageFell>());
        }

        /// <summary>Падіння фіксується лише після переїзду: інакше щит стояв би на старому місці.</summary>
        [Fact]
        public void MarkFallen_ShouldRefuse_BeforeTheVillageIsRelocated()
        {
            var village = TestKit.Entities.Village(x: 4, y: 7);

            Assert.Throws<InvalidOperationException>(() => village.MarkFallen(FallTo(village, 10, 12), Now));
            Assert.False(village.IsShieldedAt(Now));
        }

        [Fact]
        public void DropShield_ShouldLiftTheShield()
        {
            var village = Fallen();

            village.DropShield(Now.AddHours(1));

            Assert.False(village.IsShieldedAt(Now.AddHours(1)));
        }

        [Fact]
        public void NewVillage_ShouldHaveNoShield()
        {
            Assert.False(TestKit.Entities.Village(x: 4, y: 7).IsShieldedAt(Now));
        }
    }
}
