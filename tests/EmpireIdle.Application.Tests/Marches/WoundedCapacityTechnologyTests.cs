using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Технології лазарету (рішення 28.10.2026) розширюють Госпіталь: 1 рівень — 100 місць,
/// +50% — 150; уже поранені займають місця як і раніше.
/// </summary>
public class WoundedCapacityTechnologyTests
{
    private static readonly DateTime Now = Entities.Now;

    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly GameCatalog _catalog;

    public WoundedCapacityTechnologyTests()
    {
        var config = new GameConfigBuilder().WithMap().WithBuildings("hospital").Build();
        config.Buildings.Single(b => b.Key == "hospital").WoundedCapacityPerLevel = 100;

        _catalog = new GameCatalog(config);
    }

    private MarchLogistics Logistics()
    {
        var config = _catalog.Config;

        return new MarchLogistics(Substitute.For<IVillageRepository>(), Substitute.For<IHeroRepository>(), _catalog,
            new MarchCalculator(new TerrainGenerator(config.Map), _catalog), new HeroProgression(config.HeroSettings),
            new HeroStats(new HeroProgression(config.HeroSettings), _catalog),
            TestTechnologies.For(TestTechnologies.TreeWith(TechnologyStat.WoundedCapacity, 0.5), _research),
            NullLogger<MarchLogistics>.Instance);
    }

    private (Village Village, Garrison Garrison) Given()
    {
        var village = new Village(Guid.NewGuid(), Guid.NewGuid(), "Test", [], 0, 0);
        village.AddBuilding(TestKeys.Townhall, _catalog.Buildings, Now);
        village.AddBuilding("hospital", _catalog.Buildings, Now);

        return (village, new Garrison(Guid.NewGuid(), village.Id, serverId: 1));
    }

    [Fact]
    public async Task WoundedCapacity_ShouldGrow_WithTheInfirmaryTechnology()
    {
        var (village, garrison) = Given();

        Assert.Equal(100, await Logistics().WoundedCapacityAsync(village, garrison, CancellationToken.None));

        _research.Researched(village.PlayerId, "attack_infantry", 1);

        Assert.Equal(150, await Logistics().WoundedCapacityAsync(village, garrison, CancellationToken.None));
    }
}
