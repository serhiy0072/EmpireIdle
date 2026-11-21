using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Research;

/// <summary>
/// Технологія виробітку піднімає ставку лише від моменту завершення: накопичене до нього
/// фіксується за старою ставкою, а не перераховується заднім числом. Ферма — 10 їжі за хвилину,
/// attack_infantry тут ще й дає +50% виробітку їжі.
/// </summary>
public class ResearchSettlementTests
{
    private static readonly DateTime Now = Entities.Now;

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly GameCatalog _catalog;

    public ResearchSettlementTests()
    {
        var config = new GameConfigBuilder()
            // Одне кільце з нейтральним множником: тест не про геометрію
            .WithMap(m => m.Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.5], RingMultipliers = [1.0, 1.0], RingsAtFirstLevel = 1.0, FogMinShare = 1.0, FogMaxShare = 1.0
            })
            .WithBuildings(TestKeys.Farm, TestKeys.Warehouse)
            .WithTechnologies(t => t.Personal.Nodes.Single(n => n.Key == "attack_infantry").Effects
                .Add(new TechnologyEffectConfig { Stat = TechnologyStat.Production, Target = TestKeys.Food, PerLevel = 0.5 }))
            .Build();

        var farm = config.Buildings.Single(b => b.Key == TestKeys.Farm);
        farm.ProducesResource = TestKeys.Food;
        farm.BaseProductionPerMinute = 10;
        farm.BaseStorage = 1_000_000;

        _catalog = new GameCatalog(config);
        _servers.GetLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(1);
    }

    /// <summary>Село з фермою, що виробляє годину, і дослідження, яке дозріває зараз.</summary>
    private (Building Farm, PlayerResearch Research) Given(string node)
    {
        var playerId = Guid.NewGuid();
        var village = new Village(Guid.NewGuid(), playerId, "Test", [TestKeys.Food], 0, 0);
        village.AddBuilding(TestKeys.Townhall, _catalog.Buildings, Now.AddHours(-1));
        village.AddBuilding(TestKeys.Farm, _catalog.Buildings, Now.AddHours(-1));
        _villages.GetByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(village);

        var research = new PlayerResearch(Guid.NewGuid(), playerId, 1, Now);
        research.Start(new TechnologyKey(1, node), TimeSpan.FromMinutes(1), Now.AddMinutes(-1));
        _research.GetByIdAsync(research.Id, Arg.Any<CancellationToken>()).Returns(research);
        _research.GetByPlayerReadOnlyAsync(playerId, Arg.Any<CancellationToken>()).Returns(research);

        return (village.Buildings.Single(b => b.Type == TestKeys.Farm), research);
    }

    private Task Complete(PlayerResearch research)
        => new CompleteResearchCommandHandler(_research, Substitute.For<IUnitOfWork>(), new FakeTimeProvider(Now),
                TestTechnologies.Settlement(_catalog, _villages, _research, _servers),
                NullLogger<CompleteResearchCommandHandler>.Instance)
            .Handle(new CompleteResearchCommand(research.Id), CancellationToken.None);

    [Fact]
    public async Task Complete_ShouldBankTheHourAtTheOldRate_BeforeTheProductionTechnologyApplies()
    {
        var (farm, research) = Given("attack_infantry");

        await Complete(research);

        // 60 хв × 10/хв — без +50%: вони діють лише від завершення
        Assert.Equal(600, farm.AccruedAmount);
        Assert.Equal(Now, farm.LastAccruedAt);
        Assert.Equal(1, research.LevelOf(new TechnologyKey(1, "attack_infantry")));
    }

    /// <summary>Технологія, що не чіпає виробіток, село не матеріалізує.</summary>
    [Fact]
    public async Task Complete_ShouldLeaveTheVillageAlone_ForANonProductionTechnology()
    {
        var (farm, research) = Given("command_infantry");

        await Complete(research);

        Assert.Equal(0, farm.AccruedAmount);
        Assert.Equal(Now.AddHours(-1), farm.LastAccruedAt);
    }
}
