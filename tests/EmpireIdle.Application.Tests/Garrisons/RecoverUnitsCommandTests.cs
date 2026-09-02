using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Garrisons;

/// <summary>
/// Викуп відновлюваних за gems. Ключове — порядок: юніти забираються зі стеків
/// ДО розрахунку ціни, бо частина могла згоріти по дедлайну між показом екрана
/// і натисканням кнопки. Платити за неї гравець не має.
/// </summary>
public class RecoverUnitsCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string UserId = "user-1";

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Units =
        [
            new UnitConfig
            {
                Key = "infantry",
                RecoverCostGems = 2,
                Cost = [new ResourceCost { Resource = "food", Amount = 40 }]
            }
        ],
        Monetization = new MonetizationConfig { HealGemsPerUnit = 3 }
    };

    private RecoverUnitsCommandHandler Handler() => new(
        _villages, _garrisons, _wallets, _currentPlayer, _unitOfWork,
        new FakeTimeProvider(Now), new GameCatalog(Config()),
        NullLogger<RecoverUnitsCommandHandler>.Instance);

    /// <summary>Гарнізон із відновлюваними, чий дедлайн ще не минув.</summary>
    private (Garrison Garrison, PlayerWallet Wallet) GivenState(
        int recoverable = 10, int gems = 1000, int expiresInHours = 24)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        if (recoverable > 0)
        {
            garrison.AddRecoverable(
                new Dictionary<string, int> { ["infantry"] = recoverable },
                Guid.NewGuid(), Now.AddHours(expiresInHours), Now);
        }

        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(gems), "seed", PlayerId, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);
        _currentPlayer.UserId.Returns(UserId);

        return (garrison, wallet);
    }

    private static RecoverUnitsCommand Recover(int count) =>
        new(PlayerId, new Dictionary<string, int> { ["infantry"] = count });

    /// <summary>Викуплені юніти повертаються в гарнізон.</summary>
    [Fact]
    public async Task Handle_ShouldReturnUnitsToTheGarrison()
    {
        var (garrison, _) = GivenState(recoverable: 10);

        await Handler().Handle(Recover(5), CancellationToken.None);

        Assert.Equal(5, garrison.Units.Sum(u => u.Count));
        Assert.Equal(5, garrison.RecoverableCount(Now));
    }

    /// <summary>
    /// Платимо лише за те, що реально повернулось. Запит на десять при трьох
    /// доступних коштує як три — інакше гравець платив би за згорілих.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldChargeOnlyForUnitsActuallyRecovered()
    {
        var (_, wallet) = GivenState(recoverable: 3, gems: 1000);

        await Handler().Handle(Recover(10), CancellationToken.None);

        // 3 юніти × 2 gems = 6, не 10 × 2 = 20
        Assert.Equal(994, wallet.GemBalance.Value);
    }

    /// <summary>Прострочені стеки не викупляються.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenEverythingHasExpired()
    {
        GivenState(recoverable: 10, expiresInHours: -1);

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(Recover(5), CancellationToken.None));
    }

    /// <summary>Порожній кошик — помилка запиту, а не мовчазний успіх.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenThereIsNothingToRecover()
    {
        GivenState(recoverable: 0);

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(Recover(5), CancellationToken.None));
    }

    /// <summary>
    /// Нестача gems зупиняє операцію до збереження — стеки лишаються на місці,
    /// бо транзакція не дійшла до SaveChanges.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotSave_WhenGemsAreInsufficient()
    {
        GivenState(recoverable: 100, gems: 5);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler().Handle(Recover(100), CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Gems глобальні для акаунта — без автентифікації платити нема з чого.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WithoutAnAuthenticatedAccount()
    {
        GivenState();
        _currentPlayer.UserId.Returns((string?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Handler().Handle(Recover(5), CancellationToken.None));
    }
}
