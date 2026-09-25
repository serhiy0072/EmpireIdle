using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Garrisons;

public class SpeedUpLevelUpCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string UserId = "user-1";

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static MonetizationConfig Monetization() => new()
    {
        SpeedUpFloorSeconds = 60,
        SpeedUpFactor = 1.2,
        SpeedUpExponent = 0.75
    };

    private static SpeedUpCalculator Calculator() => new(Monetization());

    private SpeedUpLevelUpCommandHandler Handler() => new(
        _villages, _garrisons, _wallets, _currentPlayer, _unitOfWork,
        new FakeTimeProvider(Now), Calculator(),
        NullLogger<SpeedUpLevelUpCommandHandler>.Instance);

    /// <summary>Гарнізон із активною прокачкою партії, поставленою напряму через LevelUpUnits.</summary>
    private (Garrison Garrison, PlayerWallet Wallet, Guid OrderId) GivenLevellingUp(
        int minutesLeft = 120, int gems = 5000)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        garrison.TrainUnits("infantry", 1, 5, 100, 1000, TimeSpan.Zero, Now);
        garrison.CompleteDueTraining(Now);
        garrison.LevelUpUnits("infantry", 1, 2, 5, 10, TimeSpan.FromMinutes(minutesLeft), Now);

        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(gems), "seed", PlayerId, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);
        _currentPlayer.UserId.Returns(UserId);

        return (garrison, wallet, garrison.LevelUpOrders.Single().Id);
    }

    /// <summary>Прискорення лишає останню хвилину: прокачка ще йде, завершить її сканер.</summary>
    [Fact]
    public async Task Handle_ShouldLeaveTheFloor_AndKeepTheOrderQueued()
    {
        var (garrison, _, orderId) = GivenLevellingUp(minutesLeft: 120);

        await Handler().Handle(new SpeedUpLevelUpCommand(PlayerId, orderId), CancellationToken.None);

        Assert.Equal(Now.AddSeconds(60), garrison.LevelUpOrders.Single().CompletesAt);
        Assert.DoesNotContain(garrison.Units, u => u.Level == 2);
    }

    /// <summary>Безкоштовного фінішу немає: дві хвилини теж коштують gems.</summary>
    [Fact]
    public async Task Handle_ShouldCharge_EvenForAShortTimer()
    {
        var (_, wallet, orderId) = GivenLevellingUp(minutesLeft: 2, gems: 5000);

        await Handler().Handle(new SpeedUpLevelUpCommand(PlayerId, orderId), CancellationToken.None);

        Assert.True(wallet.GemBalance.Value < 5000);
    }

    /// <summary>Довга черга списує gems за кривою прискорення.</summary>
    [Fact]
    public async Task Handle_ShouldChargeGems_AboveTheThreshold()
    {
        var (_, wallet, orderId) = GivenLevellingUp(minutesLeft: 120, gems: 5000);

        var expected = Calculator().GetCost(Now.AddMinutes(120), Now);

        await Handler().Handle(new SpeedUpLevelUpCommand(PlayerId, orderId), CancellationToken.None);

        Assert.True(expected > 0, "120 хвилин мають коштувати gems, інакше тест нічого не перевіряє.");
        Assert.Equal(5000 - expected, wallet.GemBalance.Value);
    }

    /// <summary>Чуже або неіснуюче замовлення — 404.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownOrder()
    {
        GivenLevellingUp();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new SpeedUpLevelUpCommand(PlayerId, Guid.NewGuid()), CancellationToken.None));
    }

    /// <summary>Нестача gems лишає замовлення в черзі.</summary>
    [Fact]
    public async Task Handle_ShouldNotComplete_WhenGemsAreInsufficient()
    {
        var (garrison, _, orderId) = GivenLevellingUp(minutesLeft: 1440, gems: 1);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler().Handle(new SpeedUpLevelUpCommand(PlayerId, orderId), CancellationToken.None));

        Assert.Single(garrison.LevelUpOrders);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Gems глобальні для акаунта — без автентифікації платити нема з чого.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WithoutAnAuthenticatedAccount()
    {
        var (_, _, orderId) = GivenLevellingUp(minutesLeft: 120);
        _currentPlayer.UserId.Returns((string?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Handler().Handle(new SpeedUpLevelUpCommand(PlayerId, orderId), CancellationToken.None));
    }
}
