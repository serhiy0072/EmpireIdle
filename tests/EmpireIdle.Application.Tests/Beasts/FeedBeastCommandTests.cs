using EmpireIdle.Application.Beasts.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Beasts;

/// <summary>Годування (GDD §5.10): корм веде рівень до стелі рангу, зайвий лишається в інвентарі.</summary>
public class FeedBeastCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IBeastPenRepository _pens = Substitute.For<IBeastPenRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GameCatalog _catalog = new GameConfigBuilder().WithBeasts().BuildCatalog();

    private FeedBeastCommandHandler Handler() => new(_pens, _inventory, _unitOfWork, new BeastProgression(_catalog),
        _catalog, new FakeTimeProvider(Now), NullLogger<FeedBeastCommandHandler>.Instance);

    private BeastPen GivenPenWithWolf()
    {
        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);

        _pens.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);
        return pen;
    }

    private PlayerItem GivenFeed(int count)
    {
        var feed = new PlayerItem(Guid.NewGuid(), PlayerId, TestKeys.BeastFeed, count);

        _inventory.GetItemAsync(PlayerId, TestKeys.BeastFeed, Arg.Any<CancellationToken>()).Returns(feed);
        return feed;
    }

    /// <summary>100 корму — рівень 2, ще 25 — досвід усередині рівня (на 3-й треба 125).</summary>
    [Fact]
    public async Task Handle_ShouldRaiseTheLevel_AndKeepTheRemainderAsExperience()
    {
        var pen = GivenPenWithWolf();
        var feed = GivenFeed(500);

        var result = await Handler().Handle(new FeedBeastCommand(PlayerId, TestKeys.Beast, 125), CancellationToken.None);

        var wolf = pen.Beasts.Single();
        Assert.Equal((2, 25), (wolf.Level, wolf.Experience));
        Assert.Equal(125, result.Eaten);
        Assert.Equal(375, feed.Count);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Понад стелю рангу корм не з'їдається: гравець не втрачає його даремно.</summary>
    [Fact]
    public async Task Handle_ShouldStopAtTheRankCap_AndLeaveTheRestInTheInventory()
    {
        var pen = GivenPenWithWolf();
        var feed = GivenFeed(100_000);

        var result = await Handler().Handle(new FeedBeastCommand(PlayerId, TestKeys.Beast, 100_000), CancellationToken.None);

        var wolf = pen.Beasts.Single();
        Assert.Equal(10, wolf.Level);
        Assert.Equal(100_000 - result.Eaten, feed.Count);
        Assert.True(result.Eaten < 100_000);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheBeastIsAtItsCap()
    {
        GivenPenWithWolf();
        GivenFeed(100_000);
        await Handler().Handle(new FeedBeastCommand(PlayerId, TestKeys.Beast, 100_000), CancellationToken.None);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new FeedBeastCommand(PlayerId, TestKeys.Beast, 1), CancellationToken.None));

        Assert.Equal(RefusalReasons.BeastLevelCapped.Key, refusal.Reason);
        Assert.Equal(10, refusal.Args["level"]);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WithoutEnoughFeed()
    {
        GivenPenWithWolf();
        GivenFeed(10);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new FeedBeastCommand(PlayerId, TestKeys.Beast, 50), CancellationToken.None));

        Assert.Equal(RefusalReasons.BeastNotEnoughFeed.Key, refusal.Reason);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Звіра, якого гравець не приручив, не нагодувати — це 404, а не тихий успіх.</summary>
    [Fact]
    public async Task Handle_ShouldThrowNotFound_ForAnUntamedBeast()
    {
        GivenPenWithWolf();
        GivenFeed(10);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new FeedBeastCommand(PlayerId, "griffin", 5), CancellationToken.None));
    }
}
