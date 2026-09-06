using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.ServerQuests.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.ServerQuests;

/// <summary>
/// Джоб підрахунку: збирає внески в спільний підсумок і запускає роздачу.
/// Ключове — що він створює рядки для нових квестів із конфіга й підбирає
/// завершені з невиданими нагородами, а не лише щойно завершені.
/// </summary>
public class UpdateServerQuestTotalsCommandTests
{
    private const string QuestKey = "server_cleanup";
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IServerQuestRepository _quests = Substitute.For<IServerQuestRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Quests =
        [
            new QuestConfig
            {
                Key = QuestKey,
                DisplayName = "Cleanup",
                Scope = QuestScope.Server,
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 100 }]
            },
            new QuestConfig
            {
                Key = "personal_quest",
                DisplayName = "Personal",
                Scope = QuestScope.Personal,
                Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = 5 }]
            }
        ]
    };

    private UpdateServerQuestTotalsCommandHandler Handler()
    {
        _serverContext.ServerId.Returns(1);

        return new UpdateServerQuestTotalsCommandHandler(
            _quests, _unitOfWork, _serverContext, _mediator,
            new GameCatalog(Config()),
            new FakeTimeProvider(Now),
            NullLogger<UpdateServerQuestTotalsCommandHandler>.Instance);
    }

    private ServerQuestProgress GivenProgress(long target = 100, long total = 0)
    {
        var progress = new ServerQuestProgress(Guid.NewGuid(), 1, QuestKey, target);

        if (total > 0)
            progress.UpdateTotal(total, Now.AddHours(-1));

        _quests.GetProgressAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(progress);
        _quests.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([progress]);
        _quests.GetCompletedWithPendingRewardsAsync(Arg.Any<CancellationToken>()).Returns([]);

        return progress;
    }

    /// <summary>
    /// Рядок прогресу створюється з конфіга при першому прогоні —
    /// окремого кроку ініціалізації світу не потрібно.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCreateProgressForNewServerQuests()
    {
        _quests.GetCompletedWithPendingRewardsAsync(Arg.Any<CancellationToken>()).Returns([]);
        _quests.GetProgressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ServerQuestProgress?)null);
        _quests.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([]);

        await Handler().Handle(new UpdateServerQuestTotalsCommand(), CancellationToken.None);

        await _quests.Received(1).AddProgressAsync(
            Arg.Is<ServerQuestProgress>(p => p.QuestKey == QuestKey && p.Target == 100),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Особисті квести рядка не отримують — вони живуть у гравця.</summary>
    [Fact]
    public async Task Handle_ShouldIgnorePersonalQuests()
    {
        _quests.GetProgressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ServerQuestProgress?)null);
        _quests.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([]);
        _quests.GetCompletedWithPendingRewardsAsync(Arg.Any<CancellationToken>()).Returns([]);

        await Handler().Handle(new UpdateServerQuestTotalsCommand(), CancellationToken.None);

        await _quests.DidNotReceive().AddProgressAsync(
            Arg.Is<ServerQuestProgress>(p => p.QuestKey == "personal_quest"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Підсумок береться з суми внесків.</summary>
    [Fact]
    public async Task Handle_ShouldWriteTheSumOfContributions()
    {
        var progress = GivenProgress();
        _quests.SumContributionsAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(42);

        await Handler().Handle(new UpdateServerQuestTotalsCommand(), CancellationToken.None);

        Assert.Equal(42, progress.Total);
        Assert.Equal(QuestState.InProgress, progress.State);
    }

    /// <summary>Досягнення цілі завершує квест.</summary>
    [Fact]
    public async Task Handle_ShouldCompleteTheQuest_WhenTheTargetIsReached()
    {
        var progress = GivenProgress(target: 100);
        _quests.SumContributionsAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(150);

        await Handler().Handle(new UpdateServerQuestTotalsCommand(), CancellationToken.None);

        Assert.Equal(QuestState.Completed, progress.State);
    }

    /// <summary>
    /// Роздача запускається для завершених квестів із невиданими нагородами.
    /// Не лише для щойно завершених: збій між збереженням і видачею інакше
    /// лишив би нагороди нероздаими назавжди.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldDistributeRewardsForQuestsWithPendingGrants()
    {
        GivenProgress();
        _quests.SumContributionsAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(0);
        _quests.GetCompletedWithPendingRewardsAsync(Arg.Any<CancellationToken>()).Returns([QuestKey]);

        await Handler().Handle(new UpdateServerQuestTotalsCommand(), CancellationToken.None);

        // Через ReceivedCalls, а не Received(): MediatR має кілька перевантажень
        // Send, і зіставлення за типом аргументу залежить від того, яку
        // з них обрав компілятор у хендлері
        var sent = _mediator.ReceivedCalls()
            .SelectMany(c => c.GetArguments())
            .OfType<DistributeServerQuestRewardsCommand>()
            .ToList();

        Assert.Single(sent);
        Assert.Equal(QuestKey, sent[0].QuestKey);
    }

    /// <summary>Без завершених квестів роздача не запускається.</summary>
    [Fact]
    public async Task Handle_ShouldNotDistribute_WhenNothingIsPending()
    {
        GivenProgress();
        _quests.SumContributionsAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(10);

        await Handler().Handle(new UpdateServerQuestTotalsCommand(), CancellationToken.None);

        Assert.Empty(_mediator.ReceivedCalls()
            .SelectMany(c => c.GetArguments())
            .OfType<DistributeServerQuestRewardsCommand>());
    }

    /// <summary>Один SaveChanges на весь прогін — квестів одиниці.</summary>
    [Fact]
    public async Task Handle_ShouldSaveOnce()
    {
        GivenProgress();
        _quests.SumContributionsAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(10);

        await Handler().Handle(new UpdateServerQuestTotalsCommand(), CancellationToken.None);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
