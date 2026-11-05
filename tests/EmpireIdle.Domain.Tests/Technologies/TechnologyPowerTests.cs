using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Tests.Technologies;

/// <summary>Сила технологій: рівень × сила тіру × вага шкали; і події, що її перераховують.</summary>
public class TechnologyPowerTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<TechnologyScaling, double> Weights = new()
    {
        [TechnologyScaling.Battle] = 2,
        [TechnologyScaling.Convoy] = 2,
        [TechnologyScaling.None] = 1
    };

    private static readonly TechnologyTree Tree = new(
        [new TechnologyTierConfig { Tier = 1, PowerPerLevel = 30 }, new TechnologyTierConfig { Tier = 2, PowerPerLevel = 120 }],
        [
            new TechnologyNodeConfig { Key = "battle", Tab = "t", Row = 1, Levels = 5, Scaling = TechnologyScaling.Battle },
            new TechnologyNodeConfig { Key = "economy", Tab = "t", Row = 1, Levels = 5, Scaling = TechnologyScaling.None }
        ]);

    [Fact]
    public void Power_ShouldBeZero_WithNothingLearned()
        => Assert.Equal(0, Tree.Power(new Dictionary<string, int>(), Weights));

    /// <summary>3 × 30 × 2 бойових + 2 × 120 × 1 економічних на другому тірі.</summary>
    [Fact]
    public void Power_ShouldMultiplyLevelTierAndWeight()
        => Assert.Equal(180 + 240, Tree.Power(new Dictionary<string, int> { ["1:battle"] = 3, ["2:economy"] = 2 }, Weights));

    /// <summary>Кожен вивчений рівень дає силу, зокрема небойовий.</summary>
    [Fact]
    public void Power_ShouldCountNonBattleNodes()
        => Assert.Equal(30, Tree.Power(new Dictionary<string, int> { ["1:economy"] = 1 }, Weights));

    [Fact]
    public void Power_ShouldTreatAMissingWeightAsOne()
        => Assert.Equal(30, Tree.Power(new Dictionary<string, int> { ["1:battle"] = 1 }, new Dictionary<TechnologyScaling, double>()));

    /// <summary>Вузол чи тір прибрали з конфіга: рахунок не падає, а зайвий рівень не дає сили понад стелю.</summary>
    [Fact]
    public void Power_ShouldSkipUnknownKeys_AndClipToTheNodeCeiling()
    {
        var levels = new Dictionary<string, int> { ["1:removed"] = 3, ["9:battle"] = 3, ["garbage"] = 3, ["1:battle"] = 99 };

        Assert.Equal(5 * 30 * 2, Tree.Power(levels, Weights));
    }

    [Fact]
    public void PlayerPower_ShouldSumEveryPart()
    {
        var power = new PlayerPower(Guid.NewGuid(), Guid.NewGuid(), 1, Now);

        power.Set(army: 1, hero: 2, equipment: 4, technology: 8, clanTechnology: 16, Now);

        Assert.Equal(31, power.TotalPower);
        Assert.Equal(8, power.TechnologyPower);
        Assert.Equal(16, power.ClanTechnologyPower);
    }

    private static Clan ClanOf(Guid leader) => new(Guid.NewGuid(), 1, "Alpha", "ALP", leader, Now);

    [Fact]
    public void Join_ShouldRaiseMembershipChanged()
    {
        var player = Guid.NewGuid();
        var clan = ClanOf(Guid.NewGuid());

        clan.Join(player, capacity: 10, Now);

        Assert.Contains(new ClanMembershipChanged(player, clan.Id, Now), clan.DomainEvents);
    }

    [Fact]
    public void Leave_ShouldRaiseMembershipChanged()
    {
        var player = Guid.NewGuid();
        var clan = ClanOf(Guid.NewGuid());
        clan.Join(player, capacity: 10, Now);
        clan.ClearDomainEvents();

        clan.Leave(player, successorId: null, Now);

        Assert.Contains(new ClanMembershipChanged(player, clan.Id, Now), clan.DomainEvents);
    }

    [Fact]
    public void Kick_ShouldRaiseMembershipChanged_ForTheTarget()
    {
        var leader = Guid.NewGuid();
        var player = Guid.NewGuid();
        var clan = ClanOf(leader);
        clan.Join(player, capacity: 10, Now);
        clan.ClearDomainEvents();

        clan.Kick(leader, player, Now);

        Assert.Contains(new ClanMembershipChanged(player, clan.Id, Now), clan.DomainEvents);
    }

    [Fact]
    public void Allocate_ShouldRaiseOnce_OnlyWhenALevelCloses()
    {
        var config = new ClanTechnologiesConfig
        {
            BasePoints = 100,
            Tiers = [new TechnologyTierConfig { Tier = 1 }],
            Nodes = [new TechnologyNodeConfig { Key = "battle", Tab = "t", Row = 1, Levels = 5 }]
        };
        var technologies = new ClanTechnologies(config);
        var key = new TechnologyKey(1, "battle");
        var perLevel = technologies.Points(1, technologies.Tree.Node("battle")!, 1);
        var research = new ClanResearch(Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        research.AddPoints(perLevel * 10, Now);

        research.Allocate(key, perLevel - 1, technologies, Now);
        Assert.Empty(research.DomainEvents);

        research.Allocate(key, perLevel * 3, technologies, Now);
        Assert.IsType<ClanTechnologyLevelsCompleted>(Assert.Single(research.DomainEvents));
    }
}
