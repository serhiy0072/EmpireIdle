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
/// Майстерність коваля (GDD §6.4): спроба за золото в кузні. Невдача з'їдає золото,
/// але предмет не ламається й майстерність не падає.
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
            EquipmentSlot.Artifact, Rarity.Rare, [("Attack", 10.0)], Now);

        for (var i = 0; i < mastery; i++)
            item.RaiseMastery(Now);

        _inventory.GetEquipmentByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        return item;
    }

    private Task<bool> Raise(EquipmentItem item)
        => new RaiseArtifactMasteryCommandHandler(_inventory, _villages, _unitOfWork, new FakeTimeProvider(Now),
                new MasteryRules(_config.Equipment), _random, new GameCatalog(_config),
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

        // Нижче безпечного рівня спроба певна, навіть із найгіршим кидком
        Assert.True(success);
        Assert.Equal(1, item.Mastery);
        Assert.Equal(100_000 - 500, Gold(village));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
