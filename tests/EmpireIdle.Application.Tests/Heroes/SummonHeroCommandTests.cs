using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>Призов за осколки (GDD §6.1): 10 осколків — герой, будь-якої рідкості.</summary>
public class SummonHeroCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();

    private SummonHeroCommandHandler Handler(GameCatalog? catalog = null, int serverLevel = 1)
    {
        var resolved = catalog ?? HeroTestConfig.Catalog();
        _serverContext.ServerId.Returns(ServerId);
        _servers.GetLevelAsync(ServerId, Arg.Any<CancellationToken>()).Returns(serverLevel);

        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0, ServerId);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, ServerId);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        var granter = new HeroGranter(_heroes, new HeroShardBank(_heroes, _inventory, _serverContext, resolved),
            _villages, _garrisons, _serverContext, resolved);

        return new SummonHeroCommandHandler(_heroes, _servers, _serverContext, granter, _unitOfWork,
            new FakeTimeProvider(Now), NullLogger<SummonHeroCommandHandler>.Instance, resolved);
    }

    private HeroShardProgress GivenShards(string heroKey, int count)
    {
        var progress = new HeroShardProgress(Guid.NewGuid(), PlayerId, ServerId, heroKey);

        if (count > 0)
            progress.Add(count);

        _heroes.GetShardsAsync(PlayerId, heroKey, Arg.Any<CancellationToken>()).Returns(progress);

        return progress;
    }

    [Fact]
    public async Task Handle_ShouldAddHero_WhenShardsSuffice()
    {
        GivenShards("warrior_bran", 10);

        await Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None);

        await _heroes.Received(1).AddAsync(
            Arg.Is<Hero>(h => h.HeroKey == "warrior_bran" && h.Level == 1 && h.Tier == 1),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Рідкісні й унікальні тепер теж збираються з осколків — ціна та сама.</summary>
    [Fact]
    public async Task Handle_ShouldSummonAUniqueHero_FromShardsToo()
    {
        GivenShards(HeroTestConfig.UniqueHero, 10);

        await Handler().Handle(new SummonHeroCommand(PlayerId, HeroTestConfig.UniqueHero), CancellationToken.None);

        await _heroes.Received(1).AddAsync(Arg.Is<Hero>(h => h.HeroKey == HeroTestConfig.UniqueHero), Arg.Any<CancellationToken>());
    }

    /// <summary>Списується рівно ціна призову, надлишок лишається гравцю.</summary>
    [Fact]
    public async Task Handle_ShouldSpendExactlyTheThreshold()
    {
        var progress = GivenShards("warrior_bran", 13);

        await Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None);

        Assert.Equal(3, progress.Count);
    }

    /// <summary>Нижче порогу — відмова без списання.</summary>
    [Fact]
    public async Task Handle_ShouldReject_AndKeepShards_WhenBelowTheThreshold()
    {
        var progress = GivenShards("warrior_bran", 9);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None));
        Assert.Equal(RefusalReasons.HeroNotEnoughShards.Key, refusal.Reason);

        Assert.Equal(9, progress.Count);
        await _heroes.DidNotReceive().AddAsync(Arg.Any<Hero>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenNoShardsCollected()
    {
        _heroes.GetShardsAsync(PlayerId, "warrior_bran", Arg.Any<CancellationToken>()).Returns((HeroShardProgress?)null);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None));
        Assert.Equal(RefusalReasons.HeroNotEnoughShards.Key, refusal.Reason);
    }

    /// <summary>Герой тіру 2 — лише зі світу 2, хоч би звідки взялися осколки (GDD §6.1).</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_AHeroAboveTheWorldLevel()
    {
        var config = HeroTestConfig.Create();
        config.Heroes.Single(h => h.Key == "warrior_bran").NativeTier = 2;
        var progress = GivenShards("warrior_bran", 10);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler(new GameCatalog(config), serverLevel: 1).Handle(new SummonHeroCommand(PlayerId, "warrior_bran"), CancellationToken.None));

        Assert.Equal(RefusalReasons.HeroTierLocked.Key, refusal.Reason);
        Assert.Equal(10, progress.Count);
    }

    /// <summary>Невідомий герой — 404, а не 500.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownHero()
        => await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new SummonHeroCommand(PlayerId, "dragon_rider"), CancellationToken.None));
}
