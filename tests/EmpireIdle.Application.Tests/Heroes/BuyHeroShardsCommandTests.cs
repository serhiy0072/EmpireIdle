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

public class BuyHeroShardsCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private BuyHeroShardsCommandHandler Handler() => new(
            _villages, _heroes, _unitOfWork, new FakeTimeProvider(Now),
            NullLogger<BuyHeroShardsCommandHandler>.Instance, HeroTestConfig.Catalog());


    /// <summary>Село із залою героїв і золотом.</summary>
    private Village GivenVillage(int gold = 10_000, bool hallUnderConstruction = false)
    {
        var catalog = HeroTestConfig.Catalog();
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["gold"], 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { ["gold"] = gold }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("heroeshall", catalog.Buildings, Now);

        if (hallUnderConstruction)
            village.Buildings.Single(b => b.Type == "heroeshall")
                .BeginUpgrade(catalog.Buildings["heroeshall"], TimeSpan.FromHours(1), Now,
                    ProductionBoost.None, locationMultiplier: 1.0);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);

        return village;
    }

    /// <summary>
    /// Підміна, що поводиться як сховище: доданий прогрес видно наступним
    /// читанням. Інакше неможливо перевірити накопичення між купівлями.
    /// </summary>
    private void GivenShardStore(HeroShardProgress? existing = null)
    {
        var stored = existing;

        _heroes.GetShardsAsync(PlayerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => stored);

        _heroes.AddShardsAsync(Arg.Any<HeroShardProgress>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                stored = call.Arg<HeroShardProgress>();
                return Task.CompletedTask;
            });
    }

    /// <summary>Золото списується за кожен уламок, а не за покупку.</summary>
    [Fact]
    public async Task Handle_ShouldChargeGoldPerShard()
    {
        var village = GivenVillage(gold: 1000);
        GivenShardStore();

        await Handler().Handle(new BuyHeroShardsCommand(PlayerId, "warrior_bran", 5), CancellationToken.None);

        // 5 × 100 = 500
        Assert.Equal(500, village.Resources.Single(r => r.ResourceType == "gold").Amount);
    }

    /// <summary>Перша купівля створює лічильник.</summary>
    [Fact]
    public async Task Handle_ShouldCreateProgress_OnTheFirstPurchase()
    {
        GivenVillage();
        GivenShardStore();

        await Handler().Handle(new BuyHeroShardsCommand(PlayerId, "warrior_bran", 3), CancellationToken.None);

        await _heroes.Received(1).AddShardsAsync(
            Arg.Is<HeroShardProgress>(p => p.HeroKey == "warrior_bran" && p.Count == 3),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Наступні купівлі додаються до наявного лічильника.</summary>
    [Fact]
    public async Task Handle_ShouldAccumulateIntoExistingProgress()
    {
        GivenVillage();

        var existing = new HeroShardProgress(Guid.NewGuid(), PlayerId, 1, "warrior_bran");
        existing.Add(4);
        GivenShardStore(existing);

        await Handler().Handle(new BuyHeroShardsCommand(PlayerId, "warrior_bran", 3), CancellationToken.None);

        Assert.Equal(7, existing.Count);
        await _heroes.DidNotReceive().AddShardsAsync(Arg.Any<HeroShardProgress>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Досягнення порогу героя не призиває: призов — окрема дія гравця.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotSummon_EvenAtTheThreshold()
    {
        GivenVillage();
        GivenShardStore();

        await Handler().Handle(new BuyHeroShardsCommand(PlayerId, "warrior_bran", 10), CancellationToken.None);

        await _heroes.DidNotReceive().AddAsync(Arg.Any<Hero>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// З банерів герої приходять цілими. Без цієї перевірки унікального
    /// можна було б купити за золото.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_ForHeroesAboveCommonRank()
    {
        GivenVillage();
        GivenShardStore();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new BuyHeroShardsCommand(PlayerId, "mage_iselle", 1), CancellationToken.None));
    }

    /// <summary>Без зали героїв купувати нема де.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheHallIsMissing()
    {
        var catalog = HeroTestConfig.Catalog();
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["gold"], 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { ["gold"] = 10_000 }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        GivenShardStore();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new BuyHeroShardsCommand(PlayerId, "warrior_bran", 1), CancellationToken.None));
    }

    /// <summary>
    /// Зала в процесі будівництва не рахується — інакше уламки
    /// купувались би наперед.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheHallIsUnderConstruction()
    {
        GivenVillage(hallUnderConstruction: true);
        GivenShardStore();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new BuyHeroShardsCommand(PlayerId, "warrior_bran", 1), CancellationToken.None));
    }

    /// <summary>Невідомий герой — 404, а не 500.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownHero()
    {
        GivenVillage();
        GivenShardStore();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new BuyHeroShardsCommand(PlayerId, "dragon_rider", 1), CancellationToken.None));
    }

    /// <summary>Нестача золота зупиняє операцію до нарахування уламків.</summary>
    [Fact]
    public async Task Handle_ShouldNotGrantShards_WhenGoldIsInsufficient()
    {
        GivenVillage(gold: 50);
        GivenShardStore();

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Handler().Handle(new BuyHeroShardsCommand(PlayerId, "warrior_bran", 5), CancellationToken.None));

        await _heroes.DidNotReceive().AddShardsAsync(Arg.Any<HeroShardProgress>(), Arg.Any<CancellationToken>());
    }
}
