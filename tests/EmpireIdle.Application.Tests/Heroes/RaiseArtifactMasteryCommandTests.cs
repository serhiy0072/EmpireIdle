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
/// Заточка коваля (GDD §9.12): спроба за золото в кузні. Успіх додає ранг бонусу з журналом
/// сіду; невдача з'їдає золото, а заточка не падає.
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

    private Village GivenVillage(bool withForge = true, int gold = 100_000)
    {
        var village = Entities.VillageWithResources(gold, PlayerId);

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

    private Task<bool> Raise(EquipmentItem item)
        => new RaiseArtifactMasteryCommandHandler(_inventory, _villages, _unitOfWork, new FakeTimeProvider(Now),
                new MasteryRules(_config.Equipment), _random, new ArtifactRoller(_config.Equipment), new GameCatalog(_config),
                NullLogger<RaiseArtifactMasteryCommandHandler>.Instance)
            .Handle(new RaiseArtifactMasteryCommand(PlayerId, item.Id), CancellationToken.None);

    private static long Gold(Village village) => village.Resources.Single(r => r.ResourceType == "gold").Amount;

    [Fact]
    public async Task Handle_ShouldRaiseMastery_AndChargeGold_OnSuccess()
    {
        var village = GivenVillage();
        var item = GivenItem();
        _random.NextDouble().Returns(0.99);

        var success = await Raise(item);

        // Перші п'ять спроб певні, навіть із найгіршим кидком
        Assert.True(success);
        Assert.Equal(1, item.Mastery);
        Assert.Equal(100_000 - 400, Gold(village));
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

    /// <summary>Невдача: золото списане й збережене, майстерність та сама, предмет цілий.</summary>
    [Fact]
    public async Task Handle_ShouldKeepTheMastery_AndStillCharge_OnFailure()
    {
        var village = GivenVillage();
        var item = GivenItem(mastery: 5);
        _random.NextDouble().Returns(0.99);

        var success = await Raise(item);

        Assert.False(success);
        Assert.Equal(5, item.Mastery);
        Assert.Equal(100_000 - new MasteryRules(_config.Equipment).Cost(5), Gold(village));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        var village = GivenVillage(withForge: false);
        var item = GivenItem();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Raise(item));

        Assert.Equal(RefusalReasons.BuildingRequired.Key, refusal.Reason);
        Assert.Equal(100_000, Gold(village));
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenGoldRunsShort()
    {
        GivenVillage(gold: 100);
        var item = GivenItem();

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() => Raise(item));

        Assert.Equal(0, item.Mastery);
    }

    /// <summary>Лот у заставі ринку: відмова до списання золота.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhileOnTheMarket_WithoutCharging()
    {
        var village = GivenVillage();
        var item = GivenItem();
        item.PutOnMarket(Now);

        await Assert.ThrowsAsync<InvalidStateException>(() => Raise(item));

        Assert.Equal(100_000, Gold(village));
    }

    [Fact]
    public async Task Handle_ShouldNotTouchSomeoneElsesArtifact()
    {
        GivenVillage();
        var foreign = GivenItem(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Raise(foreign));
    }
}
