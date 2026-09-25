using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Map.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Map;

public class GetMapAreaQueryTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMapRepository _map = Substitute.For<IMapRepository>();
    private readonly IMonsterRepository _monsters = Substitute.For<IMonsterRepository>();
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();

    private GetMapAreaQueryHandler Handler() => new(_map, _monsters, _structures, _clans);

    /// <summary>Споруда несе свій клан, тег і момент готовності — клієнт малює радіус і відрізняє свою територію.</summary>
    [Fact]
    public async Task Handle_ShouldAttachClanAndReadiness_ToStructureCells()
    {
        var clanId = Guid.NewGuid();
        var structure = new ClanStructure(Guid.NewGuid(), 1, clanId, 1, 1, Guid.NewGuid(), Guid.NewGuid(),
            TimeSpan.FromHours(2), Now);

        _map.GetAreaAsync(1, -2, -2, 2, 2, Arg.Any<CancellationToken>()).Returns(
            [new MapCell(Guid.NewGuid(), 1, 1, 1, MapOccupantType.ClanStructure, structure.Id)]);
        _structures.GetInAreaAsync(-2, -2, 2, 2, Arg.Any<CancellationToken>()).Returns([structure]);
        _clans.GetCardsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(
            new Dictionary<Guid, ClanCard> { [clanId] = new(clanId, "Northern Watch", "NW", "", ClanJoinPolicy.Open, 3, Now) });

        var occupant = Assert.Single(await Handler().Handle(new GetMapAreaQuery(1, 0, 0, 2), CancellationToken.None));

        Assert.Equal(clanId, occupant.ClanId);
        Assert.Equal("NW", occupant.ClanTag);
        Assert.Equal(Now.AddHours(2), occupant.ReadyAt);
    }

    /// <summary>Монстри ділянки збагачуються типом і рівнем одним запитом за всіма ідентифікаторами.</summary>
    [Fact]
    public async Task Handle_ShouldAttachMonsterTypeAndLevel_ToMonsterCells()
    {
        var wolfId = Guid.NewGuid();
        var villageId = Guid.NewGuid();

        _map.GetAreaAsync(1, -2, -2, 2, 2, Arg.Any<CancellationToken>()).Returns(
        [
            new MapCell(Guid.NewGuid(), 1, 0, 0, MapOccupantType.Monster, wolfId),
            new MapCell(Guid.NewGuid(), 1, 1, 1, MapOccupantType.Village, villageId)
        ]);

        _monsters.GetByIdsAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == wolfId), Arg.Any<CancellationToken>())
            .Returns([new Monster(wolfId, 1, "wolf", 3, 0, 0, Now)]);

        var result = await Handler().Handle(new GetMapAreaQuery(1, 0, 0, 2), CancellationToken.None);

        var wolf = Assert.Single(result, o => o.OccupantType == MapOccupantType.Monster);
        Assert.Equal("wolf", wolf.MonsterType);
        Assert.Equal(3, wolf.MonsterLevel);

        var village = Assert.Single(result, o => o.OccupantType == MapOccupantType.Village);
        Assert.Null(village.MonsterType);
        Assert.Null(village.MonsterLevel);
    }

    /// <summary>Без монстрів на ділянці репозиторій монстрів не смикається.</summary>
    [Fact]
    public async Task Handle_ShouldNotQueryMonsters_WhenTheAreaHasNone()
    {
        _map.GetAreaAsync(1, -1, -1, 1, 1, Arg.Any<CancellationToken>()).Returns(
        [
            new MapCell(Guid.NewGuid(), 1, 0, 0, MapOccupantType.Village, Guid.NewGuid())
        ]);

        var result = await Handler().Handle(new GetMapAreaQuery(1, 0, 0, 1), CancellationToken.None);

        Assert.Single(result);
        await _monsters.DidNotReceive().GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Монстр, убитий між двома запитами, лишає клітину без типу — не помилку.</summary>
    [Fact]
    public async Task Handle_ShouldLeaveTypeEmpty_WhenTheMonsterIsGone()
    {
        _map.GetAreaAsync(1, -1, -1, 1, 1, Arg.Any<CancellationToken>()).Returns(
        [
            new MapCell(Guid.NewGuid(), 1, 0, 0, MapOccupantType.Monster, Guid.NewGuid())
        ]);
        _monsters.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await Handler().Handle(new GetMapAreaQuery(1, 0, 0, 1), CancellationToken.None);

        var cell = Assert.Single(result);
        Assert.Equal(MapOccupantType.Monster, cell.OccupantType);
        Assert.Null(cell.MonsterType);
    }

    [Fact]
    public async Task Handle_ShouldReject_ARadiusBeyondTheCap()
        => await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Handler().Handle(new GetMapAreaQuery(1, 0, 0, 26), CancellationToken.None));
}
