using EmpireIdle.Application.Heroes.Queries;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Огляд ростера для екрана героя (GDD §6.1): сила зі спорядженням і конвої — те, що гравець
/// бачить під портретом, рахує сервер, а не клієнт.
/// </summary>
public class GetHeroesOverviewQueryTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly List<Hero> _roster = [];
    private readonly List<EquipmentItem> _equipped = [];

    public GetHeroesOverviewQueryTests()
    {
        _heroes.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _roster.ToList());
        _heroes.GetAllShardsAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(new List<HeroShardProgress>());
        _inventory.GetEquippedByHeroesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => _equipped.ToList());
        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(TestKit.Entities.VillageWithTownhall(townhallLevel: 1));
    }

    private static GameConfig Config()
    {
        var config = HeroTestConfig.Create();
        config.HeroSettings.ConvoySize = 100;
        config.HeroSettings.ConvoysByLevel = [new ConvoyStepConfig { Level = 1, Convoys = 2 }];
        return config;
    }

    private GetHeroesOverviewQueryHandler Handler()
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var progression = new HeroProgression(config.HeroSettings);

        return new GetHeroesOverviewQueryHandler(
            _heroes, _inventory, new HeroStats(progression, catalog), progression,
            new HeroSkills(config.HeroSettings), new HeroConvoys(config.HeroSettings),
            new TrainingCampRules(config.HeroSettings.TrainingCamp), _villages, new VillageStatus(catalog),
            catalog, new FakeTimeProvider(Now));
    }

    /// <summary>
    /// Вдягнений артефакт додає свою силу до сили героя — так само, як у повній силі рейтингу:
    /// звичайний рівня 0 — (10 + 10) + (40 + 40) × 0.25.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCountEquippedGear_InTheHeroPower()
    {
        var bare = TestKit.Entities.Hero(HeroTestConfig.CommonHero, PlayerId);
        _roster.Add(bare);

        var bareView = Assert.Single((await Handler().Handle(new GetHeroesOverviewQuery(PlayerId), CancellationToken.None)).Heroes);

        var necklace = TestKit.Entities.Equipment(HeroTestConfig.Artifact, EquipmentSlot.Artifact, PlayerId);
        necklace.EquipTo(bare.Id, HeroTestConfig.NecklaceSlot, Now);
        _equipped.Add(necklace);

        var armedView = Assert.Single((await Handler().Handle(new GetHeroesOverviewQuery(PlayerId), CancellationToken.None)).Heroes);

        Assert.True(bareView.Power > 0);
        Assert.Equal(bareView.Power + 40, armedView.Power, 3);
    }

    /// <summary>
    /// Боронить село найсильніший кожного класу (рішення 15.10.2026), хоч і в марші: слабший воїн —
    /// ні, маг — так, бо він один свого класу.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldMarkTheStrongestHeroOfEachClass_AsADefender()
    {
        var strong = TestKit.Entities.Hero(HeroTestConfig.CommonHero, PlayerId, level: 5);
        strong.Deploy(Guid.NewGuid(), Now);
        var weak = TestKit.Entities.Hero(HeroTestConfig.CommonHero, PlayerId, level: 1);
        var mage = TestKit.Entities.Hero(HeroTestConfig.UniqueHero, PlayerId);
        _roster.AddRange([weak, strong, mage]);

        var views = (await Handler().Handle(new GetHeroesOverviewQuery(PlayerId), CancellationToken.None)).Heroes;

        Assert.True(views.Single(v => v.Id == strong.Id).IsDefender);
        Assert.False(views.Single(v => v.Id == weak.Id).IsDefender);
        Assert.True(views.Single(v => v.Id == mage.Id).IsDefender);
    }

    /// <summary>Конвої — за рівнем героя, місткість — конвої × розмір конвою.</summary>
    [Fact]
    public async Task Handle_ShouldReportConvoys_ConsistentWithCapacity()
    {
        _roster.Add(TestKit.Entities.Hero(HeroTestConfig.CommonHero, PlayerId));

        var view = Assert.Single((await Handler().Handle(new GetHeroesOverviewQuery(PlayerId), CancellationToken.None)).Heroes);

        Assert.Equal(2, view.Convoys);
        Assert.Equal(200, view.ConvoyCapacity);
    }

    /// <summary>
    /// Стати зараз і після підняття рівня — тією самою формулою, що й бій: гравець бачить,
    /// що дасть наступний рівень, ще до витрати досвіду.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldShowTheStats_NowAndAtTheNextLevel()
    {
        var hero = TestKit.Entities.Hero(HeroTestConfig.CommonHero, PlayerId);
        _roster.Add(hero);

        var view = Assert.Single((await Handler().Handle(new GetHeroesOverviewQuery(PlayerId), CancellationToken.None)).Heroes);

        var config = Config();
        var stats = new HeroStats(new HeroProgression(config.HeroSettings), new GameCatalog(config));
        var heroConfig = config.Heroes.Single(h => h.Key == hero.HeroKey);
        var next = stats.ComputeAt(hero, heroConfig, [], hero.Level + 1);

        Assert.Equal(Math.Round(stats.Compute(hero, heroConfig, [])["Attack"], 1), view.Stats["Attack"]);
        Assert.NotNull(view.NextLevelStats);
        Assert.Equal(Math.Round(next["Attack"], 1), view.NextLevelStats["Attack"]);
        Assert.True(view.NextLevelStats["Attack"] > view.Stats["Attack"]);
    }
}
