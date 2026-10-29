using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Commands;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Research;

/// <summary>
/// Дослідження в Лабораторії (рішення 28.10.2026): старт із гейтами й ціною, одне дослідження за раз,
/// прискорення за gems. Фікстура TestKit: тір 1 — ратуша 5 і світ 1, тір 2 — ратуша 10 і світ 2.
/// </summary>
public class ResearchCommandTests
{
    private static readonly DateTime Now = Entities.Now;
    private const string UserId = "user-1";

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();

    private readonly GameConfig _config = new GameConfigBuilder().WithTechnologies().Build();
    private readonly Village _village = Entities.VillageWithTownhall(townhallLevel: 6);
    private PlayerResearch? _stored;

    public ResearchCommandTests()
    {
        _serverContext.ServerId.Returns(1);
        _servers.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Server(1, "World", Now));
        _villages.GetByPlayerIdAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_village);
        _research.GetByPlayerAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _stored);
        _research.GetByPlayerReadOnlyAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _stored);
        _research.When(r => r.AddAsync(Arg.Any<PlayerResearch>(), Arg.Any<CancellationToken>()))
            .Do(call => _stored = call.Arg<PlayerResearch>());
    }

    private long Food() => _village.Resources.Single(r => r.ResourceType == TestKeys.Food).Amount;

    private StartResearchCommandHandler Start()
    {
        var catalog = new GameCatalog(_config);
        var personal = new PersonalTechnologies(_config.Technologies.Personal);

        return new StartResearchCommandHandler(_villages, _research, _servers, _serverContext, _unitOfWork, personal,
            new PlayerTechnologies(_research, personal), new VillageStatus(catalog), catalog, new FakeTimeProvider(Now),
            NullLogger<StartResearchCommandHandler>.Instance);
    }

    private Task Research(int tier, string node)
        => Start().Handle(new StartResearchCommand(_village.PlayerId, tier, node), CancellationToken.None);

    private async Task<RequirementNotMetException> Refused(int tier, string node)
        => await Assert.ThrowsAsync<RequirementNotMetException>(() => Research(tier, node));

    /// <summary>
    /// Перший рівень вузла першого ряду: 15 хв, їжа 15 × 5 × опорна ратуша 5 = 375 → 400, золото 187,5 → 200.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldChargeTheLevel_AndStartTheTimer()
    {
        var foodBefore = Food();

        await Research(1, "attack_infantry");

        Assert.Equal(("1:attack_infantry", 1, Now.AddMinutes(15)), (_stored!.ActiveKey, _stored.ActiveLevel, _stored.CompletesAt));
        Assert.Equal(foodBefore - 400, Food());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhileTheLaboratoryIsClosed()
    {
        _config.Buildings.Single(b => b.Key == TestKeys.Laboratory).RequiresMainBuildingLevel = 9;

        Assert.Equal(RefusalReasons.ResearchLaboratoryLocked.Key, (await Refused(1, "attack_infantry")).Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenAnotherResearchRuns()
    {
        await Research(1, "attack_infantry");

        Assert.Equal(RefusalReasons.ResearchBusy.Key, (await Refused(1, "attack_infantry")).Reason);
    }

    /// <summary>Дозріле дослідження не тримає Лабораторію: наступне починається без чекання сканера.</summary>
    [Fact]
    public async Task Handle_ShouldFinishARipeResearch_BeforeStartingTheNext()
    {
        _stored = new PlayerResearch(Guid.NewGuid(), _village.PlayerId, 1, Now);
        _stored.Start(new TechnologyKey(1, "attack_infantry"), TimeSpan.Zero, Now);

        await Research(1, "command_infantry");

        Assert.Equal(1, _stored.LevelOf(new TechnologyKey(1, "attack_infantry")));
        Assert.Equal("1:command_infantry", _stored.ActiveKey);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WithoutAPrerequisiteLevel()
    {
        var refusal = await Refused(1, "command_infantry");

        Assert.Equal(RefusalReasons.ResearchPrerequisites.Key, refusal.Reason);
        Assert.Equal("attack_infantry", refusal.Args!["nodes"]);
    }

    /// <summary>Тір 2 чекає світу 2: ратуша є, а світ ще перший.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_ATierGatedByTheWorld()
    {
        Entities.RaiseLevel(_village.Buildings.Single(b => b.Type == TestKeys.Townhall),
            _config.Buildings.Single(b => b.Key == TestKeys.Townhall), 6, Now);

        Assert.Equal(RefusalReasons.ResearchTierLocked.Key, (await Refused(2, "attack_infantry")).Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ANodeWithoutResources()
    {
        var poor = Entities.VillageWithTownhall(townhallLevel: 6, resourceAmount: 100);
        _villages.GetByPlayerIdAsync(poor.PlayerId, Arg.Any<CancellationToken>()).Returns(poor);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Start().Handle(new StartResearchCommand(poor.PlayerId, 1, "attack_infantry"), CancellationToken.None));
    }

    /// <summary>Gems доводять дослідження до кінця й одразу записують рівень.</summary>
    [Fact]
    public async Task SpeedUp_ShouldSpendGems_AndFinishTheResearch()
    {
        await Research(1, "attack_infantry");

        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.GrantGems(new GemAmount(10_000), "seed", Now);
        _currentPlayer.UserId.Returns(UserId);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);

        var handler = new SpeedUpResearchCommandHandler(_research, _wallets, _currentPlayer, _unitOfWork,
            new SpeedUpCalculator(_config.Monetization), new FakeTimeProvider(Now), NullLogger<SpeedUpResearchCommandHandler>.Instance);

        await handler.Handle(new SpeedUpResearchCommand(_village.PlayerId), CancellationToken.None);

        Assert.True(wallet.GemBalance.Value < 10_000);
        Assert.Equal(1, _stored!.LevelOf(new TechnologyKey(1, "attack_infantry")));
        Assert.False(_stored.IsResearching);
    }

    [Fact]
    public async Task SpeedUp_ShouldRefuse_WhenNothingIsResearched()
    {
        var handler = new SpeedUpResearchCommandHandler(_research, _wallets, _currentPlayer, _unitOfWork,
            new SpeedUpCalculator(_config.Monetization), new FakeTimeProvider(Now), NullLogger<SpeedUpResearchCommandHandler>.Instance);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            handler.Handle(new SpeedUpResearchCommand(_village.PlayerId), CancellationToken.None));

        Assert.Equal(RefusalReasons.ResearchNotActive.Key, refusal.Reason);
    }
}
