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
            MaxLevel = 80,
            LevelExperienceBase = 100,
            LevelExperienceCoefficient = 2.2,
            LevelExperienceExponent = 1.55,
            FeedExperience = new Dictionary<Rarity, int> { [Rarity.Common] = 100, [Rarity.Rare] = 300 }
        });

        private static EquipmentItem Item(Rarity rarity)
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "amulet", EquipmentSlot.Artifact, rarity, Now);

        /// <summary>Кроки round10(100 + 2.2·(n − 1)^1.55): 100, 100, 110, 110, 120.</summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 100)]
        [InlineData(2, 100)]
        [InlineData(3, 110)]
        [InlineData(4, 110)]
        [InlineData(5, 120)]
        public void StepTo_ShouldFollowTheRoundedCurve(int level, long expected)
            => Assert.Equal(expected, Progression().StepTo(level));

        [Fact]
        public void ExperienceToReach_ShouldSumTheSteps()
            => Assert.Equal(420, Progression().ExperienceToReach(4));

        /// <summary>Уся крива до L80 — 68 520 досвіду, як у таблиці Режисера.</summary>
        [Fact]
        public void ExperienceToReach_ShouldMatchTheDirectorsTotal_AtTheCeiling()
            => Assert.Equal(68_520, Progression().ExperienceToReach(80));

        [Theory]
        [InlineData(0, 0)]
        [InlineData(99, 0)]
        [InlineData(100, 1)]
        [InlineData(419, 3)]
        [InlineData(420, 4)]
        public void LevelFor_ShouldPickTheHighestReachedLevel(long experience, int expected)
            => Assert.Equal(expected, Progression().LevelFor(experience));

        [Fact]
        public void LevelFor_ShouldStopAtTheCeiling()
            => Assert.Equal(80, Progression().LevelFor(long.MaxValue / 2));

        [Fact]
        public void ExperienceToNext_ShouldCountFromTheCurrentExperience()
        {
            var item = Item(Rarity.Common);
            item.GainExperience(160, 1, Now);

            // До 2-го рівня треба 200 накопиченого
            Assert.Equal(40, Progression().ExperienceToNext(item));
        }

        [Fact]
        public void ExperienceToNext_ShouldBeNull_AtTheCeiling()
        {
            var progression = Progression();
            var item = Item(Rarity.Common);
            item.GainExperience(progression.ExperienceToReach(80), 80, Now);

            Assert.Null(progression.ExperienceToNext(item));
        }

        /// <summary>Згодований передає базу за рідкість і весь вкладений досвід.</summary>
        [Fact]
        public void FeedValue_ShouldPassTheBasisAndAllInvestedExperience()
        {
            var item = Item(Rarity.Rare);
            item.GainExperience(200, 2, Now);

            Assert.Equal(500, Progression().FeedValue(item));
        }

        [Fact]
        public void FeedValue_ShouldBeNull_ForARarityWithoutFeedExperience()
            => Assert.Null(Progression().FeedValue(Item(Rarity.Unique)));
    }
}
