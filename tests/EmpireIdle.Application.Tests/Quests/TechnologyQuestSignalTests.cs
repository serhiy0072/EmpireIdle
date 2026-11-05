using EmpireIdle.Application.Quests.Tracking.Mappers;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;

namespace EmpireIdle.Application.Tests.Quests;

/// <summary>Сигнали квестів від технологій: вкладка як уточнення, кожна подія рахується за одну.</summary>
public class TechnologyQuestSignalTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private static ResearchCompletedMapper ResearchMapper()
        => new(new PersonalTechnologies(new GameConfigBuilder().WithTechnologies().Build().Technologies.Personal));

    [Fact]
    public async Task ResearchCompleted_ShouldCarryTheTabOfTheNode()
    {
        var signal = await ResearchMapper().MapAsync(new ResearchCompleted(PlayerId, "1:attack_infantry", 2, Now), CancellationToken.None);

        Assert.NotNull(signal);
        Assert.Equal(PlayerId, signal.PlayerId);
        Assert.Equal(nameof(ResearchCompleted), signal.EventType);
        Assert.Equal("battle", signal.Target);
        Assert.Equal(1, signal.Increment);
    }

    /// <summary>Вузол прибрали з конфіга після дослідження: сигнал без вкладки, а не падіння обробки outbox.</summary>
    [Fact]
    public async Task ResearchCompleted_ShouldHaveNoTarget_ForAnUnknownNode()
    {
        var signal = await ResearchMapper().MapAsync(new ResearchCompleted(PlayerId, "1:removed_node", 1, Now), CancellationToken.None);

        Assert.NotNull(signal);
        Assert.Null(signal.Target);
    }

    /// <summary>Квест рахує кількість підношень: gems-підношення дає більше очок, але лишається одним.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    public async Task ClanOfferingMade_ShouldCountOnce_WhateverThePoints(int points)
    {
        var signal = await new ClanOfferingMadeMapper().MapAsync(new ClanOfferingMade(PlayerId, Guid.NewGuid(), points, Now), CancellationToken.None);

        Assert.NotNull(signal);
        Assert.Equal(nameof(ClanOfferingMade), signal.EventType);
        Assert.Null(signal.Target);
        Assert.Equal(1, signal.Increment);
    }
}
