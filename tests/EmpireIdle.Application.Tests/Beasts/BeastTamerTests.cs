using EmpireIdle.Application.Beasts.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Beasts;

/// <summary>Кидок приручення бере той самий шанс, що показує екран, — разом із технологіями звіроловства.</summary>
public class BeastTamerTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    // Між шансом без технології (0.21) і з нею (0.315)
    private const double Roll = 0.3;

    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly GameCatalog _catalog = new GameConfigBuilder().WithBeasts().BuildCatalog();

    private Task<string?> Tame()
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", [], 0, 0);
        village.AddBuilding(_catalog.MainBuildingKey, _catalog.Buildings, Now);
        village.AddBuilding(TestKeys.BeastPen, _catalog.Buildings, Now);

        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(Roll);

        var tamer = new BeastTamer(Substitute.For<IBeastPenRepository>(), Substitute.For<IMonsterRepository>(), random,
            new BeastTaming(_catalog), new VillageCapacities(_catalog), new VillageStatus(_catalog), _catalog,
            TestTechnologies.For(TestTechnologies.TreeWith(TechnologyStat.TameChance, 0.5), _research));

        return tamer.TameAsync(village, TestKeys.BeastMonster, Now, CancellationToken.None);
    }

    [Fact]
    public async Task TameAsync_ShouldMiss_WithoutTheBeastcraftTechnology()
        => Assert.Null(await Tame());

    /// <summary>Той самий кидок влучає, коли звіроловство підняло шанс понад нього.</summary>
    [Fact]
    public async Task TameAsync_ShouldTame_WhenTheBeastcraftTechnologyLiftsTheChanceAboveTheRoll()
    {
        _research.Researched(PlayerId, "attack_infantry", 1);

        Assert.Equal(TestKeys.Beast, await Tame());
    }
}
