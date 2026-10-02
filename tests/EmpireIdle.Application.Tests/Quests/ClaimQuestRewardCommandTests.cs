using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.Application.Quests.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Commands;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Quests;

/// <summary>
/// Клейм видає gems, тому подвійне спрацювання коштує реальних грошей.
/// Ключове тут — порядок: перехід стану ПЕРЕД видачею.
/// </summary>
public class ClaimQuestRewardCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IQuestRepository _quests = Substitute.For<IQuestRepository>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Quests =
        [
            new QuestConfig
            {
                Key = "daily_collect",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Daily,
                Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = 5 }],
                Rewards =
                [
                    new RewardConfig { Type = "Gems", Amount = 10 },
                    new RewardConfig { Type = "Gems", Amount = 5 }
                ]
            },
            new QuestConfig
            {
                Key = "intro_townhall_3",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Chain,
                Objectives =
                [
                    new QuestObjectiveConfig
                    {
                        Type = "BuildingUpgradeCompleted", Target = "townhall", Count = 3, Mode = ObjectiveMode.Threshold
                    }
                ],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 20 }]
            },
            new QuestConfig
            {
                Key = "chain_next",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Chain,
                Prerequisite = "daily_collect",
                Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = 5 }],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 10 }]
            },
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
                Key = "gate_collect",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Chain,
                Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = 5 }],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 10 }]
            },
            new QuestConfig
            {
                Key = "gated_townhall_2",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Chain,
                Prerequisite = "gate_collect",
                Objectives =
                [
                    new QuestObjectiveConfig
                    {
                        Type = "BuildingUpgradeCompleted", Target = "townhall", Count = 2, Mode = ObjectiveMode.Threshold
                    }
                ],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 10 }]
            },
            new QuestConfig
            {
                Key = "gated_townhall_3",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Chain,
                Prerequisite = "gated_townhall_2",
                Objectives =
                [
                    new QuestObjectiveConfig
                    {
                        Type = "BuildingUpgradeCompleted", Target = "townhall", Count = 3, Mode = ObjectiveMode.Threshold
                    }
                ],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 20 }]
            }
        ]
    };

    private ClaimQuestRewardCommandHandler Handler() => new(
        _quests, new QuestThresholds(_quests, _villages, Substitute.For<IServerContext>()),
        new RewardDispatcher([_granter]), _unitOfWork,
        new GameCatalog(Config()), new FakeTimeProvider(Now),
        NullLogger<ClaimQuestRewardCommandHandler>.Instance);

    /// <summary>Прогрес квесту в заданому стані.</summary>
    private QuestProgress GivenProgress(string questKey = "daily_collect", bool completed = true,
        bool alreadyClaimed = false)
    {
        var progress = new QuestProgress(Guid.NewGuid(), PlayerId, 1, questKey, [5], Now);

        if (completed)
            progress.Advance(0, 5, Now);

        if (alreadyClaimed)
            progress.Claim(Now);

        _quests.GetAsync(PlayerId, questKey, Arg.Any<CancellationToken>()).Returns(progress);

        return progress;
    }

    /// <summary>Виконаний квест переходить у Claimed і видає всі нагороди набору.</summary>
    [Fact]
    public async Task Handle_ShouldClaimAndGrantEveryReward()
    {
        _granter.RewardType.Returns("Gems");
        var progress = GivenProgress();

        await Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "daily_collect"), CancellationToken.None);

        Assert.Equal(QuestState.Claimed, progress.State);
        await _granter.Received(2).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Повторний клейм не видає нічого. Перехід стану йде перед видачею,
    /// тому другий виклик зупиняється до того, як gems залишать сховище.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotGrantTwice()
    {
        GivenProgress(alreadyClaimed: true);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "daily_collect"), CancellationToken.None));
        Assert.Equal(RefusalReasons.QuestNotClaimable.Key, refusal.Reason);

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Незавершений квест нагороди не дає.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheQuestIsNotComplete()
    {
        GivenProgress(completed: false);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "daily_collect"), CancellationToken.None));
        Assert.Equal(RefusalReasons.QuestNotClaimable.Key, refusal.Reason);

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Серверний квест не клеймиться поштучно: його нагорода видається
    /// всім за рангом при завершенні, і особистий клейм видав би її двічі.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_ForServerScopedQuests()
    {
        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "server_cleanup"), CancellationToken.None));
        Assert.Null(refusal.Reason);
    }

    /// <summary>Квест, якого гравець не починав — 404.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenProgressIsMissing()
    {
        _quests.GetAsync(PlayerId, "daily_collect", Arg.Any<CancellationToken>()).Returns((QuestProgress?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "daily_collect"), CancellationToken.None));
    }

    /// <summary>Ключ від клієнта, якого немає в каталозі, — 404, а не 500 зі збою каталогу.</summary>
    [Fact]
    public async Task Handle_ShouldThrowNotFound_ForAnUnknownQuestKey()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "no_such_quest"), CancellationToken.None));
    }

    /// <summary>
    /// Квест за незавершеним пререквізитом у списку не видно — і прямий POST
    /// його не забирає, навіть якщо прогрес уже набрано.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenThePrerequisiteIsNotComplete()
    {
        GivenProgress("daily_collect", completed: false);
        GivenProgress("chain_next");

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "chain_next"), CancellationToken.None));

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Ланцюжок відкривається завершенням пререквізиту, а не його клеймом.</summary>
    [Fact]
    public async Task Handle_ShouldClaim_WhenThePrerequisiteIsCompleteButUnclaimed()
    {
        _granter.RewardType.Returns("Gems");
        GivenProgress("daily_collect");
        var progress = GivenProgress("chain_next");

        await Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "chain_next"), CancellationToken.None);

        Assert.Equal(QuestState.Claimed, progress.State);
    }

    /// <summary>Вікно події закрилось, поки список був відкритий, — відмова з причиною, без нагороди.</summary>
    [Fact]
    public async Task Handle_ShouldReject_OutsideTheEventWindow()
    {
        GivenProgress("event_over");

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "event_over"), CancellationToken.None));
        Assert.Equal(RefusalReasons.QuestNotClaimable.Key, refusal.Reason);

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Невідомий тип нагороди валить операцію ДО збереження: краще не видати
    /// нічого, ніж списати клейм і загубити нагороду.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotSave_WhenARewardTypeIsUnknown()
    {
        _granter.RewardType.Returns("Resource");
        GivenProgress();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "daily_collect"), CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ратушу підняли до 3 ще до відкриття квесту: подія вже минула, рядка прогресу немає.
    /// Список показує квест завершеним — тож і забрати його можна; поріг фіксує сама команда.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldClaimAThresholdQuest_ReachedBeforeItOpened()
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        var townhall = village.Buildings.Single();
        for (var i = 1; i < 4; i++)
        {
            townhall.BeginUpgrade(catalog.Buildings["townhall"], TimeSpan.Zero, Now, ProductionBoost.None, 1.0);
            townhall.CompleteConstruction(Now);
        }

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _quests.GetAsync(PlayerId, "intro_townhall_3", Arg.Any<CancellationToken>()).Returns((QuestProgress?)null);
        _granter.RewardType.Returns("Gems");

        await Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "intro_townhall_3"), CancellationToken.None);

        await _quests.Received(1).AddAsync(
            Arg.Is<QuestProgress>(p => p.QuestKey == "intro_townhall_3" && p.State == QuestState.Claimed),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

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

    /// <summary>
    /// Ланцюжок перевіряється до кінця: ратуша 3 закриває порогом і другу, і третю ланку,
    /// але перша (збір) не виконана — тож друга ще зачинена, а третя не забирається.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenAnEarlierLinkOfTheChainIsNotComplete()
    {
        GivenTownHall(level: 3);
        GivenProgress("gate_collect", completed: false);
        _quests.GetAsync(PlayerId, "gated_townhall_2", Arg.Any<CancellationToken>()).Returns((QuestProgress?)null);
        _quests.GetAsync(PlayerId, "gated_townhall_3", Arg.Any<CancellationToken>()).Returns((QuestProgress?)null);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "gated_townhall_3"), CancellationToken.None));

        await _quests.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Перша ланка виконана — пороги закривають решту ланцюжка, і третя забирається.</summary>
    [Fact]
    public async Task Handle_ShouldClaim_WhenEveryEarlierLinkIsComplete()
    {
        GivenTownHall(level: 3);
        GivenProgress("gate_collect");
        _quests.GetAsync(PlayerId, "gated_townhall_2", Arg.Any<CancellationToken>()).Returns((QuestProgress?)null);
        _quests.GetAsync(PlayerId, "gated_townhall_3", Arg.Any<CancellationToken>()).Returns((QuestProgress?)null);
        _granter.RewardType.Returns("Gems");

        await Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "gated_townhall_3"), CancellationToken.None);

        await _quests.Received(1).AddAsync(
            Arg.Is<QuestProgress>(p => p.QuestKey == "gated_townhall_3" && p.State == QuestState.Claimed),
            Arg.Any<CancellationToken>());
    }
}
