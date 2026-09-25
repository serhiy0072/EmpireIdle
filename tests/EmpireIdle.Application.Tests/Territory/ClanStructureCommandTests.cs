using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.Commands;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Territory;

/// <summary>
/// Закладення й знесення кланових споруд: слоти, очки вкладу, клітина й права.
/// </summary>
public class ClanStructureCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private const long Cost = 1000;
    private const string SlotQuest = "clan_hunt";

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly IClanQuestRepository _clanQuests = Substitute.For<IClanQuestRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IMapRepository _map = Substitute.For<IMapRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Guid _leader = Guid.NewGuid();
    private readonly Clan _clan;

    public ClanStructureCommandTests()
    {
        _clan = new Clan(Guid.NewGuid(), 1, "Northern Watch", "NW", _leader, Now);
        _clan.EarnPoints(Cost * 3, null, Now);

        _serverContext.ServerId.Returns(1);
        _servers.GetLevelAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _clans.GetByMemberAsync(_leader, Arg.Any<CancellationToken>()).Returns(_clan);
        _structures.GetByClanAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns([]);
        _clanQuests.GetCompletedKeysAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns([]);
        _heroes.GetByGarrisonAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private static GameConfig Config()
    {
        var config = new GameConfig
        {
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
            Map = new MapConfig
            {
                Width = 100,
                Height = 100,
                TerrainSeed = 1,
                Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }]
            },
            Quests =
            [
                new QuestConfig
                {
                    Key = SlotQuest,
                    DisplayName = "Clan hunt",
                    Scope = QuestScope.Clan,
                    Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 100 }]
                }
            ]
        };

        config.Clan.Territory = new ClanTerritoryConfig
        {
            Enabled = true,
            StartingSlots = 1,
            MaxStructures = 10,
            SlotUnlocks = [new ClanSlotUnlockConfig { QuestKey = SlotQuest }],
            StructureCostPoints = Cost,
            BuildMinutes = 60
        };

        return config;
    }

    private PlaceClanStructureCommandHandler Place()
    {
        var config = Config();
        var catalog = new GameCatalog(config);

        return new PlaceClanStructureCommandHandler(_clans, _structures, _clanQuests, _garrisons, _map, _servers,
            _serverContext, new WorldGeometry(config.Map), new TerrainGenerator(config.Map), new ClanTerritoryRules(catalog),
            _unitOfWork, new FakeTimeProvider(Now), NullLogger<PlaceClanStructureCommandHandler>.Instance);
    }

    private DemolishClanStructureCommandHandler Demolish()
    {
        var config = Config();
        var catalog = new GameCatalog(config);

        var returner = new ReinforcementReturner(_garrisons, _villages, _structures, _marches, _heroes,
            new MarchCalculator(new TerrainGenerator(config.Map), catalog), catalog,
            new HeroProgression(config.HeroSettings), NullLogger<ReinforcementReturner>.Instance);

        var remover = new ClanStructureRemover(_structures, _garrisons, _map, returner,
            NullLogger<ClanStructureRemover>.Instance);

        return new DemolishClanStructureCommandHandler(_clans, _structures, remover, _unitOfWork,
            new FakeTimeProvider(Now), NullLogger<DemolishClanStructureCommandHandler>.Instance);
    }

    private ClanStructure ExistingStructure(Guid clanId)
        => new(Guid.NewGuid(), 1, clanId, 52, 52, Guid.NewGuid(), _leader, TimeSpan.FromHours(1), Now);

    [Fact]
    public async Task Place_ShouldPayAndOccupyTheCell()
    {
        var id = await Place().Handle(new PlaceClanStructureCommand(_leader, 52, 52), CancellationToken.None);

        Assert.Equal(Cost * 2, _clan.ContributionPoints);
        await _structures.Received(1).AddAsync(
            Arg.Is<ClanStructure>(s => s.Id == id && s.ClanId == _clan.Id && s.CompletesAt == Now.AddHours(1)),
            Arg.Any<CancellationToken>());
        await _garrisons.Received(1).AddAsync(
            Arg.Is<Garrison>(g => g.HostKind == GarrisonHost.ClanStructure && g.HostId == id), Arg.Any<CancellationToken>());
        await _map.Received(1).AddAsync(
            Arg.Is<MapCell>(c => c.OccupantType == MapOccupantType.ClanStructure && c.OccupantId == id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Place_ShouldRefuse_WhenEverySlotIsTaken()
    {
        _structures.GetByClanAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns([ExistingStructure(_clan.Id)]);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Place().Handle(new PlaceClanStructureCommand(_leader, 55, 55), CancellationToken.None));

        Assert.Equal(RefusalReasons.TerritoryNoFreeSlot.Key, refusal.Reason);
        Assert.Equal(Cost * 3, _clan.ContributionPoints);
    }

    [Fact]
    public async Task Place_ShouldUseTheSlotOpenedByAClanQuest()
    {
        _structures.GetByClanAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns([ExistingStructure(_clan.Id)]);
        _clanQuests.GetCompletedKeysAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns([SlotQuest]);

        await Place().Handle(new PlaceClanStructureCommand(_leader, 55, 55), CancellationToken.None);

        await _structures.Received(1).AddAsync(Arg.Any<ClanStructure>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Place_ShouldRefuse_WhenTheClanLacksPoints()
    {
        _clan.SpendPoints(_clan.ContributionPoints - 10, Now);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Place().Handle(new PlaceClanStructureCommand(_leader, 52, 52), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanNotEnoughPoints.Key, refusal.Reason);
        await _structures.DidNotReceive().AddAsync(Arg.Any<ClanStructure>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Place_ShouldRefuse_AnOccupiedCell()
    {
        _map.IsOccupiedAsync(1, 52, 52, Arg.Any<CancellationToken>()).Returns(true);

        var refusal = await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            Place().Handle(new PlaceClanStructureCommand(_leader, 52, 52), CancellationToken.None));

        Assert.Equal(RefusalReasons.TerritoryCellTaken.Key, refusal.Reason);
    }

    /// <summary>За туманом світу ще нікого немає — там не бувати й споруді.</summary>
    [Fact]
    public async Task Place_ShouldRefuse_ACellBeyondTheSettledRegion()
    {
        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Place().Handle(new PlaceClanStructureCommand(_leader, 98, 98), CancellationToken.None));

        Assert.Equal(RefusalReasons.TerritoryCellUnfit.Key, refusal.Reason);
    }

    [Fact]
    public async Task Place_ShouldRefuse_AMemberWithoutTheRight()
    {
        var member = Guid.NewGuid();
        _clan.Join(member, 200, Now);
        _clans.GetByMemberAsync(member, Arg.Any<CancellationToken>()).Returns(_clan);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Place().Handle(new PlaceClanStructureCommand(member, 52, 52), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanNoPermission.Key, refusal.Reason);
    }

    [Fact]
    public async Task Demolish_ShouldRemoveTheStructureItsGarrisonAndCell()
    {
        var structure = ExistingStructure(_clan.Id);
        var garrison = Garrison.ForStructure(structure.GarrisonId, structure.Id, 1);
        var cell = new MapCell(Guid.NewGuid(), 1, structure.X, structure.Y, MapOccupantType.ClanStructure, structure.Id);

        _structures.GetByIdAsync(structure.Id, Arg.Any<CancellationToken>()).Returns(structure);
        _garrisons.GetByIdAsync(structure.GarrisonId, Arg.Any<CancellationToken>()).Returns(garrison);
        _map.GetByOccupantAsync(MapOccupantType.ClanStructure, structure.Id, Arg.Any<CancellationToken>()).Returns(cell);

        await Demolish().Handle(new DemolishClanStructureCommand(_leader, structure.Id), CancellationToken.None);

        _structures.Received(1).Remove(structure);
        _garrisons.Received(1).Remove(garrison);
        _map.Received(1).Remove(cell);
    }

    /// <summary>Чужу споруду не зносять — її руйнують у бою.</summary>
    [Fact]
    public async Task Demolish_ShouldNotFindAnotherClansStructure()
    {
        var foreign = ExistingStructure(Guid.NewGuid());
        _structures.GetByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Demolish().Handle(new DemolishClanStructureCommand(_leader, foreign.Id), CancellationToken.None));

        _structures.DidNotReceive().Remove(Arg.Any<ClanStructure>());
    }
}
