using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Ремонт артефакта, зламаного заточкою (GDD §9.12): 50 gems за рівень заточки або один ремкомплект.
/// </summary>
public class RepairArtifactCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PlayerWallet _wallet = new(Guid.NewGuid(), "user-1");
    private readonly GameConfig _config = HeroTestConfig.Create();

    public RepairArtifactCommandTests()
    {
        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(new Player(PlayerId, "tester", "tester@example.com", "user-1", Now));
        _wallets.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_wallet);
        _wallet.AddGems(new GemAmount(1_000), "test", Now);
    }

    /// <summary>Зламаний на заданій заточці: ранги — тими самими кроками, що й у грі.</summary>
    private EquipmentItem GivenBroken(int mastery = 12, Guid? owner = null, bool broken = true)
    {
        var item = new EquipmentItem(Guid.NewGuid(), owner ?? PlayerId, 1, HeroTestConfig.Artifact,
            EquipmentSlot.Artifact, Rarity.Rare, Now);
        var roller = new ArtifactRoller(_config.Equipment);

        for (var i = 0; i < mastery; i++)
        {
            var rank = roller.RollRank("necklace", item.Mastery, item.Stats, seed: i);
            item.ApplyMasteryRank(rank.Position, rank.Stat, rank.Value, Now);
        }

        if (broken)
            item.Break(Now);

        _inventory.GetEquipmentByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        return item;
    }

    private PlayerItem GivenKits(int count)
    {
        var stack = new PlayerItem(Guid.NewGuid(), PlayerId, _config.Equipment.RepairKitItemKey, count);
        _inventory.GetItemAsync(PlayerId, _config.Equipment.RepairKitItemKey, Arg.Any<CancellationToken>()).Returns(stack);
        return stack;
    }

    private Task Repair(EquipmentItem item, bool useKit = false)
        => new RepairArtifactCommandHandler(_inventory, _players, _wallets, _unitOfWork, new FakeTimeProvider(Now),
                new MasteryRules(_config.Equipment), new GameCatalog(_config), NullLogger<RepairArtifactCommandHandler>.Instance)
            .Handle(new RepairArtifactCommand(PlayerId, item.Id, useKit), CancellationToken.None);

    /// <summary>Зламаний на +12 — 600 gems.</summary>
    [Fact]
    public async Task Handle_ShouldRepairForGems_ByTheMastery()
    {
        var item = GivenBroken(mastery: 12);

        await Repair(item);

        Assert.False(item.IsBroken);
        Assert.Equal(400, _wallet.GemBalance.Value);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Ремкомплект — на будь-якій заточці й без gems; останній знімає стек.</summary>
    [Fact]
    public async Task Handle_ShouldRepairWithAKit_WithoutGems()
    {
        var item = GivenBroken(mastery: 19);
        var kits = GivenKits(1);

        await Repair(item, useKit: true);

        Assert.False(item.IsBroken);
        Assert.Equal(0, kits.Count);
        _inventory.Received(1).RemoveItem(kits);
        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WithoutAKit()
    {
        var item = GivenBroken();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Repair(item, useKit: true));

        Assert.Equal(RefusalReasons.ItemNotEnough.Key, refusal.Reason);
        Assert.True(item.IsBroken);
    }

    /// <summary>Gems не вистачає — відмова з цифрами, предмет лишається зламаним.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhenGemsRunShort()
    {
        var item = GivenBroken(mastery: 20);
        _wallet.SpendGems(new GemAmount(500), "test", PlayerId, Now);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() => Repair(item));

        Assert.True(item.IsBroken);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Цілий предмет не ремонтується — і нічого не списується.</summary>
    [Fact]
    public async Task Handle_ShouldRefuseAWholeItem_WithoutCharging()
    {
        var item = GivenBroken(broken: false);
        var kits = GivenKits(1);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() => Repair(item, useKit: true));

        Assert.Equal(RefusalReasons.EquipmentNotBroken.Key, refusal.Reason);
        Assert.Equal(1, kits.Count);
    }

    [Fact]
    public async Task Handle_ShouldNotTouchSomeoneElsesArtifact()
    {
        var foreign = GivenBroken(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Repair(foreign));

        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }
}
