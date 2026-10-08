using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>Відкликання табору (§2.5): власник кличе армію додому звичайним маршем.</summary>
public class RecallCampCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Units = [new UnitConfig { Key = "infantry", Stats = new Dictionary<string, double> { ["Speed"] = 4 } }],
        Map = new MapConfig
        {
            Width = 100,
            Height = 100,
            TerrainSeed = 1,
            Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }]
        }
    };

    private RecallCampCommandHandler Handler()
    {
        var config = Config();
        var catalog = new GameCatalog(config);

        var homecoming = new CampHomecoming(_heroes, new MarchCalculator(new TerrainGenerator(config.Map), catalog),
            new HeroProgression(config.HeroSettings), catalog);

        return new RecallCampCommandHandler(_villages, _garrisons, _marches, _unitOfWork,
            new FakeTimeProvider(Now), homecoming, NullLogger<RecallCampCommandHandler>.Instance);
    }

    /// <summary>Село гравця на (10, 10) і його похід на (40, 40).</summary>
    private March GivenMarch(bool camping = true)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Home", ["food"], 10, 10);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        var march = new March(Guid.NewGuid(), 1, garrison.Id, 10, 10, 40, 40,
            MarchTargetType.Village, Guid.NewGuid(),
            new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 },
            Now.AddHours(-1), Now.AddHours(-2));

        if (camping)
            march.Camp(Now.AddHours(-1));

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);

        return march;
    }

    [Fact]
    public async Task Handle_ShouldSendTheCampHome_AsAMarchFromItsCell()
    {
        var march = GivenMarch();

        await Handler().Handle(new RecallCampCommand(PlayerId, march.Id), CancellationToken.None);

        Assert.Equal(MarchState.Returning, march.State);
        Assert.Equal(Now, march.LegStartedAt);
        Assert.True(march.ArrivesAt > Now);
        Assert.Equal((10, 10), (march.OriginX, march.OriginY));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Похід у дорозі — не табір: відкликати його не можна.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheMarchIsNotCamping()
    {
        var march = GivenMarch(camping: false);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new RecallCampCommand(PlayerId, march.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.MarchNotCamping.Key, refusal.Reason);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Чужий табір серед власних походів не знайдеться — 404, а не відкликання.</summary>
    [Fact]
    public async Task Handle_ShouldNotFind_SomeoneElsesCamp()
    {
        GivenMarch();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new RecallCampCommand(PlayerId, Guid.NewGuid()), CancellationToken.None));
    }
}
