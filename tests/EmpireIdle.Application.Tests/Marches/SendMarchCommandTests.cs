using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Відправка армії. Ключове — юніти знімаються з гарнізону й потрапляють
/// у марш рівно один раз: подвоєння чи втрата армії тут коштують гравцю
/// всього війська.
/// </summary>
public class SendMarchCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IMonsterRepository _monsters = Substitute.For<IMonsterRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();

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

    private SendMarchCommandHandler Handler()
    {
        var config = Config();
        var catalog = new GameCatalog(config);

        _serverContext.ServerId.Returns(1);

        return new SendMarchCommandHandler(
            _villages, _garrisons, _marches, _monsters, _clans, _unitOfWork, _serverContext,
            catalog,
            new FakeTimeProvider(Now),
            new MarchCalculator(new TerrainGenerator(config.Map), catalog),
            NullLogger<SendMarchCommandHandler>.Instance);
    }

    /// <summary>Село з гарнізоном, монстр на карті, задана кількість активних маршів.</summary>
    private (Garrison Garrison, Monster Monster) GivenState(int infantry = 100, int activeMarches = 0)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 50, 50);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        if (infantry > 0)
            garrison.ReceiveUnits(new Dictionary<string, int> { ["infantry"] = infantry }, Now);

        var monster = new Monster(Guid.NewGuid(), 1, "wolves", 1, 55, 55, Now);

        var existing = Enumerable.Range(0, activeMarches)
            .Select(_ => new March(
                Guid.NewGuid(), 1, garrison.Id, 50, 50, 60, 60,
                MarchTargetType.Monster, Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 1 },
                Now.AddHours(1), Now))
            .ToList();

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _monsters.GetByIdAsync(monster.Id, Arg.Any<CancellationToken>()).Returns(monster);

        return (garrison, monster);
    }

    private static SendMarchCommand Send(Guid targetId, int infantry = 10) =>
        new(PlayerId, MarchTargetType.Monster, targetId,
            new Dictionary<string, int> { ["infantry"] = infantry });

    /// <summary>Юніти зникають із гарнізону — армія не може бути у двох місцях.</summary>
    [Fact]
    public async Task Handle_ShouldRemoveUnitsFromTheGarrison()
    {
        var (garrison, monster) = GivenState(infantry: 100);

        await Handler().Handle(Send(monster.Id, infantry: 30), CancellationToken.None);

        Assert.Equal(70, garrison.Units.Sum(u => u.Count));
    }

    /// <summary>Марш зберігається зі складом армії й часом прибуття в майбутньому.</summary>
    [Fact]
    public async Task Handle_ShouldPersistTheMarchWithArrivalInTheFuture()
    {
        var (_, monster) = GivenState();

        await Handler().Handle(Send(monster.Id, infantry: 10), CancellationToken.None);

        await _marches.Received(1).AddAsync(
            Arg.Is<March>(m => m.ArrivesAt > Now
                               && m.DepartedAt == Now
                               && m.TargetX == 55 && m.TargetY == 55),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ліміт одночасних походів. Без нього гравець розсилав би армію
    /// по одному юніту на кожну ціль карти.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTooManyMarchesAreActive()
    {
        var (_, monster) = GivenState(activeMarches: 3);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id), CancellationToken.None));
    }

    /// <summary>Не можна відправити більше, ніж є в гарнізоні.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenUnitsAreInsufficient()
    {
        var (garrison, monster) = GivenState(infantry: 5);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Handler().Handle(Send(monster.Id, infantry: 10), CancellationToken.None));

        Assert.Equal(5, garrison.Units.Sum(u => u.Count));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Порожня армія — не марш.</summary>
    [Fact]
    public async Task Handle_ShouldReject_AnEmptyArmy()
    {
        var (_, monster) = GivenState();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(
                new SendMarchCommand(PlayerId, MarchTargetType.Monster, monster.Id, new Dictionary<string, int>()),
                CancellationToken.None));
    }

    /// <summary>
    /// Зниклої цілі не буває: монстра міг убити інший гравець між показом
    /// карти й натисканням кнопки — це 404, а не 500.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenTheTargetIsGone()
    {
        GivenState();
        _monsters.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Monster?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(Send(Guid.NewGuid()), CancellationToken.None));
    }

    /// <summary>
    /// Ціль зникла — гарнізон недоторканий. Юніти знімаються ПІСЛЯ резолву,
    /// інакше невдала відправка з'їдала б армію.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotTouchTheGarrison_WhenTheTargetIsGone()
    {
        var (garrison, _) = GivenState(infantry: 100);
        _monsters.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Monster?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(Send(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(100, garrison.Units.Sum(u => u.Count));
    }

    /// <summary>Далі ціль — довший шлях.</summary>
    [Fact]
    public async Task Handle_ShouldScaleTravelTimeWithDistance()
    {
        var (_, near) = GivenState();

        var far = new Monster(Guid.NewGuid(), 1, "wolves", 1, 90, 90, Now);
        _monsters.GetByIdAsync(far.Id, Arg.Any<CancellationToken>()).Returns(far);

        await Handler().Handle(Send(near.Id), CancellationToken.None);
        await Handler().Handle(Send(far.Id), CancellationToken.None);

        var captured = _marches.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMarchRepository.AddAsync))
            .Select(c => (March)c.GetArguments()[0]!)
            .ToList();

        Assert.Equal(2, captured.Count);
        Assert.True(captured[1].ArrivesAt > captured[0].ArrivesAt,
            "Дальша ціль має вимагати більше часу.");
    }
}
