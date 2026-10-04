using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Mail.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Application.Rewards.Granters;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
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
        // Склад у конфігу є, у селі — ні: раніше це означало нульову місткість
        var catalog = new GameConfigBuilder().WithBuildings(TestKeys.Warehouse).BuildCatalog();

        _villages.GetByPlayerIdAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_village);

        _granter = new ResourceRewardGranter(_villages, catalog, new FakeTimeProvider(Entities.Now));
    }

    private RewardContext Context() => new(
        _village.PlayerId,
        new RewardConfig { Type = "Resource", Key = TestKeys.Food, Amount = RewardFood },
        "test",
        Entities.Now);

    private long Food => _village.Resources.Single(r => r.ResourceType == TestKeys.Food).Amount;

    /// <summary>Стелі складу немає (GDD §4.1): нагорода лягає повністю навіть без складу.</summary>
    [Fact]
    public async Task GrantAsync_ShouldGrantInFull_WithoutAnyStorage()
    {
        await _granter.GrantAsync(Context(), CancellationToken.None);

        Assert.Equal(StartingFood + RewardFood, Food);
    }

    /// <summary>Лист передає нагороду диспетчеру як є.</summary>
    [Fact]
    public async Task MailClaim_ShouldDispatchTheLetterRewards()
    {
        var resources = Substitute.For<IRewardGranter>();
        resources.RewardType.Returns("Resource");

        var letter = MailLetter.WithRewards(Guid.NewGuid(), 1, _village.PlayerId, MailKind.DailyReward,
            [new MailReward("Resource", TestKeys.Food, RewardFood)], 1, Entities.Now, Entities.Now.AddDays(1));

        await new MailRewardClaimer(new RewardDispatcher([resources]))
            .ClaimAsync(letter, Entities.Now, CancellationToken.None);

        await resources.Received(1).GrantAsync(
            Arg.Is<RewardContext>(c => c.Reward.Amount == RewardFood && c.Reference == $"mail:{letter.Id}"),
            Arg.Any<CancellationToken>());
    }
}
