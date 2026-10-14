using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Заточка коваля (GDD §9.12): спроба за камені для заточування в кузні. Успіх додає ранг бонусу з журналом
/// сіду; невдача з'їдає камені, а заточка не падає; на пізніх рангах невдача може зламати предмет.
/// </summary>
public class RaiseArtifactMasteryCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRandomSource _random = Substitute.For<IRandomSource>();
    private readonly GameConfig _config = HeroTestConfig.Create();

    private PlayerItem? _stones;

    /// <summary>Село з кузнею і камені в рюкзаку; stones: 0 — каменів немає зовсім.</summary>
    private Village GivenVillage(bool withForge = true, int stones = 1_000)
    {
        var village = Entities.VillageWithResources(100_000, PlayerId);

        _stones = stones > 0 ? new PlayerItem(Guid.NewGuid(), PlayerId, GameConfigBuilder.Whetstone, stones) : null;
        _inventory.GetItemAsync(PlayerId, GameConfigBuilder.Whetstone, Arg.Any<CancellationToken>()).Returns(_stones);

        if (withForge)
            village.AddBuilding(HeroTestConfig.Forge,
                new Dictionary<string, BuildingConfig> { [HeroTestConfig.Forge] = new() { Key = HeroTestConfig.Forge } }, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        return village;
    }

    private EquipmentItem GivenItem(int mastery = 0, Guid? owner = null)
    {
        var item = new EquipmentItem(Guid.NewGuid(), owner ?? PlayerId, 1, HeroTestConfig.Artifact,
            EquipmentSlot.Artifact, Rarity.Rare, Now);
        var roller = new ArtifactRoller(_config.Equipment);

        // Заточка — тими самими рангами, що й у грі: позиції бонусів мають іти по колу
        for (var i = 0; i < mastery; i++)
        {
            var rank = roller.RollRank("necklace", item.Mastery, item.Stats, seed: i);
            item.ApplyMasteryRank(rank.Position, rank.Stat, rank.Value, Now);
        }

        _inventory.GetEquipmentByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        return item;
    }

    private Task<MasteryOutcome> Raise(EquipmentItem item)
        => new RaiseArtifactMasteryCommandHandler(_inventory, _villages, _unitOfWork, new FakeTimeProvider(Now),
                new MasteryRules(_config.Equipment), _random, new ArtifactRoller(_config.Equipment), new GameCatalog(_config),
                NullLogger<RaiseArtifactMasteryCommandHandler>.Instance)
            .Handle(new RaiseArtifactMasteryCommand(PlayerId, item.Id), CancellationToken.None);

    private int Stones() => _stones?.Count ?? 0;

    private int Cost(int mastery) => new MasteryRules(_config.Equipment).Cost(mastery);

    [Fact]
    public async Task Handle_ShouldRaiseMastery_AndChargeStones_OnSuccess()
    {
        GivenVillage();
        var item = GivenItem();
        _random.NextDouble().Returns(0.99);

        var outcome = await Raise(item);

        // Перші п'ять спроб певні, навіть із найгіршим кидком
        Assert.Equal(MasteryOutcome.Success, outcome);
        Assert.Equal(1, item.Mastery);
        Assert.Equal(1_000 - 10, Stones());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Перший ранг — перший сталий бонус намиста, а сід лягає в журнал під новою заточкою.</summary>
    [Fact]
    public async Task Handle_ShouldAddTheRankBonus_AndRecordItsSeed()
    {
        GivenVillage();
        var item = GivenItem();
        _random.NextDouble().Returns(0.0);
        _random.Next(Arg.Any<int>()).Returns(4242);

        await Raise(item);

        var bonus = Assert.Single(item.Stats);
        Assert.Equal(("Attack", 0), (bonus.StatKey, bonus.Position));
        Assert.Contains(bonus.Value, _config.Equipment.FindArtifactBonus("Attack")!.Steps);
        var roll = Assert.Single(item.Rolls);
        Assert.Equal((1, 4242), (roll.Mastery, roll.Seed));
    }

    /// <summary>Третій ранг відкриває випадковий бонус — із пулу намиста.</summary>
    [Fact]
    public async Task Handle_ShouldOpenARandomBonus_OnTheThirdRank()
    {
        GivenVillage();
        var item = GivenItem(mastery: 2);
        _random.NextDouble().Returns(0.0);

        await Raise(item);

        var pool = _config.Equipment.FindArtifactSlot("necklace")!.RandomBonuses.Select(e => e.Stat);
        Assert.Contains(Assert.Single(item.Stats, s => s.Position == 2).StatKey, pool);
    }

    /// <summary>Невдача не додає ні рангу, ні запису в журнал.</summary>
    [Fact]
    public async Task Handle_ShouldNotRollABonus_OnFailure()
    {
        GivenVillage();
        var item = GivenItem(mastery: 5);
        var before = item.Stats.Sum(s => s.Value);
        _random.NextDouble().Returns(0.99);

        await Raise(item);

        Assert.Equal(before, item.Stats.Sum(s => s.Value), 3);
        Assert.Empty(item.Rolls);
    }

    /// <summary>Невдача до +10: камені списані й збережені, заточка та сама, предмет цілий.</summary>
    [Fact]
    public async Task Handle_ShouldKeepTheMastery_AndStillCharge_OnFailure()
    {
        GivenVillage();
        var item = GivenItem(mastery: 5);
        _random.NextDouble().Returns(0.99, 0.0);

        var outcome = await Raise(item);

        Assert.Equal(MasteryOutcome.Failed, outcome);
        Assert.False(item.IsBroken);
        Assert.Equal(5, item.Mastery);
        Assert.Equal(1_000 - Cost(5), Stones());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Невдача на ризиковому ранзі й програний кидок поломки: предмет зламаний, заточка та сама.</summary>
    [Fact]
    public async Task Handle_ShouldBreakTheItem_OnARiskyFailure()
    {
        GivenVillage();
        var item = GivenItem(mastery: 15);
        _random.NextDouble().Returns(0.99, 0.0);

        var outcome = await Raise(item);

        Assert.Equal(MasteryOutcome.Broken, outcome);
        Assert.True(item.IsBroken);
        Assert.Equal(15, item.Mastery);
        Assert.Equal(1_000 - Cost(15), Stones());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Зламаний не точиться — відмова до списання каменів.</summary>
    [Fact]
    public async Task Handle_ShouldRefuseABrokenItem_WithoutCharging()
    {
        GivenVillage();
        var item = GivenItem(mastery: 12);
        item.Break(Now);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() => Raise(item));

        Assert.Equal(RefusalReasons.EquipmentBroken.Key, refusal.Reason);
        Assert.Equal(1_000, Stones());
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AtTheMaxMastery()
    {
        GivenVillage();
        var item = GivenItem(mastery: _config.Equipment.MaxMastery);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Raise(item));

        Assert.Equal(RefusalReasons.EquipmentMaxMastery.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WithoutAForge()
    {
        GivenVillage(withForge: false);
        var item = GivenItem();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Raise(item));

        Assert.Equal(RefusalReasons.BuildingRequired.Key, refusal.Reason);
        Assert.Equal(1_000, Stones());
    }

    /// <summary>Каменів менше, ніж коштує спроба: відмова з причиною, кидка немає, камені цілі.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhenStonesRunShort()
    {
        GivenVillage(stones: 9);
        var item = GivenItem();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Raise(item));

        Assert.Equal(RefusalReasons.ItemNotEnough.Key, refusal.Reason);
        Assert.Equal(0, item.Mastery);
        Assert.Equal(9, Stones());
    }

    /// <summary>Каменів немає зовсім — та сама відмова, а не падіння.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WithoutStones()
    {
        GivenVillage(stones: 0);
        var item = GivenItem();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Raise(item));

        Assert.Equal(RefusalReasons.ItemNotEnough.Key, refusal.Reason);
    }

    /// <summary>Остання пачка каменів пішла на спробу — порожній стек прибирається з рюкзака.</summary>
    [Fact]
    public async Task Handle_ShouldRemoveTheEmptyStack()
    {
        GivenVillage(stones: 10);
        var item = GivenItem();
        _random.NextDouble().Returns(0.0);

        await Raise(item);

        _inventory.Received(1).RemoveItem(_stones!);
    }

    /// <summary>Лот у заставі ринку: відмова до списання каменів.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhileOnTheMarket_WithoutCharging()
    {
        GivenVillage();
        var item = GivenItem();
        item.PutOnMarket(Now);

        await Assert.ThrowsAsync<InvalidStateException>(() => Raise(item));

        Assert.Equal(1_000, Stones());
    }

    [Fact]
    public async Task Handle_ShouldNotTouchSomeoneElsesArtifact()
    {
        GivenVillage();
        var foreign = GivenItem(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Raise(foreign));
    }
}
