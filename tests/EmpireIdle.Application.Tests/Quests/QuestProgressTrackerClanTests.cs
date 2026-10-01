using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Tracking;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Quests;

/// <summary>
/// Кланова гілка трекера: внески учасників складаються в спільний рядок клану,
/// а завершення одразу нараховує клану очки вкладу.
/// </summary>
public class QuestProgressTrackerClanTests
{
    private const string ClanQuestKey = "clan_hunt";
    private const long ClanPoints = 500;
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly IQuestRepository _quests = Substitute.For<IQuestRepository>();
    private readonly IServerQuestRepository _serverQuests = Substitute.For<IServerQuestRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanQuestRepository _clanQuests = Substitute.For<IClanQuestRepository>();

    private readonly Guid _founder = Guid.NewGuid();
    private readonly Clan _clan;

    public QuestProgressTrackerClanTests()
    {
        _clan = new Clan(Guid.NewGuid(), 1, "Northern Watch", "NW", _founder, Now);

        _serverContext.ServerId.Returns(1);
        _quests.GetByKeysAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>()).Returns([]);
        _clans.GetClanIdByMemberAsync(_founder, Arg.Any<CancellationToken>()).Returns(_clan.Id);
        _clans.GetByIdAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns(_clan);
    }

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Quests =
        [
            new QuestConfig
            {
                Key = ClanQuestKey,
                DisplayName = "Clan hunt",
                Scope = QuestScope.Clan,
                ClanPoints = ClanPoints,
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 10 }]
            }
        ]
    };

    private QuestProgressTracker Tracker() => new(_quests, _serverContext, _serverQuests, _clans, _clanQuests,
        new GameCatalog(Config()), NullLogger<QuestProgressTracker>.Instance);

    private static QuestSignal Kills(Guid playerId, int count) => new(playerId, "MonsterDefeated", null, count, null);

    [Fact]
    public async Task Track_ShouldStartTheClanProgress_OnTheFirstContribution()
    {
        await Tracker().TrackAsync(Kills(_founder, 3), Now, CancellationToken.None);

        await _clanQuests.Received(1).AddAsync(
            Arg.Is<ClanQuestProgress>(p => p.ClanId == _clan.Id && p.QuestKey == ClanQuestKey && p.Amount == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Track_ShouldCreditClanPoints_WhenTheQuestCompletes()
    {
        var progress = new ClanQuestProgress(Guid.NewGuid(), 1, _clan.Id, ClanQuestKey, 10);
        progress.Add(8, Now);
        _clanQuests.GetAsync(_clan.Id, ClanQuestKey, Arg.Any<CancellationToken>()).Returns(progress);

        await Tracker().TrackAsync(Kills(_founder, 5), Now, CancellationToken.None);

        Assert.Equal(QuestState.Completed, progress.State);
        Assert.Equal(ClanPoints, _clan.ContributionPoints);
    }

    /// <summary>Завершений квест більше нічого не нараховує — інакше кожен наступний бій давав би очки знову.</summary>
    [Fact]
    public async Task Track_ShouldNotCreditAgain_AfterCompletion()
    {
        var progress = new ClanQuestProgress(Guid.NewGuid(), 1, _clan.Id, ClanQuestKey, 10);
        progress.Add(10, Now);
        _clanQuests.GetAsync(_clan.Id, ClanQuestKey, Arg.Any<CancellationToken>()).Returns(progress);

        await Tracker().TrackAsync(Kills(_founder, 5), Now, CancellationToken.None);

        Assert.Equal(0, _clan.ContributionPoints);
    }

    [Fact]
    public async Task Track_ShouldIgnorePlayersWithoutAClan()
    {
        var loner = Guid.NewGuid();
        _clans.GetClanIdByMemberAsync(loner, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await Tracker().TrackAsync(Kills(loner, 5), Now, CancellationToken.None);

        await _clanQuests.DidNotReceive().AddAsync(Arg.Any<ClanQuestProgress>(), Arg.Any<CancellationToken>());
    }
}
