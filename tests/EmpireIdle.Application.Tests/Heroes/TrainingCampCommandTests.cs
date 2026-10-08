using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Навчальний табір (GDD §6.1, рішення 07.10.2026): постановка й вихід, перезарядка слота,
/// оплата gems і перенесення рівня опорної п'ятірки на героїв у слотах.
/// </summary>
public class TrainingCampCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GameCatalog _catalog;
    private readonly TrainingCampRules _rules;
    private readonly List<Hero> _roster = [];
    private readonly PlayerWallet _wallet = new(Guid.NewGuid(), "user-1");
    private TrainingCamp? _camp;

    public TrainingCampCommandTests()
    {
        var config = HeroTestConfig.Create();
        config.HeroSettings.TrainingCamp = new TrainingCampConfig
        {
            ReferenceSize = 5,
            FreeSlotTownHallLevels = [1, 1],
            ExtraSlotPricesGems = [100],
            SlotCooldownHours = 24,
            SkipCooldownGems = 50,
        };

        _catalog = new GameCatalog(config);
        _rules = new TrainingCampRules(config.HeroSettings.TrainingCamp);

        _serverContext.ServerId.Returns(1);
        _heroes.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _roster.ToList());
        _heroes.GetCampAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _camp);
        _heroes.AddCampAsync(Arg.Any<TrainingCamp>(), Arg.Any<CancellationToken>())
            .Returns(call => { _camp = call.Arg<TrainingCamp>(); return Task.CompletedTask; });

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(TestKit.Entities.VillageWithTownhall(townhallLevel: 1));

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(new Player(PlayerId, "tester", "tester@example.com", "user-1", Now));
        _wallets.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_wallet);
        _wallet.AddGems(new GemAmount(1_000), "test", Now);
    }

    private Hero GivenHero(int level)
    {
        var hero = TestKit.Entities.Hero(HeroTestConfig.CommonHero, PlayerId, level: level);
        _roster.Add(hero);
        return hero;
    }

    private TrainingCampService Camps() => new(_heroes, _players, _wallets, _serverContext, _rules);

    private Task Place(Hero hero)
        => new PlaceHeroInCampCommandHandler(_heroes, _villages, new VillageStatus(_catalog), _rules, Camps(), _unitOfWork,
                new FakeTimeProvider(Now), NullLogger<PlaceHeroInCampCommandHandler>.Instance)
            .Handle(new PlaceHeroInCampCommand(PlayerId, hero.Id), CancellationToken.None);

    private Task Remove(Hero hero)
        => new RemoveHeroFromCampCommandHandler(_heroes, _rules, Camps(), _unitOfWork, new FakeTimeProvider(Now),
                NullLogger<RemoveHeroFromCampCommandHandler>.Instance)
            .Handle(new RemoveHeroFromCampCommand(PlayerId, hero.Id), CancellationToken.None);

    private Task Skip(int slot, DateTime? at = null)
        => new SkipCampCooldownCommandHandler(_rules, Camps(), _unitOfWork, new FakeTimeProvider(at ?? Now))
            .Handle(new SkipCampCooldownCommand(PlayerId, slot), CancellationToken.None);

    private Task Buy()
        => new BuyCampSlotCommandHandler(_rules, Camps(), _unitOfWork, new FakeTimeProvider(Now))
            .Handle(new BuyCampSlotCommand(PlayerId), CancellationToken.None);

    /// <summary>П'ятеро прокачаних задають рівень: найслабший із них — 30.</summary>
    private void GivenFive() => new[] { 50, 45, 40, 35, 30 }.ToList().ForEach(level => GivenHero(level));

    // ---------- Постановка ----------

    [Fact]
    public async Task Place_ShouldGiveTheHeroTheLevelOfTheWeakestOfTheFive()
    {
        GivenFive();
        var rookie = GivenHero(level: 1);

        await Place(rookie);

        Assert.Equal(0, rookie.CampSlot);
        Assert.Equal(30, rookie.EffectiveLevel);
        Assert.Equal(1, rookie.Level);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Без п'ятьох героїв поза табором табір недоступний — навіть для шостого.</summary>
    [Fact]
    public async Task Place_ShouldRefuse_WithoutFiveHeroesOutside()
    {
        foreach (var level in new[] { 40, 40, 40, 40 })
            GivenHero(level);
        var fifth = GivenHero(level: 1);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Place(fifth));

        Assert.Equal(RefusalReasons.CampUnavailable.Key, refusal.Reason);
        Assert.Equal(4, refusal.Args["have"]);
        Assert.Null(fifth.CampSlot);
    }

    [Fact]
    public async Task Place_ShouldRefuse_WhenEverySlotIsTaken()
    {
        GivenFive();
        var heroes = new[] { GivenHero(1), GivenHero(1), GivenHero(1) };

        await Place(heroes[0]);
        await Place(heroes[1]);
        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Place(heroes[2]));

        Assert.Equal(RefusalReasons.CampNoFreeSlot.Key, refusal.Reason);
    }

    /// <summary>Сильного поставили в табір — п'ятірка перерахувалась, і рівень інших у слотах упав.</summary>
    [Fact]
    public async Task Place_ShouldRecalculateTheOthers_WhenTheFiveChanges()
    {
        GivenFive();
        var rookie = GivenHero(level: 1);
        var sixth = GivenHero(level: 20);
        await Place(rookie);

        var strongest = _roster[0];
        await Place(strongest);

        // Тепер п'ятірка: 45, 40, 35, 30, 20
        Assert.Equal(20, rookie.EffectiveLevel);
        Assert.Equal(50, strongest.EffectiveLevel);
        Assert.Null(sixth.CampSlot);
    }

    // ---------- Вихід ----------

    [Fact]
    public async Task Remove_ShouldRestoreTheOwnLevel_AndCoolTheSlotDown()
    {
        GivenFive();
        var rookie = GivenHero(level: 1);
        await Place(rookie);

        await Remove(rookie);

        Assert.Null(rookie.CampSlot);
        Assert.Equal(1, rookie.EffectiveLevel);
        Assert.True(_camp!.IsCoolingDown(0, Now.AddHours(23)));
    }

    [Fact]
    public async Task Place_ShouldSkipACoolingSlot()
    {
        GivenFive();
        var first = GivenHero(level: 1);
        var second = GivenHero(level: 1);
        await Place(first);
        await Remove(first);

        await Place(second);

        Assert.Equal(1, second.CampSlot);
    }

    // ---------- Синхронізація ----------

    [Fact]
    public async Task Sync_ShouldLiftTheCampHeroes_WhenTheFiveLevelsUp()
    {
        GivenFive();
        var rookie = GivenHero(level: 1);
        await Place(rookie);

        _roster.Single(h => h.Level == 30).GainLevels(10, 80, Now);
        await Camps().SyncAsync(PlayerId, Now, CancellationToken.None);

        Assert.Equal(35, rookie.EffectiveLevel);
    }

    // ---------- Gems ----------

    [Fact]
    public async Task Skip_ShouldChargeGems_AndFreeTheSlot()
    {
        GivenFive();
        var rookie = GivenHero(level: 1);
        await Place(rookie);
        await Remove(rookie);

        await Skip(0);

        Assert.False(_camp!.IsCoolingDown(0, Now));
        Assert.Equal(950, _wallet.GemBalance.Value);
    }

    [Fact]
    public async Task Skip_ShouldNotCharge_ForAReadySlot()
    {
        GivenFive();
        var rookie = GivenHero(level: 1);
        await Place(rookie);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Skip(1));

        Assert.Equal(RefusalReasons.CampSlotReady.Key, refusal.Reason);
        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }

    [Fact]
    public async Task Buy_ShouldChargeTheSlotPrice_AndOpenTheSlot()
    {
        await Buy();

        Assert.Equal(1, _camp!.PurchasedSlots);
        Assert.Equal(900, _wallet.GemBalance.Value);
    }

    [Fact]
    public async Task Buy_ShouldRefuse_OnceEverySlotIsBought()
    {
        await Buy();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(Buy);

        Assert.Equal(RefusalReasons.CampAllSlotsBought.Key, refusal.Reason);
        Assert.Equal(900, _wallet.GemBalance.Value);
    }
}
