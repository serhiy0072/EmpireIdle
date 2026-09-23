using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Dungeons;

/// <summary>
/// Відмови на старті забігу, до яких гравець доходить чесною грою.
/// Кожна несе причину з параметрами: клієнт показує за нею власний текст,
/// а не англійський Detail сервера.
/// </summary>
public class StartDungeonRunCommandTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly GameConfig _config = new GameConfigBuilder().WithDungeons().Build();
    private readonly IDungeonRepository _dungeons = Substitute.For<IDungeonRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();

    private StartDungeonRunCommandHandler Handler(Village village, int clearedLevel = 0)
    {
        var catalog = new GameCatalog(_config);
        var progression = new HeroProgression(_config.HeroSettings);

        _villages.GetByPlayerIdReadOnlyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(village);
        _dungeons.GetClearedLevelsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int> { [TestKeys.Dungeon] = clearedLevel });
        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        var serverContext = Substitute.For<IServerContext>();
        serverContext.ServerId.Returns(1);

        return new StartDungeonRunCommandHandler(
            _dungeons,
            _villages,
            new DungeonTeamFactory(_heroes, _inventory, catalog, new HeroStats(progression, catalog), progression),
            new BattleBuilder(_config.Dungeons),
            catalog,
            Substitute.For<IRandomSource>(),
            serverContext,
            Substitute.For<IUnitOfWork>(),
            new FakeTimeProvider(Entities.Now),
            NullLogger<StartDungeonRunCommandHandler>.Instance);
    }

    private Hero HeroOnDuty()
    {
        var hero = Entities.Hero(TestKeys.CommonHero, PlayerId);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        return hero;
    }

    private static StartDungeonRunCommand Start(Hero hero, int level = 1)
        => new(PlayerId, TestKeys.Dungeon, level, [hero.Id]);

    [Fact]
    public async Task Handle_BelowTheTownHallLevel_ShouldExplainWhichHallItNeeds()
    {
        var handler = Handler(Entities.Village(PlayerId));

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            handler.Handle(Start(HeroOnDuty()), CancellationToken.None));

        var dungeon = _config.Dungeons.Dungeons.Single();
        Assert.Equal(RefusalReasons.DungeonTownHallRequired.Key, refusal.Reason);
        Assert.Equal(dungeon.DisplayName, refusal.Args["dungeon"]);
        Assert.Equal(dungeon.RequiresMainBuildingLevel, refusal.Args["level"]);
    }

    [Fact]
    public async Task Handle_ALevelNotYetOpened_ShouldNameTheLevelToClearFirst()
    {
        var handler = Handler(Entities.VillageWithTownhall(townhallLevel: 5), clearedLevel: 0);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            handler.Handle(Start(HeroOnDuty(), level: 2), CancellationToken.None));

        Assert.Equal(RefusalReasons.DungeonLevelLocked.Key, refusal.Reason);
        Assert.Equal(2, refusal.Args["level"]);
        Assert.Equal(1, refusal.Args["previous"]);
    }

    /// <summary>Друга вкладка вже почала забіг — це відмова з поясненням, а не «Це вже існує».</summary>
    [Fact]
    public async Task Handle_WithARunAlreadyGoing_ShouldSaySo()
    {
        var handler = Handler(Entities.VillageWithTownhall(townhallLevel: 5));
        _dungeons.GetActiveRunAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(new DungeonRun(Guid.NewGuid(), PlayerId, 1, TestKeys.Dungeon, 1, "{}", Entities.Now));

        var refusal = await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            handler.Handle(Start(HeroOnDuty()), CancellationToken.None));

        Assert.Equal(RefusalReasons.DungeonRunInProgress.Key, refusal.Reason);
    }

    /// <summary>Героя поранили між вибором складу й стартом: гравець має побачити, кого саме.</summary>
    [Fact]
    public async Task Handle_WithAWoundedHero_ShouldNameTheHero()
    {
        var handler = Handler(Entities.VillageWithTownhall(townhallLevel: 5));
        var hero = HeroOnDuty();
        hero.Wound(Entities.Now);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            handler.Handle(Start(hero), CancellationToken.None));

        Assert.Equal(RefusalReasons.DungeonHeroWounded.Key, refusal.Reason);
        Assert.Equal(new GameCatalog(_config).FindHero(TestKeys.CommonHero)!.DisplayName, refusal.Args["hero"]);
    }
}
