using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
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
            => new(Guid.NewGuid(), Guid.NewGuid(), serverId: 1, heroKey, Guid.NewGuid(), asLeader: true, Now);

        private static Hero Stationed(Guid garrison, bool asLeader)
            => new(Guid.NewGuid(), Guid.NewGuid(), serverId: 1, "warrior_bran", garrison, asLeader, Now);

        // ---------- Створення ----------

        /// <summary>Сила рахується з ростера, тож кожна зміна героя, що її рухає, лишає подію.</summary>
        [Fact]
        public void HeroChanges_ShouldRaiseHeroChanged_ForPowerRecalculation()
        {
            var hero = CreateHero();
            Assert.Single(hero.DomainEvents.OfType<HeroChanged>());
            hero.ClearDomainEvents();

            hero.GainLevels(1, maxLevel: 10, Now);
            Assert.Single(hero.DomainEvents.OfType<HeroChanged>());
            hero.ClearDomainEvents();

            hero.EvolveTier(maxTier: 3, Now);
            Assert.Single(hero.DomainEvents.OfType<HeroChanged>());
            hero.ClearDomainEvents();

            hero.FillStarPart(maxStarParts: 36, Now);
            Assert.Single(hero.DomainEvents.OfType<HeroChanged>());
        }

        [Fact]
        public void NewHero_ShouldStartIdleAtFirstLevelOfFirstTier()
        {
            var hero = CreateHero();

            Assert.Equal(HeroState.Idle, hero.State);
            Assert.Equal(1, hero.Tier);
            Assert.Equal(1, hero.Level);
            Assert.Equal(0, hero.StarParts);
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
            hero.Wound(Now);

            Assert.Throws<InvalidStateException>(() => hero.Deploy(Now));
        }

        [Fact]
        public void ReturnHome_ShouldReleaseHero()
        {
            var garrison = Guid.NewGuid();
            var hero = Stationed(garrison, asLeader: false);
            hero.Deploy(Now);

            hero.Arrive(garrison, leaderSlotFree: false, Now.AddHours(2));

            Assert.Equal(HeroState.Idle, hero.State);
            Assert.True(hero.IsAvailable);
            Assert.Equal(garrison, hero.StationedGarrisonId);
        }

        [Fact]
        public void ReturnHome_ShouldRejectHeroThatNeverLeft()
        {
            var hero = CreateHero();

            Assert.Throws<InvalidStateException>(() => hero.Arrive(Guid.NewGuid(), leaderSlotFree: true, Now));
        }

        // ---------- Госпіталь ----------

        [Fact]
        public void Wound_ShouldKeepLeadership()
        {
            var garrison = Guid.NewGuid();
            var hero = Stationed(garrison, asLeader: true);

            hero.Wound(Now);

            Assert.Equal(HeroState.Wounded, hero.State);
            Assert.True(hero.IsLeader);
            Assert.False(hero.IsAvailable);
        }

        [Fact]
        public void Wound_ShouldBeIdempotent()
        {
            var hero = CreateHero();
            hero.Wound(Now);

            hero.Wound(Now.AddHours(1));

            Assert.Equal(HeroState.Wounded, hero.State);
        }

        /// <summary>Поранений у поході доїжджає й лишається пораненим.</summary>
        [Fact]
        public void Arrive_ShouldKeepTheWoundedState()
        {
            var garrison = Guid.NewGuid();
            var hero = Stationed(garrison, asLeader: false);
            hero.Deploy(Now);
            hero.Wound(Now);

            hero.Arrive(garrison, leaderSlotFree: true, Now.AddHours(1));

            Assert.Equal(HeroState.Wounded, hero.State);
            Assert.Equal(garrison, hero.StationedGarrisonId);
        }

        [Fact]
        public void SendHome_ShouldMoveTheWoundedHero()
        {
            var hero = Stationed(Guid.NewGuid(), asLeader: true);
            hero.Wound(Now);

            hero.SendHome(Now);

            Assert.Null(hero.StationedGarrisonId);
            Assert.False(hero.IsLeader);
            Assert.Equal(HeroState.Wounded, hero.State);
        }

        [Fact]
        public void Heal_ShouldRejectHeroOnTheMove()
        {
            var hero = Stationed(Guid.NewGuid(), asLeader: false);
            hero.Wound(Now);
            hero.SendHome(Now);

            Assert.Throws<InvalidStateException>(() => hero.Heal(Now));
        }

        [Fact]
        public void Heal_ShouldRejectHealthyHero()
        {
            var hero = Stationed(Guid.NewGuid(), asLeader: false);

            Assert.Throws<InvalidStateException>(() => hero.Heal(Now));
        }

        // ---------- Рівні й тіри ----------

        /// <summary>Рівень піднімається одразу на кілька — досвід списано з пулу ще до цього.</summary>
        [Fact]
        public void GainLevels_ShouldRaiseSeveralLevelsAtOnce()
        {
            var hero = CreateHero();

            hero.GainLevels(4, maxLevel: 80, Now);

            Assert.Equal(5, hero.Level);
        }

        /// <summary>Понад стелю — відмова, рівень не змінюється навіть частково.</summary>
        [Fact]
        public void GainLevels_ShouldRefuse_AboveTheCeiling()
        {
            var hero = CreateHero();

            var refusal = Assert.Throws<RequirementNotMetException>(() => hero.GainLevels(5, maxLevel: 5, Now));

            Assert.Equal(RefusalReasons.HeroLevelCeiling.Key, refusal.Reason);
            Assert.Equal(1, hero.Level);
        }

        /// <summary>Скидання — на перший рівень; повертає рівень до скидання, щоб викликач порахував досвід.</summary>
        [Fact]
        public void ResetLevel_ShouldReturnToLevelOne()
        {
            var hero = CreateHero();
            hero.GainLevels(9, maxLevel: 80, Now);

            var previous = hero.ResetLevel(Now);

            Assert.Equal(10, previous);
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

            var refusal = Assert.Throws<RequirementNotMetException>(() => hero.EvolveTier(maxTier: 3, Now));
            Assert.Equal(RefusalReasons.HeroMaxTier.Key, refusal.Reason);
            Assert.Equal(3, hero.Tier);
        }

        /// <summary>Еволюція не скидає рівень — інакше вона була б покаранням.</summary>
        [Fact]
        public void EvolveTier_ShouldKeepTheCurrentLevel()
        {
            var hero = CreateHero();
            hero.GainLevels(2, maxLevel: 10, Now);

            hero.EvolveTier(maxTier: 3, Now);

            Assert.Equal(3, hero.Level);
        }

        // ---------- Вміння ----------

        [Fact]
        public void SkillLevel_ShouldStartAtOne_WithoutBooks()
            => Assert.Equal(1, TestKit.Entities.Hero().SkillLevel("fury"));

        [Fact]
        public void RaiseSkill_ShouldAddOneLevel_AndRaiseAChange()
        {
            var hero = TestKit.Entities.Hero();
            hero.ClearDomainEvents();

            hero.RaiseSkill("fury", levelCap: 3, maxSkillLevel: 6, Now);

            Assert.Equal(2, hero.SkillLevel("fury"));
            Assert.Equal(1, hero.SkillLevel("wall"));
            Assert.Contains(hero.DomainEvents, e => e is HeroChanged);
        }

        /// <summary>Наступний рівень відкриває зірка: з двома зірками вміння доходить до третього рівня й не далі.</summary>
        [Fact]
        public void RaiseSkill_ShouldStopAtTheStarCap()
        {
            var hero = TestKit.Entities.Hero();
            hero.RaiseSkill("fury", levelCap: 3, maxSkillLevel: 6, Now);
            hero.RaiseSkill("fury", levelCap: 3, maxSkillLevel: 6, Now);

            var refusal = Assert.Throws<RequirementNotMetException>(() => hero.RaiseSkill("fury", levelCap: 3, maxSkillLevel: 6, Now));

            Assert.Equal(RefusalReasons.HeroSkillStarCapped.Key, refusal.Reason);
            Assert.Equal(3, hero.SkillLevel("fury"));
        }

        [Fact]
        public void RaiseSkill_ShouldStopAtTheTopLevel()
        {
            var hero = TestKit.Entities.Hero();

            for (var i = 0; i < 5; i++)
                hero.RaiseSkill("fury", levelCap: 6, maxSkillLevel: 6, Now);

            var refusal = Assert.Throws<RequirementNotMetException>(() => hero.RaiseSkill("fury", levelCap: 6, maxSkillLevel: 6, Now));

            Assert.Equal(RefusalReasons.HeroSkillMaxed.Key, refusal.Reason);
        }

        // ---------- Зірки ----------

        [Fact]
        public void FillStarPart_ShouldAddOnePart()
        {
            var hero = CreateHero();

            hero.FillStarPart(maxStarParts: 36, Now);

            Assert.Equal(1, hero.StarParts);
        }

        /// <summary>Усі зірки заповнені — відмова; надлишок осколків банк робить універсальними, а не тут.</summary>
        [Fact]
        public void FillStarPart_ShouldRefuseWhenEveryStarIsFull()
        {
            var hero = CreateHero();

            for (var i = 0; i < 36; i++)
                hero.FillStarPart(maxStarParts: 36, Now);

            var refusal = Assert.Throws<RequirementNotMetException>(() => hero.FillStarPart(maxStarParts: 36, Now));
            Assert.Equal(RefusalReasons.HeroMaxStars.Key, refusal.Reason);
            Assert.Equal(36, hero.StarParts);
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
