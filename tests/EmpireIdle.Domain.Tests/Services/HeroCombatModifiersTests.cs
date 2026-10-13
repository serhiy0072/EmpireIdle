using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Enums;
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

        private static readonly HeroSkillConfig Shieldwall =
            TestKit.Passives.Defence(percent: 6, target: "infantry");

        /// <summary>Відкривається лише з 20 рівня героя — як друге вміння половини в конфігу гри.</summary>
        private static readonly HeroSkillConfig HoldTheLine =
            TestKit.Passives.Defence(percent: 4, target: HeroCombatModifiers.AllUnits, unlockLevel: 20);

        private static StackBuff Buff(int heroLevel = 1)
            => TestKit.Passives.Buff(heroLevel, Shieldwall, HoldTheLine);

        private static HeroCombatModifiers Modifiers(params HeroSkillConfig[] skills)
            => new(new GameCatalog(new GameConfigBuilder().WithUnits().WithHeroes(passives: skills).Build()));

        [Fact]
        public void For_ShouldApplyAnUnlockedPassive()
            => Assert.Equal(1.06, Buff().Defense("infantry"), 3);

        /// <summary>Пасивка б'є лише по своїй цілі: лучникам вона нічого не дає.</summary>
        [Fact]
        public void For_ShouldNotLeakToOtherUnits()
            => Assert.Equal(1.0, Buff().Defense("archer"), 3);

        /// <summary>Вміння, яке рівень героя ще не відкрив, не дає нічого.</summary>
        [Fact]
        public void For_ShouldIgnoreASkillLockedByHeroLevel()
        {
            var buff = Buff(heroLevel: 19);

            Assert.Equal(1.06, buff.Defense("infantry"), 3);
            Assert.Equal(1.0, buff.Defense("archer"), 3);
        }

        /// <summary>
        /// Бонус на все військо й бонус на тип складаються, а не множаться:
        /// 6 на піхоту плюс 4 на всіх дає 10%.
        /// </summary>
        [Fact]
        public void For_ShouldSumTheAllTargetWithTheSpecificOne()
        {
            var buff = Buff(heroLevel: 20);

            Assert.Equal(1.10, buff.Defense("infantry"), 3);
            Assert.Equal(1.04, buff.Defense("archer"), 3);
        }

        [Fact]
        public void For_ShouldNotTouchTheOtherStat()
            => Assert.Equal(1.0, Buff(heroLevel: 20).Attack("infantry"), 3);

        /// <summary>Невідомий тип юніта не валить формулу.</summary>
        [Fact]
        public void For_ShouldReturnOne_ForAnUnknownUnitType()
            => Assert.Equal(1.0, Buff().Defense("siege_ram"), 3);

        /// <summary>Активне в марші не б'є саме, а дає бонус війську — як і пасивка (GDD §6.1).</summary>
        [Fact]
        public void For_ShouldApplyTheTroopBonusOfAnActiveSkill()
        {
            var active = new HeroSkillConfig
            {
                Key = "smash", DisplayName = "Smash", Half = SkillHalf.Attack, Kind = SkillKind.Active,
                Troops = new SkillTroopBonusConfig { Target = HeroCombatModifiers.AllUnits, Stat = "Attack", Percents = [5, 10, 15, 20, 25, 30] },
                Battle = new SkillBattleConfig { Cooldown = 3, DamageMultiplier = 2, LevelScale = [1, 1, 1, 1, 1, 1] },
            };

            var buff = Modifiers(active).For(TestKit.Entities.Hero(TestKeys.CommonHero));

            Assert.Equal(1.05, buff.Attack("infantry"), 3);
        }

        /// <summary>Небойове вміння бою не підсилює — воно про логістику.</summary>
        [Fact]
        public void For_ShouldIgnoreAUtilitySkill()
        {
            var swift = new HeroSkillConfig
            {
                Key = "swift", DisplayName = "Swift", Half = SkillHalf.Defense, Kind = SkillKind.Utility,
                Utility = new SkillUtilityConfig { Effect = SkillUtilityConfig.MarchSpeed, Percents = [5, 10, 15, 20, 25, 30] },
            };

            var buff = Modifiers(swift).For(TestKit.Entities.Hero(TestKeys.CommonHero));

            Assert.Equal(1.0, buff.Attack("infantry"), 3);
            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        /// <summary>Поранений не дає нічого: у цьому й ціна поразки.</summary>
        [Fact]
        public void For_ShouldReturnNothing_WhenTheHeroIsWounded()
        {
            var hero = TestKit.Entities.Hero(TestKeys.CommonHero, level: 20);
            hero.Wound(Now);

            var buff = Modifiers(Shieldwall, HoldTheLine).For(hero);

            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        [Fact]
        public void For_ShouldReturnNothing_WhenThereIsNoHero()
        {
            var buff = Modifiers(Shieldwall).For(null);

            Assert.Equal(1.0, buff.Attack("infantry"), 3);
            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        [Fact]
        public void For_ShouldReturnNothing_WhenTheHeroHasNoSkills()
        {
            var buff = Modifiers(Shieldwall).For(TestKit.Entities.Hero(TestKeys.PlainHero));

            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        /// <summary>Лідер маршу в стані Deployed — і саме в ньому б'ється. Регресія: IsAvailable гасив йому бонус.</summary>
        [Fact]
        public void For_ShouldApplySkills_WhenTheHeroLeadsAMarch()
        {
            var hero = TestKit.Entities.Hero(TestKeys.CommonHero, level: 20);
            hero.Deploy(Guid.NewGuid(), Now);

            var buff = Modifiers(Shieldwall, HoldTheLine).For(hero);

            Assert.Equal(1.10, buff.Defense("infantry"), 3);
        }

        [Fact]
        public void For_ShouldReturnNothing_WhenTheHeroIsWoundedOnTheMove()
        {
            var hero = TestKit.Entities.Hero(TestKeys.CommonHero, level: 20);
            hero.Deploy(Guid.NewGuid(), Now);
            hero.Wound(Now);

            var buff = Modifiers(Shieldwall, HoldTheLine).For(hero);

            Assert.Equal(1.0, buff.Defense("infantry"), 3);
        }

        // ---------- Марш (GDD §6.1: бойові вміння — лише на власні конвої) ----------

        private static HeroCombatModifiers MarchModifiers(params HeroSkillConfig[] skills)
            => new(new GameCatalog(new GameConfigBuilder().WithUnits()
                .WithHeroes(h => h.RoleUnits = new Dictionary<string, string>
                {
                    ["warrior"] = TestKeys.Infantry, ["knight"] = "cavalry", ["archer"] = TestKeys.Archer, ["mage"] = TestKeys.Archer,
                }, passives: skills)
                .Build()));

        /// <summary>Бонус «усьому війську» в марші діє лише на конвої героя — піхоту воїна.</summary>
        [Fact]
        public void ForMarch_ShouldTurnAnAllTargetIntoTheHerosOwnConvoys()
        {
            var buff = MarchModifiers(HoldTheLine).ForMarch([TestKit.Entities.Hero(TestKeys.CommonHero, level: 20)]);

            Assert.Equal(1.04, buff.Defense(TestKeys.Infantry), 3);
            Assert.Equal(1.0, buff.Defense("archer"), 3);
        }

        /// <summary>Вміння на чужий тип юнітів у марші нічого не дає: цих юнітів веде інший герой.</summary>
        [Fact]
        public void ForMarch_ShouldDropABonusForAnotherRolesUnits()
        {
            var archers = TestKit.Passives.Defence(percent: 7, target: "archer");

            var buff = MarchModifiers(archers).ForMarch([TestKit.Entities.Hero(TestKeys.CommonHero)]);

            Assert.Equal(1.0, buff.Defense("archer"), 3);
        }

        /// <summary>Лідер гарнізону, на відміну від маршу, підсилює всю оборону — For лишає ціль як є.</summary>
        [Fact]
        public void For_ShouldKeepTheAllTarget_ForAGarrisonLeader()
        {
            var buff = MarchModifiers(HoldTheLine).For(TestKit.Entities.Hero(TestKeys.CommonHero, level: 20));

            Assert.Equal(1.04, buff.Defense("archer"), 3);
        }
    }
}
