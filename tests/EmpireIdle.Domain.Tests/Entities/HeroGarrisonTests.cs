using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>Проживання героя в гарнізоні: лідерського слота немає (рішення 15.10.2026), лише «стоїть» чи «йде».</summary>
    public class HeroGarrisonTests
    {
        private static readonly DateTime Now = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        private static readonly Guid Garrison = Guid.NewGuid();

        private static Hero Stationed()
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "archer_lyra", Garrison, Now);

        [Fact]
        public void Deploy_ShouldClearTheGarrison()
        {
            var hero = Stationed();

            hero.Deploy(Guid.NewGuid(), Now);

            Assert.Null(hero.StationedGarrisonId);
            Assert.Equal(HeroState.Deployed, hero.State);
        }

        [Fact]
        public void Arrive_ShouldStationTheHero_AndFreeItForANewMarch()
        {
            var hero = Stationed();
            hero.Deploy(Guid.NewGuid(), Now);

            hero.Arrive(Garrison, Now);

            Assert.Equal(Garrison, hero.StationedGarrisonId);
            Assert.True(hero.IsAvailable);
        }
    }
}
