using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Tower;

namespace EmpireIdle.Domain.Tests.Tower;

/// <summary>
/// Нагорода поверху: основа кожного поверху (20 gems) і найвища з інших віх, що ділить номер. Кожен 5-й —
/// баночка, кожен 10-й — артефакт, що чергується зі скринею, з 30-го — рідкісний артефакт.
/// </summary>
public class TowerRewardsTests
{
    private static RewardConfig Gems(int amount) => new() { Type = "Gems", Amount = amount };

    private static RewardConfig Item(string key) => new() { Type = "Item", Key = key, Amount = 1 };

    private static RewardConfig Artifact(string rarity) => new() { Type = "Artifact", Key = rarity, Amount = 1 };

    private static readonly TowerRewards Rewards = new(new TowerConfig
    {
        Milestones =
        [
            new TowerMilestoneConfig { Every = 1, Stages = [new TowerRewardStageConfig { Options = [[Gems(20)]] }] },
            new TowerMilestoneConfig { Every = 5, Stages = [new TowerRewardStageConfig { Options = [[Item("jar")]] }] },
            new TowerMilestoneConfig
            {
                Every = 10,
                Stages =
                [
                    new TowerRewardStageConfig { FromFloor = 1, Options = [[Artifact("Common")], [Item("chest")]] },
                    new TowerRewardStageConfig { FromFloor = 30, Options = [[Artifact("Rare")], [Item("chest")]] }
                ]
            }
        ]
    });

    private static List<string> Describe(int floor)
        => Rewards.For(floor).Select(r => $"{r.Type}:{r.Key}:{r.Amount}").ToList();

    [Fact]
    public void For_ShouldPayTheBase_OnAPlainFloor()
        => Assert.Equal(["Gems::20"], Describe(7));

    /// <summary>Вища віха замінює нижчу, але основа поверху лишається: на 10-му — gems і артефакт, без баночки.</summary>
    [Fact]
    public void For_ShouldAddTheHighestMilestone_OnTopOfTheBase()
    {
        Assert.Equal(["Gems::20", "Item:jar:1"], Describe(5));
        Assert.Equal(["Gems::20", "Artifact:Common:1"], Describe(10));
    }

    /// <summary>Варіанти чергуються від віхи до віхи, а ступінь змінюється з поверхом.</summary>
    [Fact]
    public void For_ShouldAlternateOptions_AndFollowTheStage()
    {
        Assert.Contains("Item:chest:1", Describe(20));
        Assert.Contains("Artifact:Rare:1", Describe(30));
        Assert.Contains("Item:chest:1", Describe(40));
    }
}
