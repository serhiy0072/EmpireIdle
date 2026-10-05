using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.ServerQuests.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.ServerQuests;

/// <summary>
/// Нагорода за серверний квест (GDD §2.7, §8.4): кожному гравцю світу — лист, топу за внеском —
/// бонус свого ярусу в тому самому листі. Найдорожчі помилки — подвійна розсилка після збою,
/// загублений гравець і недетермінований ранг: усі три помітні лише постфактум.
/// </summary>
public class DistributeServerQuestRewardsCommandTests
{
    private const string QuestKey = "server_cleanup";
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IServerQuestRepository _quests = Substitute.For<IServerQuestRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IMailRepository _mail = Substitute.For<IMailRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<MailLetter> _letters = [];

    public DistributeServerQuestRewardsCommandTests()
    {
        _serverContext.ServerId.Returns(1);
        _unitOfWork.TrySaveChangesAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mail.When(m => m.AddLetterAsync(Arg.Any<MailLetter>(), Arg.Any<CancellationToken>()))
            .Do(call => _letters.Add(call.Arg<MailLetter>()));
    }

    /// <summary>Усім — 20 gems; бонус: топ-1 — 100, топ-3 — 50.</summary>
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
                Objectives = [new QuestObjectiveConfig { Type = "MonsterDefeated", Count = 100 }],
                Rewards = [new RewardConfig { Type = "Gems", Amount = 20 }],
                RewardTiers =
                [
                    new RewardTierConfig { MaxRank = 1, Rewards = [new RewardConfig { Type = "Gems", Amount = 100 }] },
                    new RewardTierConfig { MaxRank = 3, Rewards = [new RewardConfig { Type = "Gems", Amount = 50 }] }
                ]
            }
        ]
    };

    private DistributeServerQuestRewardsCommandHandler Handler(GameConfig? config = null) => new(
        _quests, _players, _mail, _serverContext, _unitOfWork,
        new GameCatalog(config ?? Config()), new FakeTimeProvider(Now),
        NullLogger<DistributeServerQuestRewardsCommandHandler>.Instance);

    private ServerQuestProgress GivenCompletedQuest()
    {
        var progress = new ServerQuestProgress(Guid.NewGuid(), 1, QuestKey, target: 100);
        progress.UpdateTotal(100, Now);

        _quests.GetProgressAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(progress);

        return progress;
    }

    /// <summary>Гравці світу за зростанням Id — так, як віддає keyset-запит репозиторію.</summary>
    private List<Guid> GivenPlayers(int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).OrderBy(id => id).ToList();

        _players.GetIdsAfterAsync(Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var after = call.ArgAt<Guid?>(0);
                IReadOnlyList<Guid> page = ids
                    .Where(id => after is null || id.CompareTo(after.Value) > 0)
                    .Take(call.ArgAt<int>(1))
                    .ToList();
                return page;
            });

        return ids;
    }

    /// <summary>Внески вже впорядковані репозиторієм: більший раніше.</summary>
    private List<ServerQuestContribution> GivenContributions(IReadOnlyList<Guid> playerIds, params long[] amounts)
    {
        var contributions = amounts
            .Select((amount, index) =>
            {
                var contribution = new ServerQuestContribution(Guid.NewGuid(), 1, QuestKey, playerIds[index]);
                contribution.Add(amount, Now.AddMinutes(index));
                return contribution;
            })
            .OrderByDescending(c => c.Amount)
            .ThenBy(c => c.LastContributedAt)
            .ToList();

        _quests.GetRankedAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(contributions);

        return contributions;
    }

    private int GemsOf(Guid playerId)
        => _letters.Where(l => l.PlayerId == playerId).SelectMany(l => l.Rewards).Where(r => r.Type == "Gems").Sum(r => r.Amount);

    /// <summary>Лист отримує кожен гравець світу, навіть той, хто не вносив.</summary>
    [Fact]
    public async Task Handle_ShouldMailEveryPlayerOfTheWorld()
    {
        GivenCompletedQuest();
        var players = GivenPlayers(5);
        GivenContributions(players, 500);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        Assert.Equal(5, _letters.Count);
        Assert.All(_letters, letter => Assert.Equal(MailKind.ServerQuestReward, letter.Kind));
        Assert.Equal(players.Order(), _letters.Select(l => l.PlayerId).Order());
    }

    /// <summary>Топ отримує бонус свого ярусу в тому самому листі, одним рядком із нагородою для всіх.</summary>
    [Fact]
    public async Task Handle_ShouldAddTheTierBonus_ToTheTopContributors()
    {
        GivenCompletedQuest();
        var players = GivenPlayers(5);
        var contributions = GivenContributions(players, 500, 300, 200, 100);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        Assert.Equal(120, GemsOf(contributions[0].PlayerId));
        Assert.Equal(70, GemsOf(contributions[1].PlayerId));
        Assert.Equal(70, GemsOf(contributions[2].PlayerId));
        Assert.Equal(20, GemsOf(contributions[3].PlayerId));
        Assert.Equal(20, GemsOf(players[4]));

        var top = Assert.Single(_letters, l => l.PlayerId == contributions[0].PlayerId);
        Assert.Single(top.Rewards);
    }

    /// <summary>Пачки зберігаються разом із курсором, а кінець розсилки ставить позначку — повторний прогін нічого не шле.</summary>
    [Fact]
    public async Task Handle_ShouldMailInBatches_AndFinish()
    {
        var progress = GivenCompletedQuest();
        var players = GivenPlayers(450);
        GivenContributions(players, 10);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        Assert.Equal(450, _letters.Count);
        Assert.Equal(players[^1], progress.MailedThroughPlayerId);
        Assert.Equal(Now, progress.RewardsMailedAt);
        await _unitOfWork.Received(4).TrySaveChangesAsync(Arg.Any<CancellationToken>());

        _letters.Clear();
        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        Assert.Empty(_letters);
    }

    /// <summary>
    /// Після збою прохід продовжує від збереженого курсора: ті, кому лист уже пішов, другого не отримають.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldResumeFromTheCursor()
    {
        var progress = GivenCompletedQuest();
        var players = GivenPlayers(6);
        GivenContributions(players, 10);
        progress.AdvanceMailing(players[2]);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        Assert.Equal(players.Skip(3), _letters.Select(l => l.PlayerId));
    }

    /// <summary>Конфлікт у пачці зупиняє прохід і не ставить позначку кінця: наступний прогін продовжить.</summary>
    [Fact]
    public async Task Handle_ShouldStop_WhenABatchConflicts()
    {
        var progress = GivenCompletedQuest();
        var players = GivenPlayers(450);
        GivenContributions(players, 10);
        _unitOfWork.TrySaveChangesAsync(Arg.Any<CancellationToken>()).Returns(false);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        await _unitOfWork.Received(1).TrySaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Null(progress.RewardsMailedAt);
    }

    /// <summary>Незавершений квест нагород не роздає.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheQuestIsStillInProgress()
    {
        var progress = new ServerQuestProgress(Guid.NewGuid(), 1, QuestKey, target: 100);
        progress.UpdateTotal(50, Now);
        _quests.GetProgressAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(progress);
        GivenPlayers(3);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        Assert.Empty(_letters);
    }

    /// <summary>
    /// Ярус «всі інші, хто вніс» стоїть останнім навіть у переплутаному конфізі:
    /// FindTier сортує за порогом, тому MaxRank = null не перехоплює топ.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotLetTheCatchAllTierStealTheTop()
    {
        var config = Config();
        config.Quests[0].RewardTiers =
        [
            new RewardTierConfig { MaxRank = null, Rewards = [new RewardConfig { Type = "Gems", Amount = 10 }] },
            new RewardTierConfig { MaxRank = 1, Rewards = [new RewardConfig { Type = "Gems", Amount = 100 }] }
        ];

        GivenCompletedQuest();
        var players = GivenPlayers(3);
        var contributions = GivenContributions(players, 500, 100);

        await Handler(config).Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        Assert.Equal(120, GemsOf(contributions[0].PlayerId));
        Assert.Equal(30, GemsOf(contributions[1].PlayerId));
        Assert.Equal(20, GemsOf(players[2]));
    }
}
