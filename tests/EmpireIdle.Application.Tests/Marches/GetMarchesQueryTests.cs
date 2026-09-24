using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Список активних походів для клієнта: назва цілі, порядок за прибуттям
/// і ціна прискорення — усе, що потрібно панелі маршів без другого запиту.
/// </summary>
public class GetMarchesQueryTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IMonsterRepository _monsters = Substitute.For<IMonsterRepository>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 }],
        Monsters =
        [
            new MonsterConfig
            {
                Key = "wolves", DisplayName = "Вовки", MinLevel = 1, MaxLevel = 10, UnitGrowth = 1.5, RewardGrowth = 1.3,
                Units = [new UnitStack { UnitType = "infantry", Count = 1 }],
                Rewards = [new ResourceCost { Resource = "food", Amount = 500 }]
            }
        ],
        Monetization = new MonetizationConfig
        {
            InstantFinishThresholdMinutes = 5,
            SpeedUpFactor = 2.0,
            SpeedUpExponent = 0.75
        }
    };

    private static SpeedUpCalculator Calculator() => new(Config().Monetization);

    private GetMarchesQueryHandler Handler() => new(
        _villages, _garrisons, _marches, _monsters, new GameCatalog(Config()), new FakeTimeProvider(Now), Calculator());

    private Garrison GivenGarrison()
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", [], 50, 50);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, village.ServerId);

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdReadOnlyAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return garrison;
    }

    private static March MarchTo(Garrison garrison, MarchTargetType targetType, Guid targetId, DateTime arrivesAt) => new(
        Guid.NewGuid(), 1, garrison.Id, Guid.NewGuid(), 50, 50, 55, 55, targetType, targetId,
        new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 },
        arrivesAt, Now.AddMinutes(-10));

    /// <summary>
    /// Назва монстра йде з довідника, як у прев'ю бою — гравець бачить те саме, що обирав.
    /// Рівень окремим полем: англійське «lvl» у назві бачив би гравець.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNameAMonsterTarget_FromTheCatalog()
    {
        var garrison = GivenGarrison();
        var monster = new Monster(Guid.NewGuid(), 1, "wolves", 3, 55, 55, Now);
        var march = MarchTo(garrison, MarchTargetType.Monster, monster.Id, Now.AddMinutes(30));

        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);
        _monsters.GetByIdAsync(monster.Id, Arg.Any<CancellationToken>()).Returns(monster);

        var views = await Handler().Handle(new GetMarchesQuery(PlayerId), CancellationToken.None);

        var view = Assert.Single(views);
        Assert.Equal("Вовки", view.TargetName);
        Assert.Equal(3, view.TargetLevel);
        Assert.Equal(MarchState.Outbound, view.State);
        Assert.Equal(10, view.Units.Single().Count);
    }

    /// <summary>Монстра вже вбили, а армія ще вертається: марш лишається в списку, назва — null.</summary>
    [Fact]
    public async Task Handle_ShouldKeepTheMarch_WhenTheTargetIsGone()
    {
        var garrison = GivenGarrison();
        var march = MarchTo(garrison, MarchTargetType.Monster, Guid.NewGuid(), Now.AddMinutes(30));

        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);
        _monsters.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Monster?)null);

        var views = await Handler().Handle(new GetMarchesQuery(PlayerId), CancellationToken.None);

        var view = Assert.Single(views);
        Assert.Null(view.TargetName);
    }

    /// <summary>Найближче прибуття першим і ціна прискорення як у SpeedUpCalculator.</summary>
    [Fact]
    public async Task Handle_ShouldOrderByArrival_AndPriceTheSpeedUp()
    {
        var garrison = GivenGarrison();
        var target = new Village(Guid.NewGuid(), Guid.NewGuid(), "Neighbour", [], 55, 55);
        var late = MarchTo(garrison, MarchTargetType.Village, target.Id, Now.AddMinutes(120));
        var soon = MarchTo(garrison, MarchTargetType.Village, target.Id, Now.AddMinutes(2));

        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([late, soon]);
        _villages.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        var views = await Handler().Handle(new GetMarchesQuery(PlayerId), CancellationToken.None);

        Assert.Equal([soon.Id, late.Id], views.Select(v => v.Id));
        Assert.Equal("Neighbour", views[0].TargetName);
        Assert.Null(views[0].TargetLevel);
        Assert.Equal(0, views[0].SpeedUpCostGems);
        Assert.Equal(Calculator().GetInstantFinishCost(late.ArrivesAt, Now), views[1].SpeedUpCostGems);
        Assert.True(views[1].SpeedUpCostGems > 0, "120 хвилин мають коштувати gems, інакше тест нічого не перевіряє.");
    }
}
