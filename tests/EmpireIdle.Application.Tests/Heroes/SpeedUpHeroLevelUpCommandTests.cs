using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Прискорення прокачки героя: gems за кривою, строк на «зараз»,
/// завершення тим самим обробником, що й у сканера.
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
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static SpeedUpCalculator Calculator() => new(new MonetizationConfig
    {
        InstantFinishThresholdMinutes = 5,
        SpeedUpFactor = 2.0,
        SpeedUpExponent = 0.75
    });

    private SpeedUpHeroLevelUpCommandHandler Handler() => new(
        _heroes, _wallets, _currentPlayer, _unitOfWork, new FakeTimeProvider(Now), Calculator(), _mediator,
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

    /// <summary>Строк підтягується на «зараз» і замовлення завершується тим самим обробником, що й у сканера.</summary>
    [Fact]
    public async Task Handle_ShouldPullTheDeadlineToNow_AndComplete()
    {
        var (order, _) = GivenLevellingUp(minutesLeft: 120);

        await Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, order.Id), CancellationToken.None);

        Assert.Equal(Now, order.CompletesAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Is<CompleteHeroLevelUpCommand>(c => c.OrderId == order.Id), Arg.Any<CancellationToken>());
    }

    /// <summary>Довга черга списує gems за кривою прискорення.</summary>
    [Fact]
    public async Task Handle_ShouldChargeGems_AboveTheThreshold()
    {
        var (order, wallet) = GivenLevellingUp(minutesLeft: 120, gems: 5000);
        var expected = Calculator().GetInstantFinishCost(order.CompletesAt, Now);

        await Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, order.Id), CancellationToken.None);

        Assert.True(expected > 0, "120 хвилин мають коштувати gems, інакше тест нічого не перевіряє.");
        Assert.Equal(5000 - expected, wallet.GemBalance.Value);
    }

    /// <summary>Коротка черга безкоштовна: гаманець не чіпаємо взагалі.</summary>
    [Fact]
    public async Task Handle_ShouldNotTouchTheWallet_BelowTheThreshold()
    {
        var (order, wallet) = GivenLevellingUp(minutesLeft: 2, gems: 10);

        await Handler().Handle(new SpeedUpHeroLevelUpCommand(PlayerId, order.Id), CancellationToken.None);

        Assert.Equal(10, wallet.GemBalance.Value);
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
        await _mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<CompleteHeroLevelUpCommand>(), default);
    }
}
