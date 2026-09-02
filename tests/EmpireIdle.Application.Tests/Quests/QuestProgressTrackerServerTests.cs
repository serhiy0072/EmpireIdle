using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Tracking;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Quests;

/// <summary>
/// Серверна гілка трекера: подія перетворюється на внесок у свій рядок гравця.
/// Спільний Total тут не чіпається — його збирає джоб, і саме тому
/// тисяча гравців не б'ється за один рядок.
/// </summary>
public class QuestProgressTrackerServerTests
{
    private const string ServerQuestKey = "server_cleanup";
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IQuestRepository _quests = Substitute.For<IQuestRepository>();
    private readonly IServerQuestRepository _serverQuests = Substitute.For<IServerQuestRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Quests =
        [
            new QuestConfig
            {
                Key = ServerQuestKey,
                DisplayName = "Cleanup",
                Scope = QuestScope.Server,
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 1000 }]
            },
            new QuestConfig
            {
                Key = "server_expired",
                DisplayName = "Old event",
                Scope = QuestScope.Server,
                ActiveTo = Now.AddDays(-1),
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 500 }]
            }
        ]
    };

    private QuestProgressTracker Tracker()
    {
        _serverContext.ServerId.Returns(1);
        _quests.GetByKeysAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        return new QuestProgressTracker(
            _quests, _serverContext, _serverQuests,
            new GameCatalog(Config()), NullLogger<QuestProgressTracker>.Instance);
    }

    private static QuestSignal Signal(int increment = 1, string eventType = "MonsterDefeated")
        => new(PlayerId, eventType, Target: null, Increment: increment, CurrentValue: null);

    /// <summary>Перша подія створює рядок внеску.</summary>
    [Fact]
    public async Task TrackAsync_ShouldCreateContributionOnFirstEvent()
    {
        _serverQuests.GetContributionAsync(ServerQuestKey, PlayerId, Arg.Any<CancellationToken>())
            .Returns((ServerQuestContribution?)null);

        await Tracker().TrackAsync(Signal(), Now, CancellationToken.None);

        await _serverQuests.Received(1).AddContributionAsync(
            Arg.Is<ServerQuestContribution>(c => c.PlayerId == PlayerId && c.QuestKey == ServerQuestKey),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Наступні події додаються в наявний рядок.</summary>
    [Fact]
    public async Task TrackAsync_ShouldAccumulateIntoAnExistingContribution()
    {
        var contribution = new ServerQuestContribution(Guid.NewGuid(), 1, ServerQuestKey, PlayerId);
        contribution.Add(5, Now.AddHours(-1));

        _serverQuests.GetContributionAsync(ServerQuestKey, PlayerId, Arg.Any<CancellationToken>())
            .Returns(contribution);

        await Tracker().TrackAsync(Signal(increment: 3), Now, CancellationToken.None);

        Assert.Equal(8, contribution.Amount);
        await _serverQuests.DidNotReceive().AddContributionAsync(
            Arg.Any<ServerQuestContribution>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Одна подія — один внесок, скільки б цілей на неї не реагувало.
    /// Інакше квест із двома схожими цілями рахував би подію двічі.
    /// </summary>
    [Fact]
    public async Task TrackAsync_ShouldCountAnEventOnce()
    {
        var config = Config();
        config.Quests[0].Objectives =
        [
            new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 500 },
            new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 1000 }
        ];

        var contribution = new ServerQuestContribution(Guid.NewGuid(), 1, ServerQuestKey, PlayerId);

        _serverContext.ServerId.Returns(1);
        _quests.GetByKeysAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _serverQuests.GetContributionAsync(ServerQuestKey, PlayerId, Arg.Any<CancellationToken>())
            .Returns(contribution);

        var tracker = new QuestProgressTracker(
            _quests, _serverContext, _serverQuests,
            new GameCatalog(config), NullLogger<QuestProgressTracker>.Instance);

        await tracker.TrackAsync(Signal(increment: 7), Now, CancellationToken.None);

        Assert.Equal(7, contribution.Amount);
    }

    /// <summary>Завершений квест внесків більше не приймає.</summary>
    [Fact]
    public async Task TrackAsync_ShouldIgnoreCompletedQuests()
    {
        var progress = new ServerQuestProgress(Guid.NewGuid(), 1, ServerQuestKey, target: 100);
        progress.UpdateTotal(100, Now.AddHours(-1));

        _serverQuests.GetProgressAsync(ServerQuestKey, Arg.Any<CancellationToken>()).Returns(progress);

        await Tracker().TrackAsync(Signal(), Now, CancellationToken.None);

        await _serverQuests.DidNotReceive().AddContributionAsync(
            Arg.Any<ServerQuestContribution>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Квест поза вікном дат внесків не збирає.</summary>
    [Fact]
    public async Task TrackAsync_ShouldIgnoreQuestsOutsideTheirWindow()
    {
        await Tracker().TrackAsync(Signal(), Now, CancellationToken.None);

        await _serverQuests.DidNotReceive().GetContributionAsync(
            "server_expired", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Подія, на яку не реагує жодна ціль, внеску не дає.</summary>
    [Fact]
    public async Task TrackAsync_ShouldIgnoreUnrelatedEvents()
    {
        await Tracker().TrackAsync(Signal(eventType: "BuildingCollected"), Now, CancellationToken.None);

        await _serverQuests.DidNotReceive().GetContributionAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Спільний підсумок трекер не чіпає: його рахує джоб із суми внесків,
    /// і саме тому гравці не конкурують за один рядок.
    /// </summary>
    [Fact]
    public async Task TrackAsync_ShouldNotTouchTheSharedTotal()
    {
        _serverQuests.GetContributionAsync(ServerQuestKey, PlayerId, Arg.Any<CancellationToken>())
            .Returns((ServerQuestContribution?)null);

        await Tracker().TrackAsync(Signal(), Now, CancellationToken.None);

        await _serverQuests.DidNotReceive().AddProgressAsync(
            Arg.Any<ServerQuestProgress>(), Arg.Any<CancellationToken>());
    }
}
