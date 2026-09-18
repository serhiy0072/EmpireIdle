using EmpireIdle.Application.Dev.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Dev;

public class SeedDevAccountCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly GameCatalog _catalog = new GameConfigBuilder().WithHeroes().WithEquipment().BuildCatalog();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Dictionary<string, IRewardGranter> _granters = new();

    private readonly List<RewardContext> _granted = [];

    private SeedDevAccountCommandHandler Handler()
    {
        foreach (var type in new[] { "Gems", "Resource", "Hero", "Item", "Equipment" })
        {
            var granter = Substitute.For<IRewardGranter>();
            granter.RewardType.Returns(type);
            granter
                .GrantAsync(Arg.Do<RewardContext>(context => _granted.Add(context)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            _granters[type] = granter;
        }

        return new SeedDevAccountCommandHandler(
            _catalog,
            new RewardDispatcher(_granters.Values),
            _unitOfWork,
            new FakeTimeProvider(Now),
            NullLogger<SeedDevAccountCommandHandler>.Instance);
    }

    private async Task<List<RewardContext>> SeedAsync()
    {
        await Handler().Handle(new SeedDevAccountCommand(Guid.NewGuid()), CancellationToken.None);
        return _granted;
    }

    [Fact]
    public async Task Handle_ShouldGrantGemsOnce()
    {
        var granted = await SeedAsync();

        var gems = granted.Where(context => context.Reward.Type == "Gems").ToList();

        Assert.Single(gems);
        Assert.Equal(5_000, gems[0].Reward.Amount);
    }

    [Fact]
    public async Task Handle_ShouldGrantEveryPieceOfEquipment()
    {
        var granted = await SeedAsync();

        var expected = _catalog.Config.Items.Count(item => item.Slot is not null);
        var actual = granted.Count(context => context.Reward.Type == "Equipment");

        Assert.Equal(expected, actual);
    }

    /// <summary>Кожен вищий ранг двічі: перша видача — герой, друга приходить уламками.</summary>
    [Fact]
    public async Task Handle_ShouldGrantEachHeroTwice()
    {
        var granted = await SeedAsync();

        var heroes = granted.Where(context => context.Reward.Type == "Hero").ToList();

        Assert.NotEmpty(heroes);
        Assert.All(heroes.GroupBy(context => context.Reward.Key), group => Assert.Equal(2, group.Count()));
    }

    /// <summary>Звичайні герої в сід не потрапляють: вони купуються за золото в залі.</summary>
    [Fact]
    public async Task Handle_ShouldSkipCommonHeroes()
    {
        var granted = await SeedAsync();

        var common = _catalog.Config.Heroes
            .Where(hero => hero.Rank == Rarity.Common)
            .Select(hero => hero.Key)
            .ToHashSet();

        Assert.DoesNotContain(granted, context => context.Reward.Type == "Hero" && common.Contains(context.Reward.Key!));
    }

    [Fact]
    public async Task Handle_ShouldSaveOnce()
    {
        await SeedAsync();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
