using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Quests;

/// <summary>
/// Ресет дейліків одного гравця. Поелементно, а не пачкою на світ:
/// конфлікт паралелізму коштує цього гравця, а не всієї черги —
/// джоб добовий, тож ціна помилки була б добою.
/// </summary>
public class ResetPlayerDailyQuestsCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IQuestRepository _quests = Substitute.For<IQuestRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config(int dailyTarget = 5) => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Quests =
        [
            new QuestConfig
            {
                Key = "daily_collect",
                DisplayName = "Collect",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Daily,
                Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = dailyTarget }]
            },
            new QuestConfig
            {
                Key = "chain_intro",
                DisplayName = "Intro",
                Scope = QuestScope.Personal,
                Window = QuestWindow.Chain,
                Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = 1 }]
            }
        ]
    };

    private ResetPlayerDailyQuestsCommandHandler Handler(int dailyTarget = 5) => new(
        _quests, _unitOfWork, new GameCatalog(Config(dailyTarget)),
        new FakeTimeProvider(Now),
        NullLogger<ResetPlayerDailyQuestsCommandHandler>.Instance);

    /// <summary>Прострочений дейлік із просунутим лічильником.</summary>
    private QuestProgress GivenStaleDaily(int advanced = 5, int required = 5)
    {
        var progress = new QuestProgress(Guid.NewGuid(), PlayerId, 1, "daily_collect", [required],
            Now.AddDays(-1));

        if (advanced > 0)
            progress.Advance(0, advanced, Now.AddDays(-1));

        _quests.GetStaleDailyForPlayerAsync(PlayerId, Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([progress]);

        return progress;
    }

    /// <summary>Ресет обнуляє лічильник і повертає квест у роботу.</summary>
    [Fact]
    public async Task Handle_ShouldResetProgressAndState()
    {
        var progress = GivenStaleDaily(advanced: 5);

        await Handler().Handle(new ResetPlayerDailyQuestsCommand(PlayerId), CancellationToken.None);

        Assert.Equal(QuestState.InProgress, progress.State);
        Assert.Equal(0, progress.Objectives.Single().Amount);
        Assert.Equal(Now, progress.StartedAt);
    }

    /// <summary>
    /// Пороги беруться з конфіга, а не з рядка в БД: вони могли змінитись
    /// між учорашнім і сьогоднішнім ресетом, і дейлік має стартувати
    /// з актуальними числами.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldTakeTargetsFromTheCurrentConfig()
    {
        var progress = GivenStaleDaily(required: 5);

        await Handler(dailyTarget: 12).Handle(
            new ResetPlayerDailyQuestsCommand(PlayerId), CancellationToken.None);

        Assert.Equal(12, progress.Objectives.Single().Required);
    }

    /// <summary>Забраний учора дейлік теж скидається — інакше він лишиться Claimed назавжди.</summary>
    [Fact]
    public async Task Handle_ShouldResetAClaimedQuest()
    {
        var progress = GivenStaleDaily(advanced: 5);
        progress.Claim(Now.AddDays(-1));

        await Handler().Handle(new ResetPlayerDailyQuestsCommand(PlayerId), CancellationToken.None);

        Assert.Equal(QuestState.InProgress, progress.State);
        Assert.Null(progress.ClaimedAt);
    }

    /// <summary>Нічого простроченого — нічого не зберігаємо.</summary>
    [Fact]
    public async Task Handle_ShouldNotSave_WhenNothingIsStale()
    {
        _quests.GetStaleDailyForPlayerAsync(PlayerId, Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([]);

        await Handler().Handle(new ResetPlayerDailyQuestsCommand(PlayerId), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Шукаються тільки дейліки: ланцюжкові квести скидати не можна,
    /// вони проходяться раз назавжди.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldOnlyLookForDailyQuestKeys()
    {
        GivenStaleDaily();

        await Handler().Handle(new ResetPlayerDailyQuestsCommand(PlayerId), CancellationToken.None);

        await _quests.Received(1).GetStaleDailyForPlayerAsync(
            PlayerId,
            Arg.Is<IReadOnlySet<string>>(keys => keys.Contains("daily_collect") && !keys.Contains("chain_intro")),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Межа — початок доби: усе, розпочате раніше, вважається простроченим.</summary>
    [Fact]
    public async Task Handle_ShouldUseTheStartOfTheDayAsCutoff()
    {
        GivenStaleDaily();

        await Handler().Handle(new ResetPlayerDailyQuestsCommand(PlayerId), CancellationToken.None);

        await _quests.Received(1).GetStaleDailyForPlayerAsync(
            PlayerId, Arg.Any<IReadOnlySet<string>>(), Now.Date, Arg.Any<CancellationToken>());
    }
}
