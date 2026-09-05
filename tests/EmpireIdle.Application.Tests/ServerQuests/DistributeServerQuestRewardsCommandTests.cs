using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
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
/// Роздача за рангом. Найдорожчі помилки тут — подвійна видача й
/// недетермінований порядок: обидві коштують гравцям нагород,
/// і обидві помітні лише постфактум.
/// </summary>
public class DistributeServerQuestRewardsCommandTests
{
    private const string QuestKey = "server_cleanup";
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IServerQuestRepository _quests = Substitute.For<IServerQuestRepository>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public DistributeServerQuestRewardsCommandTests() => _granter.RewardType.Returns("Gems");

    /// <summary>Три яруси: топ-1, топ-3, усі інші.</summary>
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
                RewardTiers =
                [
                    new RewardTierConfig { MaxRank = 1, Rewards = [new RewardConfig { Type = "Gems", Amount = 100 }] },
                    new RewardTierConfig { MaxRank = 3, Rewards = [new RewardConfig { Type = "Gems", Amount = 50 }] },
                    new RewardTierConfig { MaxRank = null, Rewards = [new RewardConfig { Type = "Gems", Amount = 10 }] }
                ]
            }
        ]
    };

    private DistributeServerQuestRewardsCommandHandler Handler() => new(
        _quests, new RewardDispatcher([_granter]), _unitOfWork,
        new GameCatalog(Config()), new FakeTimeProvider(Now),
        NullLogger<DistributeServerQuestRewardsCommandHandler>.Instance);

    private ServerQuestProgress GivenCompletedQuest()
    {
        var progress = new ServerQuestProgress(Guid.NewGuid(), 1, QuestKey, target: 100);
        progress.UpdateTotal(100, Now);

        _quests.GetProgressAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(progress);

        return progress;
    }

    /// <summary>Внески вже впорядковані репозиторієм: більший раніше.</summary>
    private List<ServerQuestContribution> GivenContributions(params long[] amounts)
    {
        var contributions = amounts
            .Select((amount, index) =>
            {
                var contribution = new ServerQuestContribution(Guid.NewGuid(), 1, QuestKey, Guid.NewGuid());
                contribution.Add(amount, Now.AddMinutes(index));
                return contribution;
            })
            .OrderByDescending(c => c.Amount)
            .ThenBy(c => c.LastContributedAt)
            .ToList();

        _quests.GetRankedAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(contributions);

        return contributions;
    }

    /// <summary>Кожен ранг отримує нагороду свого ярусу.</summary>
    [Fact]
    public async Task Handle_ShouldGrantByTier()
    {
        GivenCompletedQuest();
        var contributions = GivenContributions(500, 300, 200, 100, 50);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        // Ранг 1 → 100, ранги 2-3 → 50, решта → 10
        await _granter.Received(1).GrantAsync(
            Arg.Is<RewardContext>(c => c.PlayerId == contributions[0].PlayerId && c.Reward.Amount == 100),
            Arg.Any<CancellationToken>());

        await _granter.Received(1).GrantAsync(
            Arg.Is<RewardContext>(c => c.PlayerId == contributions[1].PlayerId && c.Reward.Amount == 50),
            Arg.Any<CancellationToken>());

        await _granter.Received(1).GrantAsync(
            Arg.Is<RewardContext>(c => c.PlayerId == contributions[4].PlayerId && c.Reward.Amount == 10),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Кожен контрибутор отримує рівно одну нагороду.</summary>
    [Fact]
    public async Task Handle_ShouldGrantOncePerContributor()
    {
        GivenCompletedQuest();
        GivenContributions(500, 300, 200, 100, 50);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        await _granter.Received(5).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Повторний прогін нікому нічого не видає: RewardedAt — позначка
    /// одноразовості, і саме вона рятує від подвійної роздачі після збою.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotGrantTwice()
    {
        GivenCompletedQuest();
        GivenContributions(500, 300);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);
        _granter.ClearReceivedCalls();

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Незавершений квест нагород не роздає.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheQuestIsStillInProgress()
    {
        var progress = new ServerQuestProgress(Guid.NewGuid(), 1, QuestKey, target: 100);
        progress.UpdateTotal(50, Now);

        _quests.GetProgressAsync(QuestKey, Arg.Any<CancellationToken>()).Returns(progress);
        GivenContributions(50);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Квест без ярусів пропускається мовчки, а не падає.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenNoTiersAreConfigured()
    {
        var config = Config();
        config.Quests[0].RewardTiers = [];

        var handler = new DistributeServerQuestRewardsCommandHandler(
            _quests, new RewardDispatcher([_granter]), _unitOfWork,
            new GameCatalog(config), new FakeTimeProvider(Now),
            NullLogger<DistributeServerQuestRewardsCommandHandler>.Instance);

        GivenCompletedQuest();
        GivenContributions(500);

        await handler.Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ярус «всі інші» стоїть останнім навіть у переплутаному конфізі:
    /// FindTier сортує за порогом, тому MaxRank = null не перехоплює топ.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotLetTheCatchAllTierStealTheTop()
    {
        var config = Config();

        // Ярус «всі інші» навмисно першим у списку
        config.Quests[0].RewardTiers =
        [
            new RewardTierConfig { MaxRank = null, Rewards = [new RewardConfig { Type = "Gems", Amount = 10 }] },
            new RewardTierConfig { MaxRank = 1, Rewards = [new RewardConfig { Type = "Gems", Amount = 100 }] }
        ];

        var handler = new DistributeServerQuestRewardsCommandHandler(
            _quests, new RewardDispatcher([_granter]), _unitOfWork,
            new GameCatalog(config), new FakeTimeProvider(Now),
            NullLogger<DistributeServerQuestRewardsCommandHandler>.Instance);

        GivenCompletedQuest();
        var contributions = GivenContributions(500, 100);

        await handler.Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        await _granter.Received(1).GrantAsync(
            Arg.Is<RewardContext>(c => c.PlayerId == contributions[0].PlayerId && c.Reward.Amount == 100),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Роздача зберігається одним разом на весь квест.</summary>
    [Fact]
    public async Task Handle_ShouldSaveOnce()
    {
        GivenCompletedQuest();
        GivenContributions(500, 300, 100);

        await Handler().Handle(new DistributeServerQuestRewardsCommand(QuestKey), CancellationToken.None);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
