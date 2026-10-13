using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Зірки й універсальні осколки (GDD §6.1): частинка зірки — за осколки героя за зростаючою ціною;
/// універсальні йдуть у відкритого героя 1:1; обмін на вищу рідкість — коли всі герої рідкості прокачані.
/// </summary>
public class HeroStarTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GameCatalog _catalog = HeroTestConfig.Catalog();
    private readonly Dictionary<string, HeroShardProgress> _shards = new();
    private readonly Dictionary<string, PlayerItem> _items = new();
    private readonly List<Hero> _owned = [];

    public HeroStarTests()
    {
        _serverContext.ServerId.Returns(1);

        _heroes.GetShardsAsync(PlayerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _shards.GetValueOrDefault(call.ArgAt<string>(1)));
        _heroes.AddShardsAsync(Arg.Any<HeroShardProgress>(), Arg.Any<CancellationToken>())
            .Returns(call => { var p = call.Arg<HeroShardProgress>(); _shards[p.HeroKey] = p; return Task.CompletedTask; });
        _heroes.GetByKeyAsync(PlayerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _owned.FirstOrDefault(h => h.HeroKey == call.ArgAt<string>(1)));
        _heroes.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ => _owned.ToList());
        _inventory.GetItemAsync(PlayerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _items.GetValueOrDefault(call.ArgAt<string>(1)));
        _inventory.AddItemAsync(Arg.Any<PlayerItem>(), Arg.Any<CancellationToken>())
            .Returns(call => { var i = call.Arg<PlayerItem>(); _items[i.ItemKey] = i; return Task.CompletedTask; });
    }

    private HeroShardBank Bank() => new(_heroes, _inventory, _serverContext, _catalog);

    private Hero GivenHero(string key = "warrior_bran", int stars = 0)
    {
        var hero = TestKit.Entities.Hero(key, PlayerId, stars: stars);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        _owned.Add(hero);
        return hero;
    }

    private void GivenShards(string key, int count)
    {
        var progress = new HeroShardProgress(Guid.NewGuid(), PlayerId, 1, key);
        progress.Add(count);
        _shards[key] = progress;
    }

    private void GivenItems(string key, int count) => _items[key] = new PlayerItem(Guid.NewGuid(), PlayerId, key, count);

    private Task Advance(Hero hero)
        => new AdvanceHeroStarCommandHandler(_heroes, _unitOfWork, new HeroProgression(_catalog.Config.HeroSettings), _catalog,
                new FakeTimeProvider(Now), NullLogger<AdvanceHeroStarCommandHandler>.Instance)
            .Handle(new AdvanceHeroStarCommand(PlayerId, hero.Id), CancellationToken.None);

    private Task Convert(string heroKey, int count)
        => new ConvertUniversalShardsCommandHandler(_heroes, _inventory, Bank(), _unitOfWork, _catalog,
                NullLogger<ConvertUniversalShardsCommandHandler>.Instance)
            .Handle(new ConvertUniversalShardsCommand(PlayerId, heroKey, count), CancellationToken.None);

    private Task Upgrade(Rarity from, int count)
        => new UpgradeUniversalShardsCommandHandler(_heroes, _inventory, Bank(), _unitOfWork, _catalog,
                NullLogger<UpgradeUniversalShardsCommandHandler>.Instance)
            .Handle(new UpgradeUniversalShardsCommand(PlayerId, from, count), CancellationToken.None);

    // ---------- Зірки ----------

    /// <summary>Перша частинка першої зірки — 1 осколок; частинка заповнюється, решта осколків лишається.</summary>
    [Fact]
    public async Task Advance_ShouldFillAPart_ForItsShardCost()
    {
        var hero = GivenHero();
        GivenShards("warrior_bran", 5);

        await Advance(hero);

        Assert.Equal(1, hero.StarParts);
        Assert.Equal(4, _shards["warrior_bran"].Count);
    }

    /// <summary>П'ята зірка — по 40 за частинку: 39 осколків не вистачить, і нічого не списується.</summary>
    [Fact]
    public async Task Advance_ShouldRefuse_WithoutEnoughShards()
    {
        var hero = GivenHero(stars: 4);
        GivenShards("warrior_bran", 39);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Advance(hero));

        Assert.Equal(RefusalReasons.HeroNotEnoughShards.Key, refusal.Reason);
        Assert.Equal(24, hero.StarParts);
        Assert.Equal(39, _shards["warrior_bran"].Count);
    }

    [Fact]
    public async Task Advance_ShouldRefuse_WhenEveryStarIsFull()
    {
        var hero = GivenHero(stars: 5);
        GivenShards("warrior_bran", 1000);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Advance(hero));

        Assert.Equal(RefusalReasons.HeroMaxStars.Key, refusal.Reason);
    }

    // ---------- Універсальні осколки ----------

    /// <summary>Універсальні йдуть у відкритого героя своєї рідкості 1:1.</summary>
    [Fact]
    public async Task Convert_ShouldTurnUniversalShardsIntoTheHerosShards()
    {
        GivenHero();
        GivenItems(TestKit.UniversalShards.Common, 30);

        await Convert("warrior_bran", 20);

        Assert.Equal(10, _items[TestKit.UniversalShards.Common].Count);
        Assert.Equal(20, _shards["warrior_bran"].Count);
    }

    /// <summary>Лише для відкритого героя: призвати героя універсальними не можна.</summary>
    [Fact]
    public async Task Convert_ShouldRefuse_ALockedHero()
    {
        GivenItems(TestKit.UniversalShards.Common, 30);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Convert("warrior_bran", 10));

        Assert.Equal(RefusalReasons.HeroNotOwned.Key, refusal.Reason);
        Assert.Equal(30, _items[TestKit.UniversalShards.Common].Count);
    }

    /// <summary>Рідкість осколка — рідкість героя: звичайними не качають унікального.</summary>
    [Fact]
    public async Task Convert_ShouldSpendTheHerosRarity()
    {
        GivenHero(HeroTestConfig.UniqueHero);
        GivenItems(TestKit.UniversalShards.Common, 30);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() => Convert(HeroTestConfig.UniqueHero, 5));
    }

    // ---------- Обмін на вищу рідкість ----------

    /// <summary>Поки не всі звичайні герої відкриті й прокачані — обмін закритий: осколкам ще є куди йти.</summary>
    [Fact]
    public async Task Upgrade_ShouldRefuse_UntilEveryHeroOfTheRarityIsMaxed()
    {
        GivenHero("warrior_bran", stars: 4);
        GivenItems(TestKit.UniversalShards.Common, 500);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Upgrade(Rarity.Common, 1));

        Assert.Equal(RefusalReasons.ShardUpgradeLocked.Key, refusal.Reason);
        Assert.Equal(500, _items[TestKit.UniversalShards.Common].Count);
    }

    /// <summary>Усі звичайні прокачані — 100 звичайних універсальних стають 1 рідкісним.</summary>
    [Fact]
    public async Task Upgrade_ShouldTradeAHundredCommonsForARare_OnceEveryCommonHeroIsMaxed()
    {
        foreach (var hero in _catalog.Config.Heroes.Where(h => h.Rank == Rarity.Common))
            GivenHero(hero.Key, stars: 5);

        GivenItems(TestKit.UniversalShards.Common, 250);

        await Upgrade(Rarity.Common, 2);

        Assert.Equal(50, _items[TestKit.UniversalShards.Common].Count);
        Assert.Equal(2, _items[TestKit.UniversalShards.Rare].Count);
    }
}
