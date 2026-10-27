using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Tower.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Tower;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Tower;

/// <summary>Екран вежі й рейтинг сервера.</summary>
public class TowerQueriesTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly GameConfig _config;
    private readonly ITowerRepository _tower = Substitute.For<ITowerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();

    public TowerQueriesTests()
    {
        _config = new GameConfigBuilder().WithUnits().WithHeroes(h =>
        {
            h.RoleUnits = new() { ["warrior"] = TestKeys.Infantry, ["knight"] = TestKeys.Cavalry, ["archer"] = TestKeys.Archer, ["mage"] = TestKeys.Archer };
            h.ConvoySize = 100;
            h.ConvoysByLevel = [new ConvoyStepConfig { Level = 1, Convoys = 1 }];
        }).WithRealtimeBattle().Build();

        _config.Tower = new TowerConfig
        {
            RequiresMainBuildingLevel = 3,
            MaxFloor = 12,
            Anchors =
            [
                new TowerAnchorConfig { Floor = 1, HeroLevel = 1, UnitLevel = 1, StatMultiplier = 1 },
                new TowerAnchorConfig { Floor = 12, HeroLevel = 5, UnitLevel = 1, StatMultiplier = 2 }
            ],
            SetSteps = [new TowerSetStepConfig { FromFloor = 1, Multiplier = 1, DisplayName = "Без набору" }],
            Archetypes = [new TowerArchetypeConfig { Key = "duel", DisplayName = "Дуель", Classes = ["warrior", "mage"] }],
            BossEvery = 10,
            BossArchetype = "duel",
            BossLane = 1,
            HeroRarities = [new TowerRarityStepConfig { FromFloor = 1, Rarities = [Rarity.Common, Rarity.Unique] }],
            Milestones = [new TowerMilestoneConfig { Every = 1, Stages = [new TowerRewardStageConfig { Options = [[new RewardConfig { Type = "Gems", Amount = 20 }]] }] }]
        };

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall(townhallLevel: 3));
        _players.GetNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IReadOnlyCollection<Guid>>().ToDictionary(id => id, id => id == PlayerId ? "Я" : "Інший"));
    }

    private GetTowerQueryHandler Tower()
    {
        var catalog = new GameCatalog(_config);

        return new GetTowerQueryHandler(_tower, _villages, new TowerFloors(catalog), new TowerRewards(_config.Tower),
            TestEffects.Resolver(_effects, catalog: catalog), catalog, new FakeTimeProvider(Entities.Now),
            new LocaleTexts(_config, "uk"));
    }

    private static TowerProgress Progress(int floors, Guid? playerId = null)
    {
        var progress = new TowerProgress(Guid.NewGuid(), playerId ?? Guid.NewGuid(), serverId: 1);

        for (var floor = 1; floor <= floors; floor++)
            progress.Clear(floor, maxFloor: 12, Entities.Now.AddMinutes(floor));

        return progress;
    }

    /// <summary>Новачок: перший поверх зі складом ворога, стрічка з десяти поверхів, місця ще немає.</summary>
    [Fact]
    public async Task GetTower_ShouldShowTheFirstFloor_ToANewcomer()
    {
        var view = await Tower().Handle(new GetTowerQuery(PlayerId), CancellationToken.None);

        Assert.True(view.Unlocked);
        Assert.Equal((0, (int?)null), (view.HighestFloor, view.Rank));
        Assert.Equal(1, view.Next!.Number);
        Assert.Equal(["warrior", "mage"], view.Next.Enemies.Select(e => e.HeroClass));
        Assert.Equal("Без набору", view.Next.SetName);
        Assert.Equal(Enumerable.Range(1, 10), view.Upcoming.Select(f => f.Number));
        Assert.True(view.Upcoming[9].IsBoss);
        Assert.Equal(20, view.Next.Rewards.Single().Amount);
    }

    /// <summary>Біля вершини стрічка коротшає, а місце в рейтингу вже є.</summary>
    [Fact]
    public async Task GetTower_ShouldStopTheStripAtTheTop_AndShowTheRank()
    {
        var progress = Progress(9, PlayerId);
        _tower.GetProgressReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(progress);
        _tower.GetRankAsync(progress, Arg.Any<CancellationToken>()).Returns(4);

        var view = await Tower().Handle(new GetTowerQuery(PlayerId), CancellationToken.None);

        Assert.Equal((9, (int?)4), (view.HighestFloor, view.Rank));
        Assert.True(view.Next!.IsBoss);
        Assert.Equal([10, 11, 12], view.Upcoming.Select(f => f.Number));
    }

    [Fact]
    public async Task GetTower_ShouldShowNoNextFloor_OnceConquered()
    {
        _tower.GetProgressReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Progress(12, PlayerId));

        var view = await Tower().Handle(new GetTowerQuery(PlayerId), CancellationToken.None);

        Assert.Null(view.Next);
        Assert.Empty(view.Upcoming);
    }

    /// <summary>Закрита ратушею вежа все одно показує, що чекає попереду, — це мотивує будувати.</summary>
    [Fact]
    public async Task GetTower_ShouldMarkTheTowerLocked_BelowTheTownHallLevel()
    {
        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall(townhallLevel: 2));

        var view = await Tower().Handle(new GetTowerQuery(PlayerId), CancellationToken.None);

        Assert.False(view.Unlocked);
        Assert.Equal(3, view.RequiresMainBuildingLevel);
    }

    /// <summary>Бафи, що діятимуть у бою, видно до штурму.</summary>
    [Fact]
    public async Task GetTower_ShouldShowTheActiveBuffs()
    {
        _effects.GetAsync(PlayerId, EffectTarget.Attack, Arg.Any<CancellationToken>())
            .Returns(new ActiveEffect(Guid.NewGuid(), PlayerId, EffectTarget.Attack, 1.25, Entities.Now.AddHours(-1), Entities.Now.AddHours(1), "boost"));

        var view = await Tower().Handle(new GetTowerQuery(PlayerId), CancellationToken.None);

        Assert.Equal((1.25, 1.0), (view.AttackBuff, view.DefenseBuff));
    }

    /// <summary>Топ з іменами за порядком, а власний рядок — навіть поза топом.</summary>
    [Fact]
    public async Task GetLeaderboard_ShouldListTheTop_AndKeepMyOwnRow()
    {
        var leader = Progress(12);
        var second = Progress(7);
        var mine = Progress(3, PlayerId);
        _tower.GetTopAsync(2, Arg.Any<CancellationToken>()).Returns([leader, second]);
        _tower.GetProgressReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(mine);
        _tower.GetRankAsync(mine, Arg.Any<CancellationToken>()).Returns(17);

        var board = await new GetTowerLeaderboardQueryHandler(_tower, _players).Handle(new GetTowerLeaderboardQuery(PlayerId, Count: 2), CancellationToken.None);

        Assert.Equal([(1, 12, "Інший"), (2, 7, "Інший")], board.Entries.Select(e => (e.Rank, e.HighestFloor, e.PlayerName)));
        Assert.Equal((17, 3, "Я"), (board.Mine!.Rank, board.Mine.HighestFloor, board.Mine.PlayerName));
    }

    [Fact]
    public async Task GetLeaderboard_ShouldGiveNoOwnRow_BeforeTheFirstFloor()
    {
        _tower.GetTopAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        var board = await new GetTowerLeaderboardQueryHandler(_tower, _players).Handle(new GetTowerLeaderboardQuery(PlayerId), CancellationToken.None);

        Assert.Empty(board.Entries);
        Assert.Null(board.Mine);
    }
}
