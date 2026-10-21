using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Commands;
using EmpireIdle.Application.Quests.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Quests;

/// <summary>
/// «Зібрати все» забирає рівно те, що список показує завершеним: ні зачинених ланок,
/// ні вже забраних, ні квестів поза вікном. Порогові квести без рядка фіксує сама команда.
/// </summary>
public class ClaimAllQuestRewardsCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IQuestRepository _quests = Substitute.For<IQuestRepository>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();

    public ClaimAllQuestRewardsCommandTests() => _granter.RewardType.Returns("Gems");

    private static QuestConfig Collect(string key, string? prerequisite = null) => new()
    {
        Key = key,
        Scope = QuestScope.Personal,
        Window = QuestWindow.Chain,
        Prerequisite = prerequisite,
        Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = 5 }],
        Rewards = [new RewardConfig { Type = "Gems", Amount = 10 }]
    };

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Quests =
        [
            Collect("done_a"),
            Collect("done_b"),
            Collect("in_progress"),
            Collect("already_claimed"),
            Collect("locked_next", prerequisite: "in_progress"),
            new QuestConfig
            {
                Key = "event_over",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Event,
                ActiveFrom = Now.AddDays(-7),
                ActiveTo = Now.AddDays(-1),
                Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = 5 }],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 10 }]
            },
            new QuestConfig
            {
                Key = "server_cleanup",
                Scope = QuestScope.Server,
                Window = QuestWindow.Chain,
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 100 }],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 50 }]
            },
            new QuestConfig
            {
                Key = "townhall_2",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Chain,
                Objectives =
                [
                    new QuestObjectiveConfig
                    {
                        Type = "BuildingUpgradeCompleted", Target = "townhall", Count = 2, Mode = ObjectiveMode.Threshold
                    }
                ],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 20 }]
            }
        ]
    };

    private ClaimAllQuestRewardsCommandHandler Handler() => new(
        _quests, new QuestThresholds(_quests, _villages, Substitute.For<IServerContext>()),
        new RewardDispatcher([_granter]), _unitOfWork,
        new GameCatalog(Config()), new FakeTimeProvider(Now),
        NullLogger<ClaimAllQuestRewardsCommandHandler>.Instance);

    private static QuestProgress Progress(string questKey, bool completed = true, bool claimed = false)
    {
        var progress = new QuestProgress(Guid.NewGuid(), PlayerId, 1, questKey, [5], Now);

        if (completed)
            progress.Advance(0, 5, Now);

        if (claimed)
            progress.Claim(Now);

        return progress;
    }

    private void GivenProgress(params QuestProgress[] progress)
        => _quests.GetAllAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(progress.ToList());

    /// <summary>Село з ратушею заданого рівня — для порогових квестів.</summary>
    private void GivenTownHall(int level)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        village.AddBuilding("townhall", catalog.Buildings, Now);

        var townhall = village.Buildings.Single();
        for (var i = 1; i < level; i++)
        {
            townhall.BeginUpgrade(catalog.Buildings["townhall"], TimeSpan.Zero, Now, ProductionBoost.None, 1.0);
            townhall.CompleteConstruction(Now);
        }

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
    }

    /// <summary>Забирає лише завершені квести й зберігає все одним SaveChanges.</summary>
    [Fact]
    public async Task Handle_ShouldClaimEveryCompletedQuest_AndSaveOnce()
    {
        var doneA = Progress("done_a");
        var doneB = Progress("done_b");
        var inProgress = Progress("in_progress", completed: false);
        GivenProgress(doneA, doneB, inProgress, Progress("already_claimed", claimed: true));
        GivenTownHall(1);

        var claimed = await Handler().Handle(new ClaimAllQuestRewardsCommand(PlayerId), CancellationToken.None);

        Assert.Equal(["done_a", "done_b"], claimed);
        Assert.Equal(QuestState.Claimed, doneA.State);
        Assert.Equal(QuestState.Claimed, doneB.State);
        Assert.Equal(QuestState.InProgress, inProgress.State);
        await _granter.Received(2).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Зачинена ланка, квест поза вікном і серверний квест не забираються, навіть якщо
    /// їхній рядок завершений: «Зібрати все» не обходить меж поштучного клейму.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldSkipLockedOutOfWindowAndServerQuests()
    {
        GivenProgress(
            Progress("in_progress", completed: false),
            Progress("locked_next"),
            Progress("event_over"),
            Progress("server_cleanup"));
        GivenTownHall(1);

        var claimed = await Handler().Handle(new ClaimAllQuestRewardsCommand(PlayerId), CancellationToken.None);

        Assert.Empty(claimed);
        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Нічого забирати — не відмова, а порожній результат без запису в базу.</summary>
    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNothingIsCompleted()
    {
        GivenProgress(Progress("in_progress", completed: false));
        GivenTownHall(1);

        var claimed = await Handler().Handle(new ClaimAllQuestRewardsCommand(PlayerId), CancellationToken.None);

        Assert.Empty(claimed);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ратуша вже 2, рядка порогового квесту немає: команда заводить його рівно один раз
    /// і одразу забирає — як поштучний клейм.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldClaimAThresholdQuestWithoutAStoredRow()
    {
        GivenProgress();
        GivenTownHall(2);

        var claimed = await Handler().Handle(new ClaimAllQuestRewardsCommand(PlayerId), CancellationToken.None);

        Assert.Equal(["townhall_2"], claimed);
        await _quests.Received(1).AddAsync(
            Arg.Is<QuestProgress>(p => p.QuestKey == "townhall_2" && p.State == QuestState.Claimed),
            Arg.Any<CancellationToken>());
    }
}
