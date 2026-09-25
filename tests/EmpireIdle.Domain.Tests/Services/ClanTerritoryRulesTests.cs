using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services
{
    public class ClanTerritoryRulesTests
    {
        private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        private static ClanTerritoryRules Rules(Action<ClanTerritoryConfig>? tune = null)
        {
            var config = new GameConfigBuilder().WithBuildings().Build();
            config.Quests =
            [
                new QuestConfig
                {
                    Key = "clan_hunt",
                    DisplayName = "Clan hunt",
                    Scope = QuestScope.Clan,
                    Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 100 }]
                }
            ];
            config.Clan.Territory = new ClanTerritoryConfig
            {
                Enabled = true,
                Radius = 5,
                MaxStructures = 10,
                StartingSlots = 5,
                SlotUnlocks =
                [
                    new ClanSlotUnlockConfig { MinMembers = 20 },
                    new ClanSlotUnlockConfig { MinMembers = 50 },
                    new ClanSlotUnlockConfig { QuestKey = "clan_hunt" },
                ],
                AttackBonus = 0.1,
                DefenceBonus = 0.2,
                BuildSharePerPower = 0.001,
                MonsterLootCashbackShare = 0.05,
            };
            tune?.Invoke(config.Clan.Territory);

            return new ClanTerritoryRules(new GameCatalog(config));
        }

        private static ClanStructure ActiveAt(int x, int y)
            => new(Guid.NewGuid(), 1, Guid.NewGuid(), x, y, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero, Now);

        [Theory]
        [InlineData(5, 5)]
        [InlineData(20, 6)]
        [InlineData(60, 7)]
        public void SlotsFor_ShouldAddAMemberThresholdSlot_WhenReached(int members, int expected)
            => Assert.Equal(expected, Rules().SlotsFor(members, []));

        [Fact]
        public void SlotsFor_ShouldAddAQuestSlot_WhenTheQuestIsDone()
            => Assert.Equal(6, Rules().SlotsFor(5, ["clan_hunt"]));

        [Fact]
        public void SlotsFor_ShouldNeverExceedTheStructureCap()
            => Assert.Equal(6, Rules(c => c.MaxStructures = 6).SlotsFor(60, ["clan_hunt"]));

        /// <summary>Кількість споруд розширює покриття, а не бонус: подвійне покриття — той самий множник.</summary>
        [Fact]
        public void Multipliers_ShouldNotStack_WhenStructuresOverlap()
        {
            var rules = Rules();
            var overlapping = new[] { ActiveAt(50, 50), ActiveAt(52, 52) };

            var covered = rules.IsCovered(overlapping, 51, 51, Now);

            Assert.True(covered);
            Assert.Equal(1.1, rules.AttackMultiplier(covered), 6);
            Assert.Equal(1.2, rules.DefenceMultiplier(covered), 6);
        }

        [Fact]
        public void IsCovered_ShouldIgnoreStructuresStillUnderConstruction()
        {
            var building = new ClanStructure(Guid.NewGuid(), 1, Guid.NewGuid(), 50, 50, Guid.NewGuid(), Guid.NewGuid(),
                TimeSpan.FromHours(1), Now);

            Assert.False(Rules().IsCovered([building], 50, 50, Now));
        }

        [Fact]
        public void IsCovered_ShouldBeFalse_WhenTheWorldHasNoTerritory()
            => Assert.False(Rules(c => c.Enabled = false).IsCovered([ActiveAt(50, 50)], 50, 50, Now));

        [Fact]
        public void Multipliers_ShouldBeOne_OutsideTheRadius()
        {
            var rules = Rules();

            Assert.Equal(1, rules.AttackMultiplier(false));
            Assert.Equal(1, rules.DefenceMultiplier(false));
        }

        /// <summary>Сила, а не кількість маршів: удвічі сильніший марш зрізає вдвічі більше.</summary>
        [Fact]
        public void BuildShareFor_ShouldGrowWithMarchPower()
        {
            var rules = Rules();

            Assert.Equal(0.1, rules.BuildShareFor(100), 6);
            Assert.Equal(0.2, rules.BuildShareFor(200), 6);
            Assert.Equal(0, rules.BuildShareFor(-5));
        }

        [Fact]
        public void CashbackFor_ShouldBeAShareOfTheLoot_RoundedDown()
            => Assert.Equal(52, Rules().CashbackFor(new Dictionary<string, int> { ["gold"] = 1000, ["food"] = 50 }));
    }
}
