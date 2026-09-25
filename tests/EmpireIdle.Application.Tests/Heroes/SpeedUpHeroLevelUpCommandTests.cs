using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Прискорення прокачки героя: gems за кривою, строк до межі прискорення,
/// завершення — сканером.
/// </summary>
public class SpeedUpHeroLevelUpCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string UserId = "user-1";

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static SpeedUpCalculator Calculator() => new(new MonetizationConfig
    {
        SpeedUpFloorSeconds = 60,
        SpeedUpFactor = 2.0,
        SpeedUpExponent = 0.75
    });

    private SpeedUpHeroLevelUpCommandHandler Handler() => new(
        _heroes, _wallets, _currentPlayer, _unitOfWork, new FakeTimeProvider(Now), Calculator(),
        NullLogger<SpeedUpHeroLevelUpCommandHandler>.Instance);

    private (HeroLevelOrder Order, PlayerWallet Wallet) GivenLevellingUp(int minutesLeft = 120, int gems = 5000)
    {
        var order = new HeroLevelOrder(Guid.NewGuid(), Guid.NewGuid(), PlayerId, 1, targetLevel: 2, Now.AddMinutes(minutesLeft));
        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(gems), "seed", PlayerId, Now);

        _heroes.GetActiveOrderAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(order);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);
        _currentPlayer.UserId.Returns(UserId);

        return (order, wallet);
    }

    /// <summary>Строк підтягується до межі: останню хвилину треба дочекатись.</summary>
    [Fact]
    public async Task Handle_ShouldPullTheDeadlineToTheFloor()
    {
        var (order, _) = GivenLevellingUp(minutesLeft: 120);

        await Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, order.Id), CancellationToken.None);

        Assert.Equal(Now.AddSeconds(60), order.CompletesAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Довга черга списує gems за кривою прискорення.</summary>
    [Fact]
    public async Task Handle_ShouldChargeGems_AboveTheThreshold()
    {
        var (order, wallet) = GivenLevellingUp(minutesLeft: 120, gems: 5000);
        var expected = Calculator().GetCost(order.CompletesAt, Now);

        await Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, order.Id), CancellationToken.None);

        Assert.True(expected > 0, "120 хвилин мають коштувати gems, інакше тест нічого не перевіряє.");
        Assert.Equal(5000 - expected, wallet.GemBalance.Value);
    }

    /// <summary>Безкоштовного фінішу немає: дві хвилини теж коштують gems.</summary>
    [Fact]
    public async Task Handle_ShouldCharge_EvenForAShortTimer()
    {
        var (order, wallet) = GivenLevellingUp(minutesLeft: 2, gems: 10);

        await Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, order.Id), CancellationToken.None);

        Assert.True(wallet.GemBalance.Value < 10);
    }

    /// <summary>На межі — відмова з причиною, гаманець не чіпаємо.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_AtTheFloor()
    {
        var order = new HeroLevelOrder(Guid.NewGuid(), Guid.NewGuid(), PlayerId, 1, targetLevel: 2, Now.AddSeconds(60));
        _heroes.GetActiveOrderAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(order);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, order.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.SpeedUpAtFloor.Key, refusal.Reason);
        await _wallets.DidNotReceive().GetByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Чуже або неіснуюче замовлення — 404: шукаємо лише серед активних замовлень гравця.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownOrder()
    {
        GivenLevellingUp();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, Guid.NewGuid()), CancellationToken.None));
    }

    /// <summary>Нестача gems лишає замовлення як було.</summary>
    [Fact]
    public async Task Handle_ShouldNotReduce_WhenGemsAreInsufficient()
    {
        var (order, _) = GivenLevellingUp(minutesLeft: 1440, gems: 1);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, order.Id), CancellationToken.None));

        Assert.Equal(Now.AddMinutes(1440), order.CompletesAt);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
