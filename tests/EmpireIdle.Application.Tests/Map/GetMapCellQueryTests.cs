using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Map.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Map;

/// <summary>Клітина цілком збирається в запиті: межі карти, місцевість поточного світу й окупант.</summary>
public class GetMapCellQueryTests
{
    private readonly IMapRepository _map = Substitute.For<IMapRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();

    private static readonly GameConfig Config = new GameConfigBuilder().WithBuildings().WithMap().WithBeasts().Build();

    public GetMapCellQueryTests()
    {
        _serverContext.ServerId.Returns(1);
        _map.GetAreaAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MapCell>());
    }

    private readonly IMonsterRepository _monsters = Substitute.For<IMonsterRepository>();

    private GetMapCellQueryHandler Handler()
    {
        var catalog = new GameCatalog(Config);

        return new GetMapCellQueryHandler(_map, _monsters, Substitute.For<IVillageRepository>(),
            new MonsterArmyBuilder(catalog), new FakeTimeProvider(), Substitute.For<IClanStructureRepository>(),
            Substitute.For<IClanRepository>(), _serverContext, new TerrainGenerator(Config.Map));
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ACellOutsideTheMap()
        => await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new GetMapCellQuery(-1, 0), CancellationToken.None));

    [Fact]
    public async Task Handle_ShouldDescribeTheTerrainOfTheCurrentWorld_ForAnEmptyCell()
    {
        var expected = new TerrainGenerator(Config.Map).GetTerrain(1, 3, 4);

        var cell = await Handler().Handle(new GetMapCellQuery(3, 4), CancellationToken.None);

        Assert.Equal(expected.Type, cell.TerrainType);
        Assert.Equal(expected.MoveCost, cell.MoveCost);
        Assert.Null(cell.Occupant);
        await _map.Received(1).GetAreaAsync(1, 3, 4, 3, 4, Arg.Any<CancellationToken>());
    }

    /// <summary>Тип монстра — окремим полем: за ним клієнт вирішує, чи пропонувати приручення (GDD §5.10).</summary>
    [Fact]
    public async Task Handle_ShouldNameTheMonsterType()
    {
        var monster = new Monster(Guid.NewGuid(), 1, TestKeys.BeastMonster, 2, 3, 4, DateTime.UtcNow);
        _map.GetAreaAsync(1, 3, 4, 3, 4, Arg.Any<CancellationToken>())
            .Returns(new List<MapCell> { new(Guid.NewGuid(), 1, 3, 4, MapOccupantType.Monster, monster.Id) });
        _monsters.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([monster]);

        var cell = await Handler().Handle(new GetMapCellQuery(3, 4), CancellationToken.None);

        Assert.Equal(TestKeys.BeastMonster, cell.Occupant?.MonsterType);
    }
}
