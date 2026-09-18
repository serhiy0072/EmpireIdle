using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>Купівля зброї в кузні.</summary>
public class BuyWeaponCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IRandomSource _random = Substitute.For<IRandomSource>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private BuyWeaponCommandHandler Handler()
    {
        _serverContext.ServerId.Returns(ServerId);

        var config = HeroTestConfig.Create();
        var catalog = new GameCatalog(config);

        var granter = new ItemGranter(_inventory, _serverContext, _random,
            new ArtifactRoller(config.Equipment));

        return new BuyWeaponCommandHandler(
            _villages, _unitOfWork, granter, new FakeTimeProvider(Now), catalog,
            NullLogger<BuyWeaponCommandHandler>.Instance);
    }

    /// <summary>Село з кузнею й золотом.</summary>
    private Village GivenVillage(int gold = 10_000, bool withForge = true)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["gold"], 0, 0, ServerId);
        village.GrantStartingResources(new Dictionary<string, int> { ["gold"] = gold }, Now);

        var configs = HeroTestConfig.Create().Buildings.ToDictionary(b => b.Key);

        village.AddBuilding("townhall", configs, Now);

        if (withForge)
            village.AddBuilding(HeroTestConfig.Forge, configs, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);

        return village;
    }

    [Fact]
    public async Task Handle_ShouldGrantTheWeaponAndChargeGold()
    {
        var village = GivenVillage();

        await Handler().Handle(new BuyWeaponCommand(PlayerId, HeroTestConfig.WarriorWeapon),
            CancellationToken.None);

        await _inventory.Received(1).AddEquipmentAsync(
            Arg.Is<EquipmentItem>(e => e.ItemKey == HeroTestConfig.WarriorWeapon
                && e.Slot == EquipmentSlot.Weapon
                && e.EnhancementLevel == 0),
            Arg.Any<CancellationToken>());

        Assert.Equal(10_000 - HeroTestConfig.WeaponPriceGold,
            village.Resources.Single(r => r.ResourceType == "gold").Amount);
    }

    /// <summary>Зброя приходить із базовими статами свого типу.</summary>
    [Fact]
    public async Task Handle_ShouldCopyBaseStats()
    {
        GivenVillage();

        await Handler().Handle(new BuyWeaponCommand(PlayerId, HeroTestConfig.WarriorWeapon),
            CancellationToken.None);

        await _inventory.Received(1).AddEquipmentAsync(
            Arg.Is<EquipmentItem>(e => e.Stats.Any(s => s.StatKey == "Attack")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenThereIsNoForge()
    {
        GivenVillage(withForge: false);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new BuyWeaponCommand(PlayerId, HeroTestConfig.WarriorWeapon),
                CancellationToken.None));

        await _inventory.DidNotReceive().AddEquipmentAsync(
            Arg.Any<EquipmentItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenGoldIsShort()
    {
        GivenVillage(gold: 10);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Handler().Handle(new BuyWeaponCommand(PlayerId, HeroTestConfig.WarriorWeapon),
                CancellationToken.None));

        await _inventory.DidNotReceive().AddEquipmentAsync(
            Arg.Any<EquipmentItem>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Артефакти не продаються — вони падають у данжах.</summary>
    [Fact]
    public async Task Handle_ShouldReject_ForAnArtifact()
    {
        GivenVillage();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new BuyWeaponCommand(PlayerId, HeroTestConfig.Artifact),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_ForAnUnknownItem()
    {
        GivenVillage();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new BuyWeaponCommand(PlayerId, "sword_of_nothing"), CancellationToken.None));
    }
}
