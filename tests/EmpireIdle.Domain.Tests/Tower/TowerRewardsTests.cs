using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Tower;

namespace EmpireIdle.Domain.Tests.Tower;

/// <summary>
/// Нагорода поверху: золото 200 + 10 × поверх і найвища віха, що ділить номер. Звичайний — 5 gems,
/// кожен 5-й — 20 gems, кожен 10-й — 50 gems і чергування уламка зі скринею, з 30-го — рідкісний уламок.
/// </summary>
public class TowerRewardsTests
{
    private static RewardConfig Gems(int amount) => new() { Type = "Gems", Amount = amount };

    private static RewardConfig Item(string key) => new() { Type = "Item", Key = key, Amount = 1 };

    private static readonly TowerRewards Rewards = new(new TowerConfig
    {
        GoldResource = "gold",
        GoldBase = 200,
        GoldPerFloor = 10,
        Milestones =
        [
            new TowerMilestoneConfig { Every = 1, Stages = [new TowerRewardStageConfig { Options = [[Gems(5)]] }] },
            new TowerMilestoneConfig { Every = 5, Stages = [new TowerRewardStageConfig { Options = [[Gems(20)]] }] },
            new TowerMilestoneConfig
            {
                Every = 10,
                Stages =
                [
                    new TowerRewardStageConfig { FromFloor = 1, Options = [[Gems(50), Item("shard")], [Gems(50), Item("chest")]] },
                    new TowerRewardStageConfig { FromFloor = 30, Options = [[Gems(50), Item("rare_shard")], [Gems(50), Item("chest")]] }
                ]
            }
        ]
    });

    private static List<string> Describe(int floor)
        => Rewards.For(floor).Select(r => $"{r.Type}:{r.Key}:{r.Amount}").ToList();

    [Fact]
    public void For_ShouldPayGoldAndTheRegularGems_OnAPlainFloor()
        => Assert.Equal(["Resource:gold:270", "Gems::5"], Describe(7));

    /// <summary>Вища віха замінює нижчу: на 10-му немає ні 5, ні 20 gems.</summary>
    [Fact]
    public void For_ShouldReplaceLowerMilestones_WithTheHighest()
    {
        Assert.Equal(["Resource:gold:250", "Gems::20"], Describe(5));
        Assert.Equal(["Resource:gold:300", "Gems::50", "Item:shard:1"], Describe(10));
    }

    /// <summary>Варіанти чергуються від віхи до віхи, а ступінь змінюється з поверхом.</summary>
    [Fact]
    public void For_ShouldAlternateOptions_AndFollowTheStage()
    {
        Assert.Contains("Item:chest:1", Describe(20));
        Assert.Contains("Item:rare_shard:1", Describe(30));
        Assert.Contains("Item:chest:1", Describe(40));
    }
}
