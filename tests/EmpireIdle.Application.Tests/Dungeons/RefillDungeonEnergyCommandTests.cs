using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Dungeons;

/// <summary>
/// Поповнення енергії данжів за gems (рішення 12.10.2026): до стелі, ціна росте з кожним
/// поповненням доби, повна шкала й вичерпаний ліміт gems не коштують.
/// </summary>
public class RefillDungeonEnergyCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IDungeonRepository _dungeons = Substitute.For<IDungeonRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PlayerWallet _wallet = new(Guid.NewGuid(), "user-1");
    private readonly GameConfig _config = new GameConfigBuilder().WithDungeons().Build();

    public RefillDungeonEnergyCommandTests()
    {
        _config.Dungeons.RefillPricesGems = [100, 300];

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(new Player(PlayerId, "tester", "tester@example.com", "user-1", Now));
        _wallets.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_wallet);
        _wallet.AddGems(new GemAmount(1_000), "test", Now);
    }

    private DungeonEnergy GivenEnergy(int amount)
    {
        var energy = new DungeonEnergy(Guid.NewGuid(), PlayerId, amount, Now);
        _dungeons.GetEnergyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(energy);

        return energy;
    }

    private Task Refill() => new RefillDungeonEnergyCommandHandler(_dungeons, _players, _wallets, _unitOfWork,
            new FakeTimeProvider(Now), new GameCatalog(_config), NullLogger<RefillDungeonEnergyCommandHandler>.Instance)
        .Handle(new RefillDungeonEnergyCommand(PlayerId), CancellationToken.None);

    /// <summary>Перше поповнення доби — перша ціна, шкала вщерть.</summary>
    [Fact]
    public async Task Handle_ShouldFillTheScale_ForTheFirstPrice()
    {
        var energy = GivenEnergy(10);

        await Refill();

        Assert.Equal(_config.Dungeons.MaxEnergy, energy.Current(_config.Dungeons.MaxEnergy, _config.Dungeons.RegenHours, Now));
        Assert.Equal(900, _wallet.GemBalance.Value);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Друге поповнення тієї ж доби коштує другу ціну.</summary>
    [Fact]
    public async Task Handle_ShouldChargeTheNextPrice_ForTheNextRefill()
    {
        var energy = GivenEnergy(10);
        await Refill();
        energy.Spend(_config.Dungeons.MaxEnergy, _config.Dungeons.MaxEnergy, _config.Dungeons.RegenHours, Now);

        await Refill();

        Assert.Equal(1_000 - 100 - 300, _wallet.GemBalance.Value);
    }

    /// <summary>Повна шкала — відмова, і gems не списано.</summary>
    [Fact]
    public async Task Handle_ShouldRefuseWithoutCharging_WhenTheScaleIsFull()
    {
        GivenEnergy(_config.Dungeons.MaxEnergy);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(Refill);

        Assert.Equal(RefusalReasons.DungeonEnergyFull.Key, refusal.Reason);
        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }

    /// <summary>Хто ще не заходив у данж, має повну шкалу — та сама відмова.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhenThePlayerHasNoEnergyYet()
    {
        var refusal = await Assert.ThrowsAsync<InvalidStateException>(Refill);

        Assert.Equal(RefusalReasons.DungeonEnergyFull.Key, refusal.Reason);
    }

    /// <summary>Ліміт доби вичерпано — відмова, і gems не списано.</summary>
    [Fact]
    public async Task Handle_ShouldRefuseWithoutCharging_OverTheDailyLimit()
    {
        var energy = GivenEnergy(0);
        _config.Dungeons.RefillPricesGems = [100];
        await Refill();
        energy.Spend(_config.Dungeons.MaxEnergy, _config.Dungeons.MaxEnergy, _config.Dungeons.RegenHours, Now);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(Refill);

        Assert.Equal(RefusalReasons.DungeonRefillLimit.Key, refusal.Reason);
        Assert.Equal(900, _wallet.GemBalance.Value);
    }

    /// <summary>Бракує gems — відмова з цифрами, шкала не змінилась.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhenGemsAreShort()
    {
        var energy = GivenEnergy(10);
        _config.Dungeons.RefillPricesGems = [5_000];

        await Assert.ThrowsAsync<NotEnoughResourcesException>(Refill);

        Assert.Equal(10, energy.Current(_config.Dungeons.MaxEnergy, _config.Dungeons.RegenHours, Now));
    }
}
