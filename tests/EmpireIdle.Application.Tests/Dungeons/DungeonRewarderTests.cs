using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Dungeons;

/// <summary>
/// Нагорода за забіг — рівно та, яку обіцяла вітрина. Вітрина й видача
/// беруть множник з одного BattleBuilder: дві копії формули вже розходились
/// у запасному значенні.
/// </summary>
public class DungeonRewarderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Grant_ShouldPayTheResourcesTheShowcasePromised(int level)
    {
        var config = new GameConfigBuilder().WithDungeons().Build();
        var catalog = new GameCatalog(config);
        var builder = new BattleBuilder(config.Dungeons);
        var village = Entities.VillageWithResources(0);

        var dungeons = Substitute.For<IDungeonRepository>();
        dungeons.GetClearedLevelsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>());

        var villages = Substitute.For<IVillageRepository>();
        villages.GetByPlayerIdAsync(village.PlayerId, Arg.Any<CancellationToken>()).Returns(village);

        var granter = new ItemGranter(Substitute.For<IInventoryRepository>(), Substitute.For<IServerContext>(),
            Substitute.For<IRandomSource>(), new ArtifactRoller(config.Equipment));

        var rewarder = new DungeonRewarder(dungeons, villages, granter, builder, catalog, Substitute.For<IRandomSource>());
        var run = new DungeonRun(Guid.NewGuid(), village.PlayerId, 1, TestKeys.Dungeon, level, "{}", Entities.Now);

        var reward = await rewarder.GrantAsync(run, Entities.Now, CancellationToken.None);

        var line = config.Dungeons.Dungeons.Single().Reward.Single();
        var promised = (int)Math.Round(line.Amount * builder.RewardMultiplier(level));

        Assert.Equal(promised, reward.Resources.Single().Amount);
        Assert.Equal(promised, village.Resources.Single(r => r.ResourceType == line.Resource).Amount);
    }
}
