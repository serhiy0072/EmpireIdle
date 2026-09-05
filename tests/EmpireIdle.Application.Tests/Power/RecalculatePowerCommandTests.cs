using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Power.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Power;

public class RecalculatePowerCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IPlayerPowerRepository _powers = Substitute.For<IPlayerPowerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Units =
        [
            new UnitConfig
            {
                Key = "infantry",
                Stats = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 12 }
            }
        ],
        Combat = new CombatConfig()
    };

    private RecalculatePowerCommandHandler Handler()
    {
        var config = Config();

        return new RecalculatePowerCommandHandler(
            _garrisons, _villages, _marches, _powers, _unitOfWork,
            new CombatCalculator(config.Combat, new GameCatalog(config)),
            new FakeTimeProvider(Now),
            NullLogger<RecalculatePowerCommandHandler>.Instance);
    }

    /// <summary>Гарнізон із юнітами, село, порожній список маршів.</summary>
    private Garrison GivenGarrison(int garrisonInfantry = 10)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        if (garrisonInfantry > 0)
            garrison.ReceiveUnits(new Dictionary<string, int> { ["infantry"] = garrisonInfantry }, Now);

        _garrisons.GetByIdAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([]);
        _powers.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((PlayerPower?)null);

        return garrison;
    }

    /// <summary>Перший перерахунок створює рядок сили.</summary>
    [Fact]
    public async Task Handle_ShouldCreatePowerOnFirstCalculation()
    {
        var garrison = GivenGarrison(garrisonInfantry: 10);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        await _powers.Received(1).AddAsync(
            Arg.Is<PlayerPower>(p => p.PlayerId == PlayerId && p.TotalPower == 100),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Армія в поході входить у силу. Інакше Power падала б під час атаки,
    /// і гравці тримали б військо вдома заради рейтингу.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldIncludeUnitsOnActiveMarches()
    {
        var garrison = GivenGarrison(garrisonInfantry: 10);

        var march = new March(
            Guid.NewGuid(), 1, garrison.Id, 0, 0, 5, 5,
            MarchTargetType.Monster, Guid.NewGuid(),
            new Dictionary<string, int> { ["infantry"] = 5 },
            Now.AddHours(1), Now);

        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        // 15 юнітів × 10 атаки
        await _powers.Received(1).AddAsync(
            Arg.Is<PlayerPower>(p => p.TotalPower == 150), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Наступні перерахунки пишуть абсолютне значення в наявний рядок,
    /// а не дельту: пропущена подія коштує затримки, а не назавжди хибного числа.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldOverwriteExistingPower()
    {
        var garrison = GivenGarrison(garrisonInfantry: 3);

        var existing = new PlayerPower(Guid.NewGuid(), PlayerId, 1, Now.AddDays(-1));
        existing.Set(9999, 0, 0, Now.AddDays(-1));

        _powers.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(existing);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        Assert.Equal(30, existing.TotalPower);
        await _powers.DidNotReceive().AddAsync(Arg.Any<PlayerPower>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Порожній гарнізон дає нульову силу, а не помилку.</summary>
    [Fact]
    public async Task Handle_ShouldWriteZero_ForAnEmptyGarrison()
    {
        var garrison = GivenGarrison(garrisonInfantry: 0);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        await _powers.Received(1).AddAsync(
            Arg.Is<PlayerPower>(p => p.TotalPower == 0), Arg.Any<CancellationToken>());
    }

    /// <summary>Зниклий гарнізон — не помилка: подія могла прийти після видалення.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheGarrisonIsMissing()
    {
        _garrisons.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Garrison?)null);

        await Handler().Handle(new RecalculatePowerCommand(Guid.NewGuid()), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
