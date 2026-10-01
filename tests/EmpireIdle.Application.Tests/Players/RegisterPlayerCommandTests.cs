using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Players.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Players;

/// <summary>
/// Реєстрація гравця: одна операція створює акаунт, п'ять сутностей у різних агрегатах
/// і займає клітину на карті. Помилка тут не має середини — або гравець
/// повністю створений, або нічого.
/// </summary>
public class RegisterPlayerCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string UserId = "user-1";

    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMapRepository _map = Substitute.For<IMapRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserAccounts _accounts = Substitute.For<IUserAccounts>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();

    private static GameConfig Config() => new()
    {
        DefaultServerId = 1,
        ActiveServerIds = [1],
        StartingResources = new Dictionary<string, int> { ["food"] = 500, ["wood"] = 300 },
        Resources =
        [
            new ResourceConfig { Key = "food" },
            new ResourceConfig { Key = "wood" }
        ],
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.28 },
            new BuildingConfig
            {
                Key = "farm",
                ProducesResource = "food",
                BaseProductionPerMinute = 10,
                BaseStorage = 600,
                UpgradeCostGrowth = 1.28
            },
            new BuildingConfig
            {
                Key = "sawmill",
                ProducesResource = "wood",
                BaseProductionPerMinute = 8,
                BaseStorage = 800,
                UpgradeCostGrowth = 1.28
            },
            new BuildingConfig
            {
                Key = "warehouse",
                StoresResources = ["food", "wood"],
                BaseStorage = 2000,
                UpgradeCostGrowth = 1.28
            }
        ],
        Map = new MapConfig
        {
            Width = 100,
            Height = 100,
            TerrainSeed = 7,
            MaxServerLevel = 3,
            Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.20, 0.50],
                RingMultipliers = [2.0, 1.4, 1.0],
                RingsAtFirstLevel = 0.40,
                FogMinShare = 0.40,
                FogMaxShare = 1.0
            },
            Terrains =
            [
                new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }
            ]
        }
    };

    private RegisterPlayerCommandHandler Handler()
    {
        var config = Config();
        var geometry = new WorldGeometry(config.Map);
        var terrain = new TerrainGenerator(config.Map);

        _servers.GetLevelAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _map.IsOccupiedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _accounts.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UserId);

        return new RegisterPlayerCommandHandler(
            _accounts, _serverContext,
            _players, _villages, _wallets, _garrisons, _unitOfWork, _servers,
            NullLogger<RegisterPlayerCommandHandler>.Instance,
            new FakeTimeProvider(Now),
            new GameCatalog(config),
            new SettlementPlacer(terrain, geometry, new SystemRandomSource()),
            _map);
    }

    private static RegisterPlayerCommand Create(string email = "Player@Example.COM") =>
        new("Serhiy", email, "Password123");

    /// <summary>Створює гравця, село, гаманець, гарнізон і займає клітину — одним SaveChanges.</summary>
    [Fact]
    public async Task Handle_ShouldCreateEveryAggregateInOneTransaction()
    {
        await Handler().Handle(Create(), CancellationToken.None);

        await _players.Received(1).AddAsync(Arg.Any<Player>(), Arg.Any<CancellationToken>());
        await _villages.Received(1).AddAsync(Arg.Any<Village>(), Arg.Any<CancellationToken>());
        await _wallets.Received(1).AddAsync(Arg.Any<PlayerWallet>(), Arg.Any<CancellationToken>());
        await _garrisons.Received(1).AddAsync(Arg.Any<Garrison>(), Arg.Any<CancellationToken>());
        await _map.Received(1).AddAsync(Arg.Any<MapCell>(), Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Селище створюється повним: усі будівлі з конфіга, кожна 1 рівня.
    /// Недоступні ховає туман, але існують вони з першого дня.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCreateTheVillageWithEveryBuilding()
    {
        Village? created = null;
        await _villages.AddAsync(Arg.Do<Village>(v => created = v), Arg.Any<CancellationToken>());

        await Handler().Handle(Create(), CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(4, created!.Buildings.Count);
        Assert.All(created.Buildings, b => Assert.Equal(1, b.Level.Value));
    }

    /// <summary>Стартові ресурси нараховуються за конфігом.</summary>
    [Fact]
    public async Task Handle_ShouldGrantStartingResources()
    {
        Village? created = null;
        await _villages.AddAsync(Arg.Do<Village>(v => created = v), Arg.Any<CancellationToken>());

        await Handler().Handle(Create(), CancellationToken.None);

        Assert.Equal(500, created!.Resources.Single(r => r.ResourceType == "food").Amount);
        Assert.Equal(300, created.Resources.Single(r => r.ResourceType == "wood").Amount);
    }

    /// <summary>
    /// Email нормалізується: інакше «Player@a.com» і «player@A.COM»
    /// дали б два акаунти на одну адресу.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNormaliseTheEmail()
    {
        Player? created = null;
        await _players.AddAsync(Arg.Do<Player>(p => created = p), Arg.Any<CancellationToken>());

        await Handler().Handle(Create("  Player@Example.COM  "), CancellationToken.None);

        Assert.Equal("player@example.com", created!.Email);
    }

    /// <summary>Село ставиться в межах туману поточного рівня світу.</summary>
    [Fact]
    public async Task Handle_ShouldPlaceTheVillageWithinTheFog()
    {
        var config = Config();
        var geometry = new WorldGeometry(config.Map);

        Village? created = null;
        await _villages.AddAsync(Arg.Do<Village>(v => created = v), Arg.Any<CancellationToken>());

        await Handler().Handle(Create(), CancellationToken.None);

        Assert.True(geometry.IsWithinFog(created!.X, created.Y, serverLevel: 1),
            $"Село поставлено за межею туману: ({created.X},{created.Y}).");
    }

    /// <summary>Гарнізон прив'язаний до села й до того самого світу.</summary>
    [Fact]
    public async Task Handle_ShouldLinkTheGarrisonToTheVillage()
    {
        Village? village = null;
        Garrison? garrison = null;

        await _villages.AddAsync(Arg.Do<Village>(v => village = v), Arg.Any<CancellationToken>());
        await _garrisons.AddAsync(Arg.Do<Garrison>(g => garrison = g), Arg.Any<CancellationToken>());

        await Handler().Handle(Create(), CancellationToken.None);

        Assert.Equal(village!.Id, garrison!.VillageId);
        Assert.Equal(village.ServerId, garrison.ServerId);
    }

    /// <summary>Гравець прив'язаний до щойно створеного акаунта, а світ — з конфіга.</summary>
    [Fact]
    public async Task Handle_ShouldBindThePlayerToTheNewAccount_OnTheDefaultServer()
    {
        Player? created = null;
        await _players.AddAsync(Arg.Do<Player>(p => created = p), Arg.Any<CancellationToken>());

        await Handler().Handle(Create(), CancellationToken.None);

        await _accounts.Received(1).CreateAsync("Serhiy", "Player@Example.COM", "Password123", Arg.Any<CancellationToken>());
        Assert.Equal(UserId, created!.UserId);
        _serverContext.Received(1).UseServer(1);
    }

    /// <summary>Акаунт і світ гравця — одна транзакція: спершу акаунт, наприкінці коміт.</summary>
    [Fact]
    public async Task Handle_ShouldCreateTheAccountAndThePlayer_InOneTransaction()
    {
        await Handler().Handle(Create(), CancellationToken.None);

        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _accounts.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _unitOfWork.CommitTransactionAsync(Arg.Any<CancellationToken>());
        });
    }

    /// <summary>Відмова Identity (слабкий пароль, зайнятий email) — відкат, жодного гравця.</summary>
    [Fact]
    public async Task Handle_ShouldRollBack_WhenTheAccountIsRejected()
    {
        var handler = Handler();
        _accounts.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new RequirementNotMetException(RefusalReasons.AuthRegistrationRejected,
                "Registration failed: DuplicateEmail", "DuplicateEmail"));

        await Assert.ThrowsAsync<RequirementNotMetException>(() => handler.Handle(Create(), CancellationToken.None));

        await _players.DidNotReceive().AddAsync(Arg.Any<Player>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Пароль не просочується в лог через ToString команди.</summary>
    [Fact]
    public void Command_ShouldNotPrintThePassword()
        => Assert.DoesNotContain("Password123", Create().ToString());

    /// <summary>Клітина займається під те саме село, що створене.</summary>
    [Fact]
    public async Task Handle_ShouldOccupyTheCellForTheNewVillage()
    {
        Village? village = null;
        MapCell? cell = null;

        await _villages.AddAsync(Arg.Do<Village>(v => village = v), Arg.Any<CancellationToken>());
        await _map.AddAsync(Arg.Do<MapCell>(c => cell = c), Arg.Any<CancellationToken>());

        await Handler().Handle(Create(), CancellationToken.None);

        Assert.Equal(MapOccupantType.Village, cell!.OccupantType);
        Assert.Equal(village!.Id, cell.OccupantId);
        Assert.Equal(village.X, cell.X);
        Assert.Equal(village.Y, cell.Y);
    }
}
