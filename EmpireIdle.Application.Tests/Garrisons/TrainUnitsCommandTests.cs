using EmpireIdle.Application.Common.Services;
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

public class TrainUnitsCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private EffectResolver Effects() => new(_effects);

    private static GameConfig Config() => new()
    {
        MaxTrainingBatchSize = 50,
        ArmyCapacityPerBarracksLevel = 20,
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 },
            new BuildingConfig
            {
                Key = "barracks",
                UpgradeCostGrowth = 1.45,
                BaseBuildMinutes = 10,
                BuildTimeGrowth = 1.5,
                Cost = [new ResourceCost { Resource = "food", Amount = 10 }]
            },
            new BuildingConfig { Key = "warehouse", StoresResources = ["food"], UpgradeCostGrowth = 1.45 }
        ],
        Units =
        [
            new UnitConfig
            {
                Key = "infantry",
                RequiresBuilding = "barracks",
                RequiresBuildingLevel = 1,
                BaseTrainMinutes = 2,
                Cost = [new ResourceCost { Resource = "food", Amount = 10 }]
            },
            new UnitConfig
            {
                Key = "siege",
                RequiresBuilding = "barracks",
                RequiresBuildingLevel = 6,
                BaseTrainMinutes = 15,
                Cost = [new ResourceCost { Resource = "food", Amount = 50 }]
            }
        ]
    };

    private TrainUnitsCommandHandler Handler() => new(
        _villages, _garrisons, _unitOfWork, new FakeTimeProvider(Now),
        NullLogger<TrainUnitsCommandHandler>.Instance, new GameCatalog(Config()));

    /// <summary>Село з казармами заданого рівня, гарнізон, ресурси.</summary>
    private (Village Village, Garrison Garrison) GivenVillage(
        int barracksLevel = 1, int food = 10_000, bool barracksUnderConstruction = false)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = food }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("barracks", catalog.Buildings, Now);

        var barracks = village.Buildings.Single(b => b.Type == "barracks");

        for (var level = 1; level < barracksLevel; level++)
        {
            barracks.BeginUpgrade(catalog.Buildings["barracks"], TimeSpan.Zero, Now,
                ProductionBoost.None, locationMultiplier: 1.0);
            barracks.CompleteConstruction(Now);
        }

        if (barracksUnderConstruction)
            barracks.BeginUpgrade(catalog.Buildings["barracks"], TimeSpan.FromHours(1), Now,
                ProductionBoost.None, locationMultiplier: 1.0);

        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return (village, garrison);
    }

    /// <summary>Вартість списується за кількість, а не за партію.</summary>
    [Fact]
    public async Task Handle_ShouldChargeCostPerUnit()
    {
        var (village, _) = GivenVillage(food: 1000);

        await Handler().Handle(new TrainUnitsCommand(PlayerId, "infantry", 5), CancellationToken.None);

        // 5 × 10 = 50
        Assert.Equal(950, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>Замовлення стає в чергу з часом, пропорційним кількості.</summary>
    [Fact]
    public async Task Handle_ShouldQueueTrainingOrder()
    {
        var (_, garrison) = GivenVillage();

        await Handler().Handle(new TrainUnitsCommand(PlayerId, "infantry", 5), CancellationToken.None);

        var order = Assert.Single(garrison.TrainingOrders);
        Assert.Equal("infantry", order.UnitType);
        Assert.Equal(5, order.Count);
        Assert.Equal(Now.AddMinutes(10), order.CompletesAt);
    }

    /// <summary>
    /// Рівень будівлі гейтить тип юніта: інакше казарма 1 рівня відкриває
    /// всю армію одразу, і качати її немає причин.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenBuildingLevelIsTooLow()
    {
        GivenVillage(barracksLevel: 3);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new TrainUnitsCommand(PlayerId, "siege", 1), CancellationToken.None));
    }

    /// <summary>Той самий юніт доступний, коли казарма доросла.</summary>
    [Fact]
    public async Task Handle_ShouldAllow_WhenBuildingLevelIsSufficient()
    {
        var (_, garrison) = GivenVillage(barracksLevel: 6);

        await Handler().Handle(new TrainUnitsCommand(PlayerId, "siege", 1), CancellationToken.None);

        Assert.Single(garrison.TrainingOrders);
    }

    /// <summary>
    /// Казарма в процесі апгрейду не рахується: інакше замовлення робилось би
    /// наперед, під ще недосягнутий рівень.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheBuildingIsUnderConstruction()
    {
        GivenVillage(barracksLevel: 1, barracksUnderConstruction: true);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new TrainUnitsCommand(PlayerId, "infantry", 1), CancellationToken.None));
    }

    /// <summary>
    /// Ліміт армії від рівня казарм — те, що замінило населення.
    /// Казарма 1 рівня × 20 = 20 юнітів.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenArmyCapacityIsExceeded()
    {
        GivenVillage(barracksLevel: 1);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new TrainUnitsCommand(PlayerId, "infantry", 21), CancellationToken.None));
    }

    /// <summary>Вища казарма піднімає стелю армії.</summary>
    [Fact]
    public async Task Handle_ShouldRaiseArmyCapacity_WithTheBuildingLevel()
    {
        var (_, garrison) = GivenVillage(barracksLevel: 3);

        await Handler().Handle(new TrainUnitsCommand(PlayerId, "infantry", 50), CancellationToken.None);

        Assert.Single(garrison.TrainingOrders);
    }

    /// <summary>Невідомий тип юніта — 404, а не 500.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownUnitType()
    {
        GivenVillage();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new TrainUnitsCommand(PlayerId, "dragon", 1), CancellationToken.None));
    }

    /// <summary>Нестача ресурсів зупиняє операцію до постановки в чергу.</summary>
    [Fact]
    public async Task Handle_ShouldNotQueue_WhenResourcesAreInsufficient()
    {
        var (_, garrison) = GivenVillage(food: 10);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Handler().Handle(new TrainUnitsCommand(PlayerId, "infantry", 5), CancellationToken.None));

        Assert.Empty(garrison.TrainingOrders);
    }
}
