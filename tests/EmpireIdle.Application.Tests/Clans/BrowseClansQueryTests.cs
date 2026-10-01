using EmpireIdle.Application.Clans.Queries;
using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Сторінки списку кланів: номер із URL не має переповнити OFFSET.</summary>
public class BrowseClansQueryTests
{
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();

    private BrowseClansQueryHandler Handler()
        => new(_clans, new GameCatalog(new GameConfigBuilder().WithBuildings().Build()));

    [Theory]
    [InlineData(int.MaxValue, BrowseClansQueryHandler.MaxPage)]
    [InlineData(-5, 1)]
    public async Task Handle_ShouldClampThePageNumber(int requested, int expected)
    {
        _clans.BrowseAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<ClanCard>(), 0));

        var page = await Handler().Handle(new BrowseClansQuery(Page: requested, PageSize: 20), CancellationToken.None);

        Assert.Equal(expected, page.Page);
        await _clans.Received(1).BrowseAsync(null, (expected - 1) * 20, 20, Arg.Any<CancellationToken>());
    }
}
