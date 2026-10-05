using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Servers.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Servers;

public class EvolveServerCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    /// <summary>
    /// Мала карта навмисно: радіус 10, туман на 1 рівні — 4, відкрита площа
    /// 81 клітина. Так поріг щільності досягається десятками сіл, а не тисячами.
    /// </summary>
    private static GameConfig Config() => new()
    {
        BuildingLevelsPerTier = 10,
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Map = new MapConfig
        {
            Width = 20,
            Height = 20,
            FullyOpenAtLevel = 3,
            Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.20, 0.50],
                RingMultipliers = [2.0, 1.4, 1.0],
                RingsAtFirstLevel = 0.40,
                FogMinShare = 0.40,
                FogMaxShare = 1.0
            },
            Evolution = new ServerEvolutionConfig
            {
                DensityThreshold = 0.35,
                DaysPerLevel = 45
            }
        }
    };

    private EvolveServerCommandHandler Handler()
    {
        var config = Config();

        return new EvolveServerCommandHandler(
            _servers, _villages, _unitOfWork,
            new GameCatalog(config),
            new WorldGeometry(config.Map),
            new FakeTimeProvider(Now),
            NullLogger<EvolveServerCommandHandler>.Instance);
    }

    /// <summary>Сервер заданого віку з заданою кількістю сіл.</summary>
    private Server GivenServer(int daysOld, int villages)
    {
        var server = new Server(1, "Test", Now.AddDays(-daysOld));

        _servers.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(server);
        _villages.CountAsync(Arg.Any<CancellationToken>()).Returns(villages);

        return server;
    }

    /// <summary>Неіснуючий світ — не помилка: джоб міг спіймати видалений сервер.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenServerIsMissing()
    {
        _servers.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns((Server?)null);

        await Handler().Handle(new EvolveServerCommand(1), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Рівень росте з часом (GDD §2.7): минув період — світ на рівень вищий, без умов зрілості.</summary>
    [Fact]
    public async Task Handle_ShouldRaiseLevel_WhenThePeriodElapsed()
    {
        var server = GivenServer(daysOld: 45, villages: 5);

        await Handler().Handle(new EvolveServerCommand(1), CancellationToken.None);

        Assert.Equal(2, server.Level);
        Assert.Equal(Now, server.LevelRaisedAt);
    }

    [Fact]
    public async Task Handle_ShouldNotRaiseLevel_BeforeThePeriodEnds()
    {
        var server = GivenServer(daysOld: 44, villages: 5);

        await Handler().Handle(new EvolveServerCommand(1), CancellationToken.None);

        Assert.Equal(1, server.Level);
    }

    /// <summary>
    /// Стелі немає: світ іде далі за рівень, на якому карта відкрита повністю.
    /// Період відлічується від останнього підйому, а не від створення.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldKeepRaisingLevel_PastTheFullyOpenMap()
    {
        var server = GivenServer(daysOld: 400, villages: 5);
        server.RaiseLevel(Now.AddDays(-300));
        server.RaiseLevel(Now.AddDays(-200));
        server.RaiseLevel(Now.AddDays(-50));

        await Handler().Handle(new EvolveServerCommand(1), CancellationToken.None);

        Assert.Equal(5, server.Level);
    }

    /// <summary>
    /// Щільність закриває реєстрацію незалежно від рівня: заповнений світ
    /// не розтягується, натомість новачки йдуть у наступний.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCloseRegistration_WhenDensityExceedsTheThreshold()
    {
        // 81 клітина відкритої площі × 0.35 = 29
        var server = GivenServer(daysOld: 1, villages: 30);

        await Handler().Handle(new EvolveServerCommand(1), CancellationToken.None);

        Assert.False(server.AcceptsNewPlayers);
    }

    /// <summary>Нижче порога світ лишається відкритим.</summary>
    [Fact]
    public async Task Handle_ShouldStayOpen_BelowTheThreshold()
    {
        var server = GivenServer(daysOld: 1, villages: 10);

        await Handler().Handle(new EvolveServerCommand(1), CancellationToken.None);

        Assert.True(server.AcceptsNewPlayers);
    }

    /// <summary>
    /// Закритий світ розвивається далі: закриття реєстрації означає
    /// «новачків не беремо», а не «зупинилися».
    /// </summary>
    [Fact]
    public async Task Handle_ShouldStillEvolve_WhenRegistrationIsClosed()
    {
        var server = GivenServer(daysOld: 50, villages: 30);
        server.CloseRegistration(Now.AddDays(-10));

        await Handler().Handle(new EvolveServerCommand(1), CancellationToken.None);

        Assert.Equal(2, server.Level);
    }

    /// <summary>Нічого не змінилось — нічого й не зберігаємо.</summary>
    [Fact]
    public async Task Handle_ShouldNotSave_WhenNothingChanged()
    {
        GivenServer(daysOld: 1, villages: 1);

        await Handler().Handle(new EvolveServerCommand(1), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
