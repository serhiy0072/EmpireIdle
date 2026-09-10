using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>
    /// Герой гейтить марш, тож його стан — це те, що вирішує, чи гравець
    /// узагалі має доступ до карти. Кожен перехід перевіряється окремо:
    /// мовчазний перехід із Wounded у Deployed повернув би на карту героя,
    /// який лежить у госпіталі.
    /// </summary>
    public class HeroTests
    {
        private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private static Hero CreateHero(string heroKey = "warrior_bran")
            => new(Guid.NewGuid(), Guid.NewGuid(), serverId: 1, heroKey, Now);

        // ---------- Створення ----------

        [Fact]
        public void NewHero_ShouldStartIdleAtFirstLevelOfFirstTier()
        {
            var hero = CreateHero();

            Assert.Equal(HeroState.Idle, hero.State);
            Assert.Equal(1, hero.Tier);
            Assert.Equal(1, hero.Level);
            Assert.Equal(0, hero.Constellation);
            Assert.True(hero.IsAvailable);
        }

        // ---------- Похід ----------

        [Fact]
        public void Deploy_ShouldMakeHeroUnavailable()
        {
            var hero = CreateHero();

            hero.Deploy(Now);

            Assert.Equal(HeroState.Deployed, hero.State);
            Assert.False(hero.IsAvailable);
        }

        /// <summary>
        /// Один герой веде один марш. Другий похід тим самим героєм означав би,
        /// що ліміт маршів, який і є кількістю героїв, нічого не обмежує.
        /// </summary>
        [Fact]
        public void Deploy_ShouldRejectAlreadyDeployedHero()
        {
            var hero = CreateHero();
            hero.Deploy(Now);

            Assert.Throws<InvalidStateException>(() => hero.Deploy(Now));
        }

        [Fact]
        public void Deploy_ShouldRejectWoundedHero()
        {
            var hero = CreateHero();
            hero.Wound(Now.AddHours(1), Now);

            Assert.Throws<InvalidStateException>(() => hero.Deploy(Now));
        }

        [Fact]
        public void ReturnHome_ShouldReleaseHero()
        {
            var hero = CreateHero();
            hero.Deploy(Now);

            hero.ReturnHome(Now.AddHours(2));

            Assert.Equal(HeroState.Idle, hero.State);
            Assert.True(hero.IsAvailable);
        }

        [Fact]
        public void ReturnHome_ShouldRejectHeroThatNeverLeft()
        {
            var hero = CreateHero();

            Assert.Throws<InvalidStateException>(() => hero.ReturnHome(Now));
        }

        // ---------- Госпіталь ----------

        /// <summary>
        /// Поранення застає героя в поході, тому воно не вимагає стану Idle:
        /// перевірка стану тут заблокувала б розбір бою.
        /// </summary>
        [Fact]
        public void Wound_ShouldTakeHeroOutOfTheField_EvenWhileDeployed()
        {
            var hero = CreateHero();
            hero.Deploy(Now);

            var healedAt = Now.AddHours(3);
            hero.Wound(healedAt, Now);

            Assert.Equal(HeroState.Wounded, hero.State);
            Assert.Equal(healedAt, hero.HealedAt);
            Assert.False(hero.IsAvailable);
        }

        [Fact]
        public void TryHeal_ShouldKeepHeroInHospital_BeforeTheDeadline()
        {
            var hero = CreateHero();
            hero.Wound(Now.AddHours(3), Now);

            var healed = hero.TryHeal(Now.AddHours(1));

            Assert.False(healed);
            Assert.Equal(HeroState.Wounded, hero.State);
        }

        [Fact]
        public void TryHeal_ShouldReleaseHero_AfterTheDeadline()
        {
            var hero = CreateHero();
            hero.Wound(Now.AddHours(3), Now);

            var healed = hero.TryHeal(Now.AddHours(4));

            Assert.True(healed);
            Assert.Equal(HeroState.Idle, hero.State);
            Assert.Null(hero.HealedAt);
        }

        /// <summary>
        /// Сканер таймерів проходить по всіх героях підряд, тож здоровий герой
        /// має повертати false, а не падати.
        /// </summary>
        [Fact]
        public void TryHeal_ShouldIgnoreHealthyHero()
        {
            var hero = CreateHero();

            Assert.False(hero.TryHeal(Now.AddDays(1)));
            Assert.Equal(HeroState.Idle, hero.State);
        }

        [Fact]
        public void HealInstantly_ShouldReleaseHeroBeforeTheDeadline()
        {
            var hero = CreateHero();
            hero.Wound(Now.AddHours(5), Now);

            hero.HealInstantly(Now);

            Assert.Equal(HeroState.Idle, hero.State);
            Assert.Null(hero.HealedAt);
        }

        /// <summary>Платити gems за лікування здорового героя не можна.</summary>
        [Fact]
        public void HealInstantly_ShouldRejectHealthyHero()
        {
            var hero = CreateHero();

            Assert.Throws<InvalidStateException>(() => hero.HealInstantly(Now));
        }

        // ---------- Рівні й тіри ----------

        [Fact]
        public void GainLevel_ShouldRaiseLevelBelowTheCeiling()
        {
            var hero = CreateHero();

            hero.GainLevel(maxLevel: 10, Now);

            Assert.Equal(2, hero.Level);
        }

        /// <summary>
        /// Стеля приходить ззовні: вона залежить від ратуші й тіру, а герой
        /// ні про село, ні про конфіг не знає.
        /// </summary>
        [Fact]
        public void GainLevel_ShouldRejectAtTheCeiling()
        {
            var hero = CreateHero();

            Assert.Throws<RequirementNotMetException>(() => hero.GainLevel(maxLevel: 1, Now));
            Assert.Equal(1, hero.Level);
        }

        [Fact]
        public void EvolveTier_ShouldRaiseTier()
        {
            var hero = CreateHero();

            hero.EvolveTier(maxTier: 3, Now);

            Assert.Equal(2, hero.Tier);
        }

        [Fact]
        public void EvolveTier_ShouldRejectAtTheHighestTier()
        {
            var hero = CreateHero();
            hero.EvolveTier(maxTier: 3, Now);
            hero.EvolveTier(maxTier: 3, Now);

            Assert.Throws<RequirementNotMetException>(() => hero.EvolveTier(maxTier: 3, Now));
            Assert.Equal(3, hero.Tier);
        }

        /// <summary>Еволюція не скидає рівень — інакше вона була б покаранням.</summary>
        [Fact]
        public void EvolveTier_ShouldKeepTheCurrentLevel()
        {
            var hero = CreateHero();
            hero.GainLevel(maxLevel: 10, Now);
            hero.GainLevel(maxLevel: 10, Now);

            hero.EvolveTier(maxTier: 3, Now);

            Assert.Equal(3, hero.Level);
        }

        // ---------- Сузір'я ----------

        [Fact]
        public void TryAddConstellation_ShouldAbsorbDuplicateBelowTheCap()
        {
            var hero = CreateHero();

            Assert.True(hero.TryAddConstellation(maxConstellation: 6, Now));
            Assert.Equal(1, hero.Constellation);
        }

        /// <summary>
        /// Понад стелю дублікат не зникає мовчки: false — сигнал викликачу
        /// конвертувати його за правилами гача.
        /// </summary>
        [Fact]
        public void TryAddConstellation_ShouldRefuseAtTheCap()
        {
            var hero = CreateHero();

            for (var i = 0; i < 6; i++)
                hero.TryAddConstellation(maxConstellation: 6, Now);

            Assert.False(hero.TryAddConstellation(maxConstellation: 6, Now));
            Assert.Equal(6, hero.Constellation);
        }

        // ---------- Токен паралелізму ----------

        /// <summary>
        /// UpdatedAt рухається на кожній мутації: інакше токен паралелізму
        /// на корені не спрацював би там, де правились лише дочірні рядки.
        /// </summary>
        [Fact]
        public void Mutations_ShouldMoveUpdatedAt()
        {
            var hero = CreateHero();
            var before = hero.UpdatedAt;

            hero.Deploy(Now.AddMinutes(5));

            Assert.True(hero.UpdatedAt > before);
        }
    }
}
