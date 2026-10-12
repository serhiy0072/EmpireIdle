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
    private const long ExperiencePerRun = 1000;

    private readonly GameConfig _config;
    private readonly BattleBuilder _builder;
    private readonly Village _village = Entities.VillageWithResources(0);
    private readonly HeroExperiencePool _pool;
    private readonly DungeonRewarder _rewarder;

    public DungeonRewarderTests()
    {
        _config = new GameConfigBuilder().WithDungeons().Build();
        _config.Dungeons.HeroExperiencePerRun = ExperiencePerRun;
        _builder = new BattleBuilder(_config.Dungeons);
        _pool = new HeroExperiencePool(Guid.NewGuid(), _village.PlayerId, 1);

        var dungeons = Substitute.For<IDungeonRepository>();
        dungeons.GetClearedLevelsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>());

        var villages = Substitute.For<IVillageRepository>();
        villages.GetByPlayerIdAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_village);

        var heroes = Substitute.For<IHeroRepository>();
        heroes.GetExperienceAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_pool);

        var granter = new ItemGranter(Substitute.For<IInventoryRepository>(), Substitute.For<IServerContext>());

        _rewarder = new DungeonRewarder(dungeons, villages, heroes, Substitute.For<IServerContext>(), granter, _builder,
            new GameCatalog(_config), Substitute.For<IRandomSource>());
    }

    private Task<Application.Dungeons.Commands.DungeonReward> Grant(int level)
        => _rewarder.GrantAsync(new DungeonRun(Guid.NewGuid(), _village.PlayerId, 1, TestKeys.Dungeon, level, "{}", Entities.Now),
            Entities.Now, CancellationToken.None);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Grant_ShouldPayTheResourcesTheShowcasePromised(int level)
    {
        var reward = await Grant(level);

        var line = _config.Dungeons.Dungeons.Single().Reward.Single();
        var promised = (int)Math.Round(line.Amount * _builder.RewardMultiplier(level));

        Assert.Equal(promised, reward.Resources.Single().Amount);
        Assert.Equal(promised, _village.Resources.Single(r => r.ResourceType == line.Resource).Amount);
    }

    /// <summary>Досвід героїв росте з рівнем забігу тим самим множником, що й ресурси, і лягає в спільний запас.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Grant_ShouldAddHeroExperienceToThePool(int level)
    {
        var reward = await Grant(level);

        var expected = (long)Math.Round(ExperiencePerRun * _builder.RewardMultiplier(level));

        Assert.Equal(expected, reward.HeroExperience);
        Assert.Equal(expected, _pool.Amount);
    }
}
