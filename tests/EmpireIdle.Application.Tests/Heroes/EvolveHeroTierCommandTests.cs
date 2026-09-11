using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

public class EvolveHeroTierCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private EvolveHeroTierCommandHandler Handler()
    {
        _serverContext.ServerId.Returns(ServerId);

        return new EvolveHeroTierCommandHandler(
            _heroes, _inventory, _servers, _serverContext, _unitOfWork,
            HeroTestConfig.Progression(), new FakeTimeProvider(Now),
            NullLogger<EvolveHeroTierCommandHandler>.Instance, HeroTestConfig.Catalog());
    }

    /// <summary>Світ заданого рівня. Новий сервер завжди починає з першого.</summary>
    private void GivenServer(int level)
    {
        var server = new Server(ServerId, "Test World", Now);

        for (var current = 1; current < level; current++)
            server.RaiseLevel(maxLevel: 10, Now);

        _servers.GetByIdAsync(ServerId, Arg.Any<CancellationToken>()).Returns(server);
    }

    private Hero GivenHero(int tier = 1, HeroState state = HeroState.Idle)
    {
        var hero = new Hero(Guid.NewGuid(), PlayerId, ServerId, "warrior_bran", Now);

        for (var i = 1; i < tier; i++)
            hero.EvolveTier(3, Now);

        if (state == HeroState.Deployed)
            hero.Deploy(Now);

        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        return hero;
    }

    private PlayerItem GivenEssence(string key, int count = 1)
    {
        var item = new PlayerItem(Guid.NewGuid(), PlayerId, key, count);

        _inventory.GetItemAsync(PlayerId, key, Arg.Any<CancellationToken>()).Returns(item);

        return item;
    }

    /// <summary>Тір росте, предмет списується.</summary>
    [Fact]
    public async Task Handle_ShouldRaiseTierAndConsumeTheEssence()
    {
        GivenServer(level: 2);
        var hero = GivenHero(tier: 1);
        var essence = GivenEssence("hero_essence_t2", count: 3);

        await Handler().Handle(new EvolveHeroTierCommand(PlayerId, hero.Id), CancellationToken.None);

        Assert.Equal(2, hero.Tier);
        Assert.Equal(2, essence.Count);
    }

    /// <summary>
    /// Тір прив'язаний до рівня світу: контент відкривається для всіх
    /// одночасно, не для найшвидших.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheWorldIsTooYoung()
    {
        GivenServer(level: 1);
        var hero = GivenHero(tier: 1);
        GivenEssence("hero_essence_t2");

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new EvolveHeroTierCommand(PlayerId, hero.Id), CancellationToken.None));

        Assert.Equal(1, hero.Tier);
    }

    /// <summary>Третій тір потребує третього рівня світу, не другого.</summary>
    [Fact]
    public async Task Handle_ShouldGateEachTierOnItsOwnWorldLevel()
    {
        GivenServer(level: 2);
        var hero = GivenHero(tier: 2);
        GivenEssence("hero_essence_t3");

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new EvolveHeroTierCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>Кожен перехід вимагає свого предмета.</summary>
    [Fact]
    public async Task Handle_ShouldRequireTheEssenceMatchingTheCurrentTier()
    {
        GivenServer(level: 3);
        var hero = GivenHero(tier: 2);
        GivenEssence("hero_essence_t2");

        _inventory.GetItemAsync(PlayerId, "hero_essence_t3", Arg.Any<CancellationToken>())
            .Returns((PlayerItem?)null);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new EvolveHeroTierCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>Без предмета тір не піднімається.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheEssenceIsMissing()
    {
        GivenServer(level: 2);
        var hero = GivenHero(tier: 1);

        _inventory.GetItemAsync(PlayerId, "hero_essence_t2", Arg.Any<CancellationToken>())
            .Returns((PlayerItem?)null);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new EvolveHeroTierCommand(PlayerId, hero.Id), CancellationToken.None));

        Assert.Equal(1, hero.Tier);
    }

    /// <summary>На найвищому тірі предмет не має згорати.</summary>
    [Fact]
    public async Task Handle_ShouldReject_AtTheHighestTier()
    {
        GivenServer(level: 3);
        var hero = GivenHero(tier: 3);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new EvolveHeroTierCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>Героя в поході не еволюціонують.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheHeroIsDeployed()
    {
        GivenServer(level: 2);
        var hero = GivenHero(tier: 1, state: HeroState.Deployed);
        GivenEssence("hero_essence_t2");

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new EvolveHeroTierCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>Чужий герой не існує з погляду цього гравця.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForAnotherPlayersHero()
    {
        GivenServer(level: 2);

        var foreign = new Hero(Guid.NewGuid(), Guid.NewGuid(), ServerId, "warrior_bran", Now);
        _heroes.GetByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new EvolveHeroTierCommand(PlayerId, foreign.Id), CancellationToken.None));
    }
}
