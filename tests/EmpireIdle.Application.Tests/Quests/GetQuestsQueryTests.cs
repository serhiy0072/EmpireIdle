using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Queries;
using EmpireIdle.Application.Quests.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Quests;

/// <summary>
/// Список квестів — лише читання (CQRS): кожен GET із UPDATE конфліктував би з outbox і
/// давав гравцю 409 на звичайному перегляді. Пороги рахуються в пам'яті.
/// </summary>
public class GetQuestsQueryTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IQuestRepository _quests = Substitute.For<IQuestRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Quests =
        [
            new QuestConfig
            {
                Key = "intro_townhall_3", Scope = QuestScope.Personal, Window = QuestWindow.Chain,
                Objectives =
                [
                    new QuestObjectiveConfig
                    {
                        Type = "BuildingUpgradeCompleted", Target = "townhall", Count = 3, Mode = ObjectiveMode.Threshold
                    }
                ]
            },
            new QuestConfig
            {
                Key = "next_step", Scope = QuestScope.Personal, Window = QuestWindow.Chain,
                Prerequisite = "intro_townhall_3",
                Objectives = [new QuestObjectiveConfig { Type = "BuildingCollected", Count = 5 }]
            }
        ]
    };

    private GetQuestsQueryHandler Handler() => new(
        _quests, new QuestThresholds(_quests, _villages, Substitute.For<IServerContext>()),
        new FakeTimeProvider(Now), new GameCatalog(Config()));

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
        _quests.GetAllAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(new List<QuestProgress>());
    }

    [Fact]
    public async Task Handle_ShouldShowAReachedThresholdAsCompleted_WithoutWritingAnything()
    {
        GivenTownHall(level: 4);

        var views = await Handler().Handle(new GetQuestsQuery(PlayerId), CancellationToken.None);

        var intro = views.Single(v => v.Key == "intro_townhall_3");
        Assert.Equal(QuestState.Completed, intro.State);
        Assert.Equal(4, intro.Objectives.Single().Amount);

        // Ланцюжок відкривається обчисленим завершенням
        Assert.Contains(views, v => v.Key == "next_step");

        await _quests.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ShouldKeepAnUnreachedThresholdInProgress()
    {
        GivenTownHall(level: 2);

        var views = await Handler().Handle(new GetQuestsQuery(PlayerId), CancellationToken.None);

        Assert.Equal(QuestState.InProgress, views.Single(v => v.Key == "intro_townhall_3").State);
        Assert.DoesNotContain(views, v => v.Key == "next_step");
    }
}
