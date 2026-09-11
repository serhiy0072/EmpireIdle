using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

public class StartHeroLevelUpCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private StartHeroLevelUpCommandHandler Handler() => new(
        _villages, _heroes, _unitOfWork, HeroTestConfig.Progression(), new FakeTimeProvider(Now),
        NullLogger<StartHeroLevelUpCommandHandler>.Instance, HeroTestConfig.Catalog());

    /// <summary>Село з ратушею заданого рівня й залою героїв.</summary>
    private Village GivenVillage(int townHallLevel = 10, int gold = 100_000, bool hallUnderConstruction = false)
    {
        var catalog = HeroTestConfig.Catalog();
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["gold"], 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { ["gold"] = gold }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("heroeshall", catalog.Buildings, Now);

        var townHall = village.Buildings.Single(b => b.Type == "townhall");

        for (var level = 1; level < townHallLevel; level++)
        {
            townHall.BeginUpgrade(catalog.Buildings["townhall"], TimeSpan.Zero, Now,
                ProductionBoost.None, locationMultiplier: 1.0);
            townHall.CompleteConstruction(Now);
        }

        if (hallUnderConstruction)
            village.Buildings.Single(b => b.Type == "heroeshall")
                .BeginUpgrade(catalog.Buildings["heroeshall"], TimeSpan.FromHours(1), Now,
                    ProductionBoost.None, locationMultiplier: 1.0);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);

        return village;
    }

    private Hero GivenHero(int level = 1, int tier = 1, HeroState state = HeroState.Idle)
    {
        var hero = new Hero(Guid.NewGuid(), PlayerId, ServerId, "warrior_bran", Now);

        for (var i = 1; i < level; i++)
            hero.GainLevel(level, Now);

        for (var i = 1; i < tier; i++)
            hero.EvolveTier(3, Now);

        if (state == HeroState.Deployed)
            hero.Deploy(Now);

        if (state == HeroState.Wounded)
            hero.Wound(Now.AddHours(1), Now);

        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        return hero;
    }

    /// <summary>Замовлення стає в чергу з часом, пропорційним цільовому рівню.</summary>
    [Fact]
    public async Task Handle_ShouldQueueOrderForTheNextLevel()
    {
        GivenVillage();
        var hero = GivenHero(level: 3);

        await Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None);

        await _heroes.Received(1).AddOrderAsync(
            Arg.Is<HeroLevelOrder>(o => o.HeroId == hero.Id && o.TargetLevel == 4
                && o.CompletesAt == Now.AddMinutes(16)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Вартість множиться на цільовий рівень, а не береться пласко.</summary>
    [Fact]
    public async Task Handle_ShouldChargeCostScaledByTargetLevel()
    {
        var village = GivenVillage(gold: 10_000);
        var hero = GivenHero(level: 4);

        await Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None);

        // 100 × 5 = 500
        Assert.Equal(9_500, village.Resources.Single(r => r.ResourceType == "gold").Amount);
    }

    /// <summary>
    /// Черга одна на гравця: зала героїв качає одного за раз, і без цієї
    /// перевірки пропускна здатність множилась би на кількість героїв.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenAnotherOrderIsActive()
    {
        GivenVillage();
        var hero = GivenHero();

        _heroes.GetActiveOrderAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(new HeroLevelOrder(Guid.NewGuid(), Guid.NewGuid(), PlayerId, ServerId, 2, Now.AddMinutes(5)));

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>
    /// Ратуша стелить рівень усіх героїв гравця. На стелі далі
    /// або ратуша, або еволюція тіру.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_AtTheTownHallCeiling()
    {
        GivenVillage(townHallLevel: 5);
        var hero = GivenHero(level: 5);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>Тір стелить окремо: T1 не переступає десятий рівень.</summary>
    [Fact]
    public async Task Handle_ShouldReject_AtTheTierCeiling()
    {
        GivenVillage(townHallLevel: 25);
        var hero = GivenHero(level: 10, tier: 1);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>Після еволюції той самий герой качається далі.</summary>
    [Fact]
    public async Task Handle_ShouldAllow_AboveTheTierCeiling_AfterEvolution()
    {
        GivenVillage(townHallLevel: 25);
        var hero = GivenHero(level: 10, tier: 2);

        await Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None);

        await _heroes.Received(1).AddOrderAsync(Arg.Any<HeroLevelOrder>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Героя в поході в залі немає — качати нема кого.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheHeroIsDeployed()
    {
        GivenVillage();
        var hero = GivenHero(state: HeroState.Deployed);

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>
    /// Поранений качається вільно: госпіталь забирає його з карти,
    /// але не з зали героїв.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldAllow_WhenTheHeroIsWounded()
    {
        GivenVillage();
        var hero = GivenHero(state: HeroState.Wounded);

        await Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None);

        await _heroes.Received(1).AddOrderAsync(Arg.Any<HeroLevelOrder>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Чужий герой не існує з погляду цього гравця.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForAnotherPlayersHero()
    {
        GivenVillage();

        var foreign = new Hero(Guid.NewGuid(), Guid.NewGuid(), ServerId, "warrior_bran", Now);
        _heroes.GetByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new StartHeroLevelUpCommand(PlayerId, foreign.Id), CancellationToken.None));
    }

    /// <summary>Зала в процесі будівництва не качає.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheHallIsUnderConstruction()
    {
        GivenVillage(hallUnderConstruction: true);
        var hero = GivenHero();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None));
    }

    /// <summary>Нестача ресурсів зупиняє операцію до постановки в чергу.</summary>
    [Fact]
    public async Task Handle_ShouldNotQueue_WhenResourcesAreInsufficient()
    {
        GivenVillage(gold: 50);
        var hero = GivenHero();

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Handler().Handle(new StartHeroLevelUpCommand(PlayerId, hero.Id), CancellationToken.None));

        await _heroes.DidNotReceive().AddOrderAsync(Arg.Any<HeroLevelOrder>(), Arg.Any<CancellationToken>());
    }
}
