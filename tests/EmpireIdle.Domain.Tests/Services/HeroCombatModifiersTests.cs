using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Бонуси героя своєму війську. Головне тут — що множник завжди
    /// визначений: бойова формула не має розрізняти «герой є» і «ні».
    /// </summary>
    public class HeroCombatModifiersTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static readonly HeroPassiveConfig Shieldwall =
            TestKit.Passives.Defence(percent: 6, target: "infantry", perConstellation: 2);

        private static readonly HeroPassiveConfig HoldTheLine =
            TestKit.Passives.Defence(percent: 4, target: HeroCombatModifiers.AllUnits,
                unlockConstellation: 3, perConstellation: 1.5);

        private static StackBuff Buff(int constellation = 0)
            => TestKit.Passives.Buff(constellation, Shieldwall, HoldTheLine);

        [Fact]
        public void For_ShouldApplyAnUnlockedPassive()
            => Assert.Equal(1.06, Buff().Defense("infantry"), 3);

        /// <summary>Пасивка б'є лише по своїй цілі: лучникам вона нічого не дає.</summary>
        [Fact]
        public void For_ShouldNotLeakToOtherUnits()
            => Assert.Equal(1.0, Buff().Defense("archer"), 3);

        [Fact]
        public void For_ShouldIgnoreALockedPassive()
        {
            var buff = Buff(constellation: 2);

            // shieldwall: 6 + 2×2 = 10; hold_the_line ще закрита
            Assert.Equal(1.10, buff.Defense("infantry"), 3);
            Assert.Equal(1.0, buff.Defense("archer"), 3);
        }

        /// <summary>
        /// Бонус на все військо й бонус на тип складаються, а не множаться:
        /// 6+2×3 на піхоту плюс 4 на всіх дає 16%.
        /// </summary>
        [Fact]
        public void For_ShouldSumTheAllTargetWithTheSpecificOne()
        {
            var buff = Buff(constellation: 3);

            Assert.Equal(1.16, buff.Defense("infantry"), 3);
            Assert.Equal(1.04, buff.Defense("archer"), 3);
        }

        [Fact]
        public void For_ShouldNotTouchTheOtherStat()
            => Assert.Equal(1.0, Buff(constellation: 3).Attack("infantry"), 3);

        /// <summary>Невідомий тип юніта не валить формулу.</summary>
        [Fact]
        public void For_ShouldReturnOne_ForAnUnknownUnitType()
            => Assert.Equal(1.0, Buff().Defense("siege_ram"), 3);

        /// <summary>Поранений не дає нічого: у цьому й ціна поразки.</summary>
        [Fact]
        public void For_ShouldReturnNothing_WhenTheHeroIsWounded()
        {
            var config = new GameConfigBuilder()
                .WithUnits()
                .WithHeroes(passives: new[] { Shieldwall, HoldTheLine })
                .Build();
            var hero = TestKit.Entities.Hero(TestKeys.CommonHero, constellation: 3);
            hero.Wound(Now);

            var buff = new HeroCombatModifiers(new GameCatalog(config)).For(hero);

            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        [Fact]
        public void For_ShouldReturnNothing_WhenThereIsNoHero()
        {
            var buff = new HeroCombatModifiers(new GameCatalog(new GameConfigBuilder().WithUnits().WithHeroes(passives: Shieldwall).Build())).For(null);

            Assert.Equal(1.0, buff.Attack("infantry"), 3);
            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        [Fact]
        public void For_ShouldReturnNothing_WhenTheHeroHasNoPassives()
        {
            var config = new GameConfigBuilder().WithUnits().WithHeroes(passives: Shieldwall).Build();
            var hero = TestKit.Entities.Hero(TestKeys.PlainHero);

            var buff = new HeroCombatModifiers(new GameCatalog(config)).For(hero);

            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }
    }
}
