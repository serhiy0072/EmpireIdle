using EmpireIdle.Application.Beasts.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Beasts;

/// <summary>Активація пасивки (GDD §5.10): коштує їжі, діє обмежений час, перезаряджається.</summary>
public class ActivateBeastCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IBeastPenRepository _pens = Substitute.For<IBeastPenRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private ActivateBeastCommandHandler Handler(GameCatalog catalog) => new(_pens, _villages, _servers, _unitOfWork,
        TestEffects.Resolver(_effects, _pens, catalog), new WorldGeometry(catalog.Config.Map), catalog,
        new FakeTimeProvider(Now), NullLogger<ActivateBeastCommandHandler>.Instance);

    private static GameCatalog Catalog(EffectTarget effect = EffectTarget.Attack)
        => new GameConfigBuilder().WithMap().WithBeasts(b => b.Types[0].Effect = effect).BuildCatalog();

    private BeastPen GivenPenWithWolf()
    {
        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);

        _pens.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);
        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);
        return pen;
    }

    private Village GivenVillage(GameCatalog catalog, int food)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", [.. TestKeys.AllResources], 0, 0);
        village.GrantStartingResources(new Dictionary<string, int> { [TestKeys.Food] = food }, Now);
        village.AddBuilding(catalog.MainBuildingKey, catalog.Buildings, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        return village;
    }

    private static long Food(Village village) => village.Resources.Single(r => r.ResourceType == TestKeys.Food).Amount;

    [Fact]
    public async Task Handle_ShouldChargeFoodAndOpenTheWindow()
    {
        var catalog = Catalog();
        GivenPenWithWolf();
        var village = GivenVillage(catalog, food: 500);

        var result = await Handler(catalog).Handle(new ActivateBeastCommand(PlayerId, TestKeys.Beast), CancellationToken.None);

        Assert.Equal(400, Food(village));
        Assert.Equal((Now.AddMinutes(120), Now.AddMinutes(480)), (result.ActiveUntil, result.CooldownUntil));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Без їжі — відмова, і пасивка не вмикається.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WithoutEnoughFood()
    {
        var catalog = Catalog();
        GivenPenWithWolf();
        GivenVillage(catalog, food: 50);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Handler(catalog).Handle(new ActivateBeastCommand(PlayerId, TestKeys.Beast), CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>На перезарядці — відмова до списання: гравець не платить за те, що не ввімкнулось.</summary>
    [Fact]
    public async Task Handle_ShouldRefuseOnCooldown_WithoutCharging()
    {
        var catalog = Catalog();
        var pen = GivenPenWithWolf();
        var village = GivenVillage(catalog, food: 500);
        pen.Activate(TestKeys.Beast, _ => EffectTarget.Attack, TimeSpan.FromHours(2), TimeSpan.FromHours(8), Now.AddHours(-1));

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler(catalog).Handle(new ActivateBeastCommand(PlayerId, TestKeys.Beast), CancellationToken.None));

        Assert.Equal(RefusalReasons.BeastOnCooldown.Key, refusal.Reason);
        Assert.Equal(500, Food(village));
    }

    /// <summary>
    /// Звір виробітку: накопичене за старими вікнами фіксується до нової активації,
    /// інакше попереднє вікно звіра зникло б разом із виробітком за ним.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldMaterializeProduction_BeforeAProductionBeastStarts()
    {
        // Одне кільце з нейтральним множником: тест не про геометрію
        var config = new GameConfigBuilder().WithMap(m => m.Geometry = new Domain.Services.Config.MapGeometryConfig
            {
                RingBoundaries = [0.5], RingMultipliers = [1.0, 1.0], RingsAtFirstLevel = 1.0, FogMinShare = 1.0, FogMaxShare = 1.0
            })
            .WithBuildings(TestKeys.Farm, TestKeys.Warehouse)
            .WithBeasts(b => b.Types[0].Effect = EffectTarget.Production).Build();

        // Ферма TestKit нічого не виробляє: даємо їй їжу, щоб було що фіксувати
        var farmConfig = config.Buildings.Single(b => b.Key == TestKeys.Farm);
        farmConfig.ProducesResource = TestKeys.Food;
        farmConfig.BaseProductionPerMinute = 10;
        farmConfig.BaseStorage = 10_000;

        var catalog = new GameCatalog(config);
        GivenPenWithWolf();
        var village = GivenVillage(catalog, food: 500);
        village.AddBuilding(TestKeys.Farm, catalog.Buildings, Now.AddHours(-1));
        var farm = village.Buildings.Single(b => b.Type == TestKeys.Farm);
        _servers.GetLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(1);

        await Handler(catalog).Handle(new ActivateBeastCommand(PlayerId, TestKeys.Beast), CancellationToken.None);

        Assert.Equal(Now, farm.LastAccruedAt);
        Assert.True(farm.AccruedAmount > 0, "виробіток за годину до активації мав осісти в буфері");
    }
}
