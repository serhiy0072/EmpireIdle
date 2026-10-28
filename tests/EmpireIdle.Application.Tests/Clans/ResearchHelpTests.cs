using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>
/// Допомога клану з дослідженням (рішення 28.10.2026): своя частка й ліміт — 0,5% повної тривалості
/// за допомогу до 50 допомог, а не 2% до 20, як у будівництва.
/// </summary>
public class ResearchHelpTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid HelperId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanHelpRepository _help = Substitute.For<IClanHelpRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Help_ShouldCutHalfAPercentOfTheResearch_UnderItsOwnCap()
    {
        var config = new GameConfigBuilder().WithTechnologies().Build();

        var clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", OwnerId, Now);
        clan.Join(HelperId, capacity: 50, Now);
        _clans.GetByMemberAsync(HelperId, Arg.Any<CancellationToken>()).Returns(clan);
        _villages.GetByPlayerIdAsync(OwnerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall());

        var research = new PlayerResearch(Guid.NewGuid(), OwnerId, 1, Now);
        research.Start(new TechnologyKey(1, "attack_infantry"), TimeSpan.FromHours(10), Now);
        _research.GetByPlayerAsync(OwnerId, Arg.Any<CancellationToken>()).Returns(research);

        var request = new ClanHelpRequest(Guid.NewGuid(), 1, clan.Id, OwnerId, ClanHelpTarget.Research, research.Id,
            research.PlannedDuration, Now.AddHours(10), Now);
        _help.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var handler = new GiveClanHelpCommandHandler(_clans, _help, _villages, _garrisons, _research, _unitOfWork,
            new GameCatalog(config), new FakeTimeProvider(Now), NullLogger<GiveClanHelpCommandHandler>.Instance);

        await handler.Handle(new GiveClanHelpCommand(HelperId, request.Id), CancellationToken.None);

        // 10 годин × 0,5% = 3 хвилини
        Assert.Equal(TimeSpan.FromHours(10) - TimeSpan.FromMinutes(3), research.Remaining(Now));
        Assert.Equal(1, request.HelpCount);
    }
}
