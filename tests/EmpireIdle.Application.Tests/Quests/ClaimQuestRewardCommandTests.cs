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
                Key = "server_cleanup",
                Scope = QuestScope.Server,
                Window = QuestWindow.Chain,
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 100 }],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 50 }]
            }
        ]
    };

    private ClaimQuestRewardCommandHandler Handler() => new(
        _quests, new RewardDispatcher([_granter]), _unitOfWork,
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

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "daily_collect"), CancellationToken.None));

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Незавершений квест нагороди не дає.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheQuestIsNotComplete()
    {
        GivenProgress(completed: false);

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "daily_collect"), CancellationToken.None));

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Серверний квест не клеймиться поштучно: його нагорода видається
    /// всім за рангом при завершенні, і особистий клейм видав би її двічі.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_ForServerScopedQuests()
    {
        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "server_cleanup"), CancellationToken.None));
    }

    /// <summary>Квест, якого гравець не починав — 404.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenProgressIsMissing()
    {
        _quests.GetAsync(PlayerId, "daily_collect", Arg.Any<CancellationToken>()).Returns((QuestProgress?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new ClaimQuestRewardCommand(PlayerId, "daily_collect"), CancellationToken.None));
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
}
