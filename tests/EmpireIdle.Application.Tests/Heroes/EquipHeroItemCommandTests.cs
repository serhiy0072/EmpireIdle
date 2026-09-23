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

/// <summary>Вдягання спорядження: слоти, клас зброї, заміна зайнятого.</summary>
public class EquipHeroItemCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid GarrisonId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private EquipHeroItemCommandHandler Handler() => new(
        _heroes, _inventory, _unitOfWork, new FakeTimeProvider(Now),
        HeroTestConfig.Catalog(), NullLogger<EquipHeroItemCommandHandler>.Instance);

    private Hero GivenHero(string heroKey = "warrior_bran")
    {
        var hero = new Hero(Guid.NewGuid(), PlayerId, ServerId, heroKey, GarrisonId, asLeader: true, Now);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        return hero;
    }

    private EquipmentItem GivenItem(string itemKey, EquipmentSlot slot, Guid? owner = null)
    {
        var item = new EquipmentItem(Guid.NewGuid(), owner ?? PlayerId, ServerId, itemKey, slot,
            Rarity.Common, [("Attack", 10.0)], Now);

        _inventory.GetEquipmentByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        return item;
    }

    private void GivenEquipped(Guid heroId, params EquipmentItem[] items)
        => _inventory.GetEquippedAsync(heroId, Arg.Any<CancellationToken>()).Returns(items.ToList());

    [Fact]
    public async Task Handle_ShouldEquipTheWeapon()
    {
        var hero = GivenHero();
        var sword = GivenItem("sword_iron", EquipmentSlot.Weapon);
        GivenEquipped(hero.Id);

        await Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, sword.Id), CancellationToken.None);

        Assert.Equal(hero.Id, sword.EquippedByHeroId);
        Assert.Equal(0, sword.SlotIndex);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Зайнятий слот означає заміну, а не відмову.</summary>
    [Fact]
    public async Task Handle_ShouldReplaceTheOccupant()
    {
        var hero = GivenHero();
        var old = GivenItem("sword_iron", EquipmentSlot.Weapon);
        old.EquipTo(hero.Id, 0, Now);

        var fresh = GivenItem("sword_steel", EquipmentSlot.Weapon);
        GivenEquipped(hero.Id, old);

        await Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, fresh.Id), CancellationToken.None);

        Assert.Null(old.EquippedByHeroId);
        Assert.Equal(hero.Id, fresh.EquippedByHeroId);
    }

    /// <summary>Артефакт сам визначає слот: намисто — у слот намиста, кільце — у слот кільця.</summary>
    [Fact]
    public async Task Handle_ShouldPutAnArtifactIntoTheSlotOfItsType()
    {
        var hero = GivenHero();
        var necklace = GivenItem(HeroTestConfig.Artifact, EquipmentSlot.Artifact);
        var ring = GivenItem(HeroTestConfig.SecondArtifact, EquipmentSlot.Artifact);
        GivenEquipped(hero.Id);

        await Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, necklace.Id), CancellationToken.None);
        await Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, ring.Id), CancellationToken.None);

        Assert.Equal(HeroTestConfig.NecklaceSlot, necklace.SlotIndex);
        Assert.Equal(HeroTestConfig.RingSlot, ring.SlotIndex);
    }

    /// <summary>
    /// Слот одного типу один: друге намисто — навіть таке саме — замінює перше,
    /// тож два однакові артефакти разом не вдягнути й набір дешевше не закрити.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReplaceAnArtifactOfTheSameType()
    {
        var hero = GivenHero();
        var first = GivenItem(HeroTestConfig.Artifact, EquipmentSlot.Artifact);
        first.EquipTo(hero.Id, HeroTestConfig.NecklaceSlot, Now);

        var second = GivenItem(HeroTestConfig.Artifact, EquipmentSlot.Artifact);
        GivenEquipped(hero.Id, first);

        await Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, second.Id), CancellationToken.None);

        Assert.Null(first.EquippedByHeroId);
        Assert.Equal(hero.Id, second.EquippedByHeroId);
        Assert.Equal(HeroTestConfig.NecklaceSlot, second.SlotIndex);
    }

    [Fact]
    public async Task Handle_ShouldRejectAWeaponOfTheWrongClass()
    {
        var hero = GivenHero("mage_iselle");
        var sword = GivenItem("sword_iron", EquipmentSlot.Weapon);
        GivenEquipped(hero.Id);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, sword.Id), CancellationToken.None));
        Assert.Equal(RefusalReasons.EquipmentClassMismatch.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRejectAHeroOnTheMove()
    {
        var hero = GivenHero();
        hero.Deploy(Now);

        var sword = GivenItem("sword_iron", EquipmentSlot.Weapon);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, sword.Id), CancellationToken.None));
        Assert.Equal(RefusalReasons.HeroOnTheMove.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRejectABrokenWeapon()
    {
        var hero = GivenHero();
        var sword = GivenItem("sword_iron", EquipmentSlot.Weapon);
        sword.Break(Now);
        GivenEquipped(hero.Id);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, sword.Id), CancellationToken.None));
        Assert.Equal(RefusalReasons.EquipmentBroken.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheItemBelongsToAnotherPlayer()
    {
        var hero = GivenHero();
        var sword = GivenItem("sword_iron", EquipmentSlot.Weapon, owner: Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, sword.Id), CancellationToken.None));
    }

    /// <summary>Повторне вдягання в той самий слот нічого не міняє.</summary>
    [Fact]
    public async Task Handle_ShouldBeIdempotent()
    {
        var hero = GivenHero();
        var sword = GivenItem("sword_iron", EquipmentSlot.Weapon);
        sword.EquipTo(hero.Id, 0, Now);
        GivenEquipped(hero.Id, sword);

        await Handler().Handle(new EquipHeroItemCommand(PlayerId, hero.Id, sword.Id), CancellationToken.None);

        Assert.Equal(hero.Id, sword.EquippedByHeroId);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
