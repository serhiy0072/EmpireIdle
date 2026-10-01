using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>Очки вкладу — кланова валюта: набігають від учасників і квестів, тратяться на споруди.</summary>
    public class ClanContributionTests
    {
        private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        private static (Clan Clan, Guid Founder) NewClan()
        {
            var founder = Guid.NewGuid();
            return (new Clan(Guid.NewGuid(), 1, "Northern Watch", "NW", founder, Now), founder);
        }

        [Fact]
        public void EarnPoints_ShouldCreditTheClanAndTheMember()
        {
            var (clan, founder) = NewClan();

            clan.EarnPoints(120, founder, Now);

            Assert.Equal(120, clan.ContributionPoints);
            Assert.Equal(120, clan.Members.Single(m => m.PlayerId == founder).Contribution);
        }

        /// <summary>Квест клану вносить без автора — особистий внесок нікому не пишеться.</summary>
        [Fact]
        public void EarnPoints_ShouldCreditOnlyTheClan_WithoutAMember()
        {
            var (clan, founder) = NewClan();

            clan.EarnPoints(300, null, Now);

            Assert.Equal(300, clan.ContributionPoints);
            Assert.Equal(0, clan.Members.Single(m => m.PlayerId == founder).Contribution);
        }

        [Fact]
        public void SpendPoints_ShouldRefuseWithTheShortfall_WhenNotEnough()
        {
            var (clan, _) = NewClan();
            clan.EarnPoints(100, null, Now);

            var refusal = Assert.Throws<RequirementNotMetException>(() => clan.SpendPoints(250, Now));

            Assert.Equal(RefusalReasons.ClanNotEnoughPoints.Key, refusal.Reason);
            Assert.Equal(100, clan.ContributionPoints);
        }

        [Fact]
        public void SpendPoints_ShouldDeduct_WhenEnough()
        {
            var (clan, _) = NewClan();
            clan.EarnPoints(1000, null, Now);

            clan.SpendPoints(400, Now);

            Assert.Equal(600, clan.ContributionPoints);
        }
    }
}
