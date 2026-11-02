using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Queries;
using EmpireIdle.Application.Research.ReadModels;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Research;

/// <summary>Стан Лабораторії для екрана: дерево всіх тірів, ціна наступного рівня й активне дослідження.</summary>
public class GetResearchQueryTests
{
    private static readonly DateTime Now = Entities.Now;

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly GameConfig _config = new GameConfigBuilder().WithTechnologies().Build();
    private readonly Village _village = Entities.VillageWithTownhall(townhallLevel: 6);

    public GetResearchQueryTests()
    {
        _serverContext.ServerId.Returns(1);
        _servers.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Server(1, "World", Now));
        _villages.GetByPlayerIdReadOnlyAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_village);
    }

    private Task<ResearchView> Query(DateTime at)
    {
        var catalog = new GameCatalog(_config);

        return new GetResearchQueryHandler(_villages, _research, _servers, _serverContext,
                new PersonalTechnologies(_config.Technologies.Personal), new VillageStatus(catalog), catalog, new FakeTimeProvider(at))
            .Handle(new GetResearchQuery(_village.PlayerId), CancellationToken.None);
    }

    /// <summary>Два тіри × два вузли; перший вузол першого тіру відкритий і має ціну, другий тір закритий світом.</summary>
    [Fact]
    public async Task Handle_ShouldListEveryNodeOnEveryTier()
    {
        var view = await Query(Now);

        Assert.True(view.Unlocked);
        Assert.Equal(4, view.Nodes.Count);

        var first = view.Nodes.Single(n => n.Tier == 1 && n.Node == "attack_infantry");
        Assert.Equal(("Available", 900, 0), (first.Availability, first.NextSeconds, first.Level));
        Assert.Equal([true, false], view.Tiers.Select(t => t.Open));
        Assert.Null(view.Active);
    }

    /// <summary>Запит нічого не змінює, але дозріле дослідження показує вже вивченим, а не «ще 0 с».</summary>
    [Fact]
    public async Task Handle_ShouldShowARipeResearchAsLearned()
    {
        var research = new PlayerResearch(Guid.NewGuid(), _village.PlayerId, 1, Now);
        research.Start(new TechnologyKey(1, "attack_infantry"), TimeSpan.FromMinutes(15), Now);
        _research.GetByPlayerReadOnlyAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(research);

        var running = await Query(Now.AddMinutes(1));
        Assert.Equal(("attack_infantry", 1), (running.Active!.Node, running.Active.Level));

        var ripe = await Query(Now.AddMinutes(16));
        Assert.Null(ripe.Active);
        Assert.Equal(1, ripe.Nodes.Single(n => n.Tier == 1 && n.Node == "attack_infantry").Level);
        Assert.True(research.IsResearching);
    }
}
