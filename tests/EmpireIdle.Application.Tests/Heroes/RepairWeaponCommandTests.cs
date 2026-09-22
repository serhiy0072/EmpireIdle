using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>Ремонт зламаної зброї — за gems, у кузні.</summary>
public class RepairWeaponCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PlayerWallet _wallet = new(Guid.NewGuid(), "user-1");

    public RepairWeaponCommandTests()
    {
        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(new Player(PlayerId, "tester", "tester@example.com", "user-1", Now));
        _wallets.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_wallet);
    }

    private RepairWeaponCommandHandler Handler()
    {
        var config = HeroTestConfig.Create();

        return new RepairWeaponCommandHandler(
            _inventory, _villages, _players, _wallets, _unitOfWork, new FakeTimeProvider(Now),
            new EnhancementRules(config.Equipment), new GameCatalog(config),
            NullLogger<RepairWeaponCommandHandler>.Instance);
    }

    private EquipmentItem GivenBrokenSword(int level)
    {
        var sword = new EquipmentItem(Guid.NewGuid(), PlayerId, ServerId, "sword_iron", EquipmentSlot.Weapon,
            Rarity.Common, [("Attack", 10.0)], Now);

        for (var i = 0; i < level; i++)
            sword.Enhance(Now);

        sword.Break(Now);
        _inventory.GetEquipmentByIdAsync(sword.Id, Arg.Any<CancellationToken>()).Returns(sword);

        return sword;
    }

    private void GivenForge()
    {
        var config = HeroTestConfig.Create();
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["gold"], 0, 0);
        village.AddBuilding(HeroTestConfig.Forge, config.Buildings.ToDictionary(b => b.Key), Now);
        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
    }

    [Fact]
    public async Task Handle_ShouldChargeGemsByLevel_AndRepair()
    {
        GivenForge();
        _wallet.AddGems(new GemAmount(500), "seed", PlayerId, Now);
        var sword = GivenBrokenSword(level: 5);

        await Handler().Handle(new RepairWeaponCommand(PlayerId, sword.Id), CancellationToken.None);

        Assert.False(sword.IsBroken);
        Assert.Equal(500 - (20 + 8 * 5), _wallet.GemBalance.Value);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenGemsAreShort()
    {
        GivenForge();
        _wallet.AddGems(new GemAmount(10), "seed", PlayerId, Now);
        var sword = GivenBrokenSword(level: 5);

        var error = await Assert.ThrowsAsync<NotEnoughResourcesException>(
            () => Handler().Handle(new RepairWeaponCommand(PlayerId, sword.Id), CancellationToken.None));

        Assert.Equal("gems", error.Resource);
        Assert.True(sword.IsBroken);
        Assert.Equal(10, _wallet.GemBalance.Value);
    }

    /// <summary>Ціле не лагодять: команда ідемпотентна, gems не списуються.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheWeaponIsWhole()
    {
        _wallet.AddGems(new GemAmount(500), "seed", PlayerId, Now);
        var sword = new EquipmentItem(Guid.NewGuid(), PlayerId, ServerId, "sword_iron", EquipmentSlot.Weapon,
            Rarity.Common, [("Attack", 10.0)], Now);
        _inventory.GetEquipmentByIdAsync(sword.Id, Arg.Any<CancellationToken>()).Returns(sword);

        await Handler().Handle(new RepairWeaponCommand(PlayerId, sword.Id), CancellationToken.None);

        Assert.Equal(500, _wallet.GemBalance.Value);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
