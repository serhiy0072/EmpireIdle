using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>Крива рівня артефакта й цінність згодованого спорядження (GDD §9.12).</summary>
    public class ArtifactProgressionTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static ArtifactProgression Progression() => new(new EquipmentConfig
        {
            MaxLevel = 20,
            LevelExperienceBase = 40,
            LevelExperienceExponent = 1.4,
            FeedExperience = new Dictionary<Rarity, int> { [Rarity.Common] = 100, [Rarity.Rare] = 300 }
        });

        private static EquipmentItem Item(Rarity rarity)
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "amulet", EquipmentSlot.Artifact, rarity, [("Attack", 5.0)], Now);

        /// <summary>Кроки кривої округлюються до десятків: 40, 110, 190, 280.</summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 40)]
        [InlineData(2, 110)]
        [InlineData(3, 190)]
        [InlineData(4, 280)]
        public void StepTo_ShouldFollowTheRoundedCurve(int level, long expected)
            => Assert.Equal(expected, Progression().StepTo(level));

        /// <summary>Перший новий стат (рівень 4) — 620 досвіду, шість звичайних артефактів.</summary>
        [Fact]
        public void ExperienceToReach_ShouldSumTheSteps()
            => Assert.Equal(620, Progression().ExperienceToReach(4));

        [Theory]
        [InlineData(0, 0)]
        [InlineData(39, 0)]
        [InlineData(40, 1)]
        [InlineData(619, 3)]
        [InlineData(620, 4)]
        public void LevelFor_ShouldPickTheHighestReachedLevel(long experience, int expected)
            => Assert.Equal(expected, Progression().LevelFor(experience));

        [Fact]
        public void LevelFor_ShouldStopAtTheCeiling()
            => Assert.Equal(20, Progression().LevelFor(long.MaxValue / 2));

        [Fact]
        public void ExperienceToNext_ShouldCountFromTheCurrentExperience()
        {
            var item = Item(Rarity.Common);
            item.GainExperience(60, 1, Now);

            // До 2-го рівня треба 150 накопиченого
            Assert.Equal(90, Progression().ExperienceToNext(item));
        }

        [Fact]
        public void ExperienceToNext_ShouldBeNull_AtTheCeiling()
        {
            var progression = Progression();
            var item = Item(Rarity.Common);
            item.GainExperience(progression.ExperienceToReach(20), 20, Now);

            Assert.Null(progression.ExperienceToNext(item));
        }

        /// <summary>Згодований передає базу за рідкість і весь вкладений досвід.</summary>
        [Fact]
        public void FeedValue_ShouldPassTheBasisAndAllInvestedExperience()
        {
            var item = Item(Rarity.Rare);
            item.GainExperience(150, 2, Now);

            Assert.Equal(450, Progression().FeedValue(item));
        }

        [Fact]
        public void FeedValue_ShouldBeNull_ForUniqueGear()
            => Assert.Null(Progression().FeedValue(Item(Rarity.Unique)));
    }
}
