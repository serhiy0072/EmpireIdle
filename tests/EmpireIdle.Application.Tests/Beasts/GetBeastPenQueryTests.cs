using EmpireIdle.Application.Beasts.Queries;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Beasts;

/// <summary>Звіринець для екрана: місця, звірі з рівнями, шанси й гарантія (GDD §5.10).</summary>
public class GetBeastPenQueryTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IBeastPenRepository _pens = Substitute.For<IBeastPenRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly GameCatalog _catalog = new GameConfigBuilder().WithBeasts().BuildCatalog();

    private GetBeastPenQueryHandler Handler()
        => new(_pens, _villages, new BeastTaming(_catalog), new BeastProgression(_catalog),
            new VillageCapacities(_catalog), new VillageStatus(_catalog), _catalog);

    private void GivenVillageWithPen()
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", [], 0, 0);
        village.AddBuilding(_catalog.MainBuildingKey, _catalog.Buildings, Now);
        village.AddBuilding(TestKeys.BeastPen, _catalog.Buildings, Now);

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
    }

    /// <summary>Гравець ще нікого не приручав: порожній звіринець, а не 404, і шанси вже видно.</summary>
    [Fact]
    public async Task Handle_ShouldShowAnEmptyPen_BeforeTheFirstTaming()
    {
        GivenVillageWithPen();

        var view = await Handler().Handle(new GetBeastPenQuery(PlayerId), CancellationToken.None);

        Assert.Equal(1, view.Capacity);
        Assert.Empty(view.Beasts);
        var taming = Assert.Single(view.Taming);
        Assert.Equal((TestKeys.Beast, TestKeys.BeastMonster, 0, 10),
            (taming.BeastKey, taming.MonsterKey, taming.Misses, taming.PityWins));
        Assert.Equal(0.21, taming.Chance, precision: 10);
    }

    [Fact]
    public async Task Handle_ShouldShowTamedBeastsWithLevelsAndMisses()
    {
        GivenVillageWithPen();

        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: false, pityWins: 10, capacity: 1, maxRank: 5, Now);
        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);

        var view = await Handler().Handle(new GetBeastPenQuery(PlayerId), CancellationToken.None);

        var beast = Assert.Single(view.Beasts);
        Assert.Equal((TestKeys.Beast, 1, 1, 0, 100, 10),
            (beast.BeastKey, beast.Rank, beast.Level, beast.Experience, beast.ExperienceToNext, beast.MaxLevel));
        Assert.Equal(1, view.Taming.Single().Misses);
    }

    /// <summary>
    /// Бонус пасивки — сума кроків рівня в double: 0.15 + 0.01·2 дає 0.16999999999999998.
    /// Клієнт отримує чисте число, а не хвіст, який вилізе в інтерфейс.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnAClean_PassiveBonus()
    {
        GivenVillageWithPen();

        var progression = new BeastProgression(_catalog);
        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);
        pen.Feed(TestKeys.Beast, progression.ExperienceToNext(1) + progression.ExperienceToNext(2),
            progression.ExperienceToNext, levelsPerRank: 10, Now);
        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);

        var view = await Handler().Handle(new GetBeastPenQuery(PlayerId), CancellationToken.None);

        var beast = Assert.Single(view.Beasts);
        Assert.Equal(3, beast.Level);
        Assert.Equal(0.17, beast.Passive.Bonus);
    }
}
