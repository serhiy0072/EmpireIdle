using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Mail.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Application.Rewards.Granters;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Rewards;

public class ResourceRewardGranterTests
{
    private const int StartingFood = 1000;
    private const int RewardFood = 500;

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly Village _village = Entities.VillageWithResources(StartingFood);
    private readonly ResourceRewardGranter _granter;

    public ResourceRewardGranterTests()
    {
        // Склад у конфігу є, у селі — ні: місткість нульова, будь-яка нагорода «не влазить»
        var catalog = new GameConfigBuilder().WithBuildings(TestKeys.Warehouse).BuildCatalog();

        _villages.GetByPlayerIdAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_village);

        _granter = new ResourceRewardGranter(_villages, catalog, new FakeTimeProvider(Entities.Now),
            new VillageCapacities(catalog), NullLogger<ResourceRewardGranter>.Instance);
    }

    private RewardContext Context(bool ignoreStorageCap) => new(
        _village.PlayerId,
        new RewardConfig { Type = "Resource", Key = TestKeys.Food, Amount = RewardFood },
        "test",
        Entities.Now,
        ignoreStorageCap);

    private int Food => _village.Resources.Single(r => r.ResourceType == TestKeys.Food).Amount;

    [Fact]
    public async Task GrantAsync_ShouldCapAtStorage_ByDefault()
    {
        await _granter.GrantAsync(Context(ignoreStorageCap: false), CancellationToken.None);

        Assert.Equal(StartingFood, Food);
    }

    [Fact]
    public async Task GrantAsync_ShouldGrantInFull_WhenStorageCapIsIgnored()
    {
        await _granter.GrantAsync(Context(ignoreStorageCap: true), CancellationToken.None);

        Assert.Equal(StartingFood + RewardFood, Food);
    }

    [Fact]
    public async Task MailClaim_ShouldIgnoreStorageCap()
    {
        var resources = Substitute.For<IRewardGranter>();
        resources.RewardType.Returns("Resource");

        var letter = MailLetter.WithRewards(Guid.NewGuid(), 1, _village.PlayerId, MailKind.DailyReward,
            [new MailReward("Resource", TestKeys.Food, RewardFood)], 1, Entities.Now, Entities.Now.AddDays(1));

        await new MailRewardClaimer(new RewardDispatcher([resources]))
            .ClaimAsync(letter, Entities.Now, CancellationToken.None);

        await resources.Received(1).GrantAsync(
            Arg.Is<RewardContext>(c => c.IgnoreStorageCap && c.Reward.Amount == RewardFood),
            Arg.Any<CancellationToken>());
    }
}
