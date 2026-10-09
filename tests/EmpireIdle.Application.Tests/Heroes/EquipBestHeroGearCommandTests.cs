using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// «Швидке використання» (GDD §6.4): у кожен слот артефакта — найкраще вільне за рідкістю,
/// далі за заточкою; чуже, зламане й виставлене на ринок не чіпає.
/// </summary>
public class EquipBestHeroGearCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid GarrisonId = Guid.NewGuid();

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<EquipmentItem> _owned = [];

    public EquipBestHeroGearCommandTests()
    {
        _inventory.GetEquipmentAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _owned.ToList());
        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _owned.Where(e => e.EquippedByHeroId == call.Arg<Guid>()).ToList());
    }

    private EquipBestHeroGearCommandHandler Handler()
    {
        var catalog = HeroTestConfig.Catalog();

        return new(_heroes, _inventory, _unitOfWork, new FakeTimeProvider(Now), new EquipmentFit(catalog),
            NullLogger<EquipBestHeroGearCommandHandler>.Instance);
    }

    private Hero GivenHero()
    {
        var hero = new Hero(Guid.NewGuid(), PlayerId, 1, HeroTestConfig.CommonHero, GarrisonId, asLeader: true, Now);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        return hero;
    }

    private EquipmentItem GivenItem(string itemKey, Rarity rarity = Rarity.Common, int level = 0)
    {
        var item = new EquipmentItem(Guid.NewGuid(), PlayerId, 1, itemKey, EquipmentSlot.Artifact, rarity, [("Attack", 10.0)], Now);

        if (level > 0)
            item.GainExperience(100 * level, level, Now);

        _owned.Add(item);
        _inventory.GetEquipmentByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        return item;
    }

    private Task<int> QuickEquip(Hero hero)
        => Handler().Handle(new EquipBestHeroGearCommand(PlayerId, hero.Id), CancellationToken.None);

    /// <summary>Порожній герой отримує артефакти — кожен у слот свого типу.</summary>
    [Fact]
    public async Task Handle_ShouldFillEveryEmptySlot()
    {
        var hero = GivenHero();
        var necklace = GivenItem(HeroTestConfig.Artifact);
        var ring = GivenItem(HeroTestConfig.SecondArtifact);

        var equipped = await QuickEquip(hero);

        Assert.Equal(2, equipped);
        Assert.Equal(HeroTestConfig.NecklaceSlot, necklace.SlotIndex);
        Assert.Equal(HeroTestConfig.RingSlot, ring.SlotIndex);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Рідкість важить більше за заточку, а за рівної рідкості перемагає заточка.</summary>
    [Fact]
    public async Task Handle_ShouldPreferRarity_ThenEnhancement()
    {
        var hero = GivenHero();
        GivenItem(HeroTestConfig.Artifact, Rarity.Common, level: 3);
        GivenItem(HeroTestConfig.Artifact, Rarity.Rare, level: 0);
        var best = GivenItem(HeroTestConfig.Artifact, Rarity.Rare, level: 2);

        await QuickEquip(hero);

        Assert.Equal(hero.Id, best.EquippedByHeroId);
        Assert.Single(_owned, e => e.EquippedByHeroId == hero.Id);
    }

    /// <summary>Вдягнене на іншого героя й виставлене на ринок — не вільне.</summary>
    [Fact]
    public async Task Handle_ShouldTakeOnlyFreeItems()
    {
        var hero = GivenHero();
        var other = GivenHero();

        var worn = GivenItem(HeroTestConfig.Artifact, Rarity.Unique);
        worn.EquipTo(other.Id, HeroTestConfig.NecklaceSlot, Now);

        var listed = GivenItem(HeroTestConfig.Artifact, Rarity.Unique);
        listed.PutOnMarket(Now);

        var plain = GivenItem(HeroTestConfig.Artifact);

        await QuickEquip(hero);

        Assert.Equal(other.Id, worn.EquippedByHeroId);
        Assert.Null(listed.EquippedByHeroId);
        Assert.Equal(hero.Id, plain.EquippedByHeroId);
    }

    /// <summary>Гірше за вдягнене замінюється, рівне — ні: автоекіп не міняє шило на швайку.</summary>
    [Fact]
    public async Task Handle_ShouldReplaceOnlyWorseGear()
    {
        var hero = GivenHero();
        var worse = GivenItem(HeroTestConfig.Artifact, Rarity.Common);
        worse.EquipTo(hero.Id, HeroTestConfig.NecklaceSlot, Now);
        var keeper = GivenItem(HeroTestConfig.SecondArtifact, Rarity.Rare);
        keeper.EquipTo(hero.Id, HeroTestConfig.RingSlot, Now);

        var upgrade = GivenItem(HeroTestConfig.Artifact, Rarity.Rare);
        var equal = GivenItem(HeroTestConfig.SecondArtifact, Rarity.Rare);

        var equipped = await QuickEquip(hero);

        Assert.Equal(1, equipped);
        Assert.Null(worse.EquippedByHeroId);
        Assert.Equal(hero.Id, upgrade.EquippedByHeroId);
        Assert.Equal(hero.Id, keeper.EquippedByHeroId);
        Assert.Null(equal.EquippedByHeroId);
    }

    /// <summary>Кращого вільного немає — нічого не змінюється й нічого не зберігається.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenNothingIsBetter()
    {
        var hero = GivenHero();
        var worn = GivenItem(HeroTestConfig.Artifact, Rarity.Unique);
        worn.EquipTo(hero.Id, HeroTestConfig.NecklaceSlot, Now);
        GivenItem(HeroTestConfig.Artifact, Rarity.Common);

        var equipped = await QuickEquip(hero);

        Assert.Equal(0, equipped);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
