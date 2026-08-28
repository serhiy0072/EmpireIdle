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

public class HealWoundedCommandTests
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
                Cost = [new ResourceCost { Resource = "food", Amount = 40 }]
            }
        ],
        Monetization = new MonetizationConfig { HealGemsPerUnit = 3 }
    };

    private HealWoundedCommandHandler Handler() => new(
        _villages, _garrisons, _wallets, _currentPlayer, _unitOfWork,
        new FakeTimeProvider(Now), new GameCatalog(Config()),
        NullLogger<HealWoundedCommandHandler>.Instance);

    /// <summary>Село з ресурсами, гарнізон із пораненими, гаманець із балансом.</summary>
    private (Village Village, Garrison Garrison, PlayerWallet Wallet) GivenState(
        int woundedInfantry = 10, int gems = 100, int food = 1000)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = food }, Now);

        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);
        garrison.AdmitWounded(new Dictionary<string, int> { ["infantry"] = woundedInfantry }, Now);

        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(gems), "seed", PlayerId, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);
        _currentPlayer.UserId.Returns(UserId);

        return (village, garrison, wallet);
    }

    private static HealWoundedCommand Heal(int count, HealPaymentMethod payment) =>
        new(PlayerId, new Dictionary<string, int> { ["infantry"] = count }, payment);

    /// <summary>Оплата gems: фіксована ціна за юніта, помножена на кількість.</summary>
    [Fact]
    public async Task Handle_ShouldChargeGemsPerUnit()
    {
        var (_, _, wallet) = GivenState(gems: 100);

        await Handler().Handle(Heal(5, HealPaymentMethod.Gems), CancellationToken.None);

        // 5 юнітів × 3 gems = 15, лишається 85
        Assert.Equal(85, wallet.GemBalance.Value);
    }

    /// <summary>
    /// Оплата ресурсами коштує половину вартості нового юніта, округлену вгору:
    /// лікування має бути дешевшим за тренування, інакше воно безглузде.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldChargeHalfTheUnitCost_WhenPayingWithResources()
    {
        var (village, _, _) = GivenState(food: 1000);

        await Handler().Handle(Heal(5, HealPaymentMethod.Resources), CancellationToken.None);

        // 5 × 40 × 0.5 = 100, лишається 900
        Assert.Equal(900, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>Оплата ресурсами не чіпає гаманець.</summary>
    [Fact]
    public async Task Handle_ShouldNotTouchTheWallet_WhenPayingWithResources()
    {
        var (_, _, wallet) = GivenState(gems: 100);

        await Handler().Handle(Heal(5, HealPaymentMethod.Resources), CancellationToken.None);

        Assert.Equal(100, wallet.GemBalance.Value);
    }

    /// <summary>Порожній госпіталь — не мовчазний успіх, а помилка запиту.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenThereIsNothingToHeal()
    {
        GivenState(woundedInfantry: 0);

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(Heal(5, HealPaymentMethod.Gems), CancellationToken.None));
    }

    /// <summary>
    /// Gems глобальні для акаунта, тому без автентифікованого користувача
    /// платити нема з чого — це 403, а не 500.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenPayingWithGemsWithoutAnAccount()
    {
        GivenState();
        _currentPlayer.UserId.Returns((string?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Handler().Handle(Heal(5, HealPaymentMethod.Gems), CancellationToken.None));
    }

    /// <summary>Нестача gems зупиняє операцію — баланс не йде в мінус.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenGemsAreInsufficient()
    {
        GivenState(woundedInfantry: 10, gems: 5);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Handler().Handle(Heal(10, HealPaymentMethod.Gems), CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Не можна вилікувати більше, ніж лежить у госпіталі.</summary>
    [Fact]
    public async Task Handle_ShouldChargeOnlyForUnitsActuallyHealed()
    {
        var (_, _, wallet) = GivenState(woundedInfantry: 3, gems: 100);

        await Handler().Handle(Heal(10, HealPaymentMethod.Gems), CancellationToken.None);

        // Вилікувано 3, не 10: 3 × 3 = 9
        Assert.Equal(91, wallet.GemBalance.Value);
    }
}
