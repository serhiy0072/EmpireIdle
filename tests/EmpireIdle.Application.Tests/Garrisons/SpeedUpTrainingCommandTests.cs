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

public class SpeedUpTrainingCommandTests
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
        InstantFinishThresholdMinutes = 5,
        SpeedUpFactor = 1.2,
        SpeedUpExponent = 0.75
    };

    private static SpeedUpCalculator Calculator() => new(Monetization());

    private SpeedUpTrainingCommandHandler Handler() => new(
        _villages, _garrisons, _wallets, _currentPlayer, _unitOfWork,
        new FakeTimeProvider(Now), Calculator(),
        NullLogger<SpeedUpTrainingCommandHandler>.Instance);

    /// <summary>
    /// Гарнізон із активним замовленням. Ставимо його напряму через TrainUnits
    /// із просторими лімітами: тест про ціну прискорення, не про гейти.
    /// </summary>
    private (Garrison Garrison, PlayerWallet Wallet, Guid OrderId) GivenTraining(
        int minutesLeft = 120, int gems = 5000)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        garrison.TrainUnits("infantry", count: 5, maxBatchSize: 100, armyCapacity: 1000,
            TimeSpan.FromMinutes(minutesLeft), Now);

        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(gems), "seed", PlayerId, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);
        _currentPlayer.UserId.Returns(UserId);

        return (garrison, wallet, garrison.TrainingOrders.Single().Id);
    }

    /// <summary>Прискорення завершує тренування одразу, не чекаючи сканера.</summary>
    [Fact]
    public async Task Handle_ShouldCompleteTrainingImmediately()
    {
        var (garrison, _, orderId) = GivenTraining(minutesLeft: 120);

        await Handler().Handle(new SpeedUpTrainingCommand(PlayerId, orderId), CancellationToken.None);

        Assert.Empty(garrison.TrainingOrders);
        Assert.Equal(5, garrison.Units.Sum(u => u.Count));
    }

    /// <summary>Довга черга списує gems за кривою.</summary>
    [Fact]
    public async Task Handle_ShouldChargeGems_AboveTheThreshold()
    {
        var (_, wallet, orderId) = GivenTraining(minutesLeft: 120, gems: 5000);

        var expected = Calculator().GetInstantFinishCost(Now.AddMinutes(120), Now);

        await Handler().Handle(new SpeedUpTrainingCommand(PlayerId, orderId), CancellationToken.None);

        Assert.True(expected > 0, "120 хвилин мають коштувати gems, інакше тест нічого не перевіряє.");
        Assert.Equal(5000 - expected, wallet.GemBalance.Value);
    }

    /// <summary>
    /// Коротка черга безкоштовна: гравець не має платити за хвилину,
    /// і без порога ціна прискорення була б абсурдною для дрібниць.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldChargeNothing_BelowTheFreeThreshold()
    {
        var (_, wallet, orderId) = GivenTraining(minutesLeft: 3, gems: 5000);

        await Handler().Handle(new SpeedUpTrainingCommand(PlayerId, orderId), CancellationToken.None);

        Assert.Equal(5000, wallet.GemBalance.Value);
    }

    /// <summary>Чуже або неіснуюче замовлення — 404.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownOrder()
    {
        GivenTraining();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new SpeedUpTrainingCommand(PlayerId, Guid.NewGuid()), CancellationToken.None));
    }

    /// <summary>Нестача gems лишає замовлення в черзі.</summary>
    [Fact]
    public async Task Handle_ShouldNotComplete_WhenGemsAreInsufficient()
    {
        var (garrison, _, orderId) = GivenTraining(minutesLeft: 1440, gems: 1);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler().Handle(new SpeedUpTrainingCommand(PlayerId, orderId), CancellationToken.None));

        Assert.Single(garrison.TrainingOrders);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Gems глобальні для акаунта — без автентифікації платити нема з чого.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WithoutAnAuthenticatedAccount()
    {
        var (_, _, orderId) = GivenTraining(minutesLeft: 120);
        _currentPlayer.UserId.Returns((string?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Handler().Handle(new SpeedUpTrainingCommand(PlayerId, orderId), CancellationToken.None));
    }
}
