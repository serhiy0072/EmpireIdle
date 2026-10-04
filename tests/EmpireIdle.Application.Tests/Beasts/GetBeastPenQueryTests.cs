using EmpireIdle.Application.Beasts.Queries;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Beasts;

/// <summary>Звіринець для екрана: місця, звірі, шанси й гарантія (GDD §5.10).</summary>
public class GetBeastPenQueryTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IBeastPenRepository _pens = Substitute.For<IBeastPenRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();

    private static GameCatalog Catalog()
    {
        var config = new GameConfigBuilder().WithBuildings("beastpen").Build();

        config.Buildings.Single(b => b.Key == "beastpen").BeastCapacityPerLevel = 1;
        config.Monsters = [new MonsterConfig { Key = "wolves", DisplayName = "Вовки", Units = [], Rewards = [] }];
        config.Beasts = new BeastsConfig
        {
            TameChancePerPenLevel = 0.01,
            MaxTameChanceMultiplier = 2,
            PityWins = 10,
            MaxRank = 5,
            Types = [new BeastConfig { Key = "wolf", DisplayName = "Вовк", MonsterKey = "wolves", TameChance = 0.2 }]
        };

        return new GameCatalog(config);
    }

    private GetBeastPenQueryHandler Handler(GameCatalog catalog)
        => new(_pens, _villages, new BeastTaming(catalog), new VillageCapacities(catalog), new VillageStatus(catalog), catalog);

    private Village GivenVillageWithPen(GameCatalog catalog)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", [], 0, 0);
        village.AddBuilding(catalog.MainBuildingKey, catalog.Buildings, Now);
        village.AddBuilding("beastpen", catalog.Buildings, Now);

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        return village;
    }

    /// <summary>Гравець ще нікого не приручав: порожній звіринець, а не 404, і шанси вже видно.</summary>
    [Fact]
    public async Task Handle_ShouldShowAnEmptyPen_BeforeTheFirstTaming()
    {
        var catalog = Catalog();
        GivenVillageWithPen(catalog);

        var view = await Handler(catalog).Handle(new GetBeastPenQuery(PlayerId), CancellationToken.None);

        Assert.Equal(1, view.Capacity);
        Assert.Empty(view.Beasts);
        var taming = Assert.Single(view.Taming);
        Assert.Equal(("wolf", "wolves", 0, 10), (taming.BeastKey, taming.MonsterKey, taming.Misses, taming.PityWins));
        Assert.Equal(0.21, taming.Chance, precision: 10);
    }

    [Fact]
    public async Task Handle_ShouldShowTamedBeastsAndMisses()
    {
        var catalog = Catalog();
        GivenVillageWithPen(catalog);

        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming("wolf", rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);
        pen.ResolveTaming("wolf", rolled: false, pityWins: 10, capacity: 1, maxRank: 5, Now);
        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);

        var view = await Handler(catalog).Handle(new GetBeastPenQuery(PlayerId), CancellationToken.None);

        Assert.Equal(("wolf", 1), Assert.Single(view.Beasts.Select(b => (b.BeastKey, b.Rank))));
        Assert.Equal(1, view.Taming.Single().Misses);
    }
}
