using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Правило «новий герой — у ростер, дублікат — осколками» (GDD §6.1) живе в одному місці
/// саме тому, що його легко розсинхронізувати між джерелами видачі.
/// </summary>
public class HeroGranterTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly Dictionary<string, HeroShardProgress> _shards = new();
    private readonly Dictionary<string, PlayerItem> _items = new();

    public HeroGranterTests()
    {
        _serverContext.ServerId.Returns(ServerId);

        // Підміни, що поводяться як сховища: додане видно наступним читанням
        _heroes.GetShardsAsync(PlayerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _shards.GetValueOrDefault(call.ArgAt<string>(1)));
        _heroes.AddShardsAsync(Arg.Any<HeroShardProgress>(), Arg.Any<CancellationToken>())
            .Returns(call => { var p = call.Arg<HeroShardProgress>(); _shards[p.HeroKey] = p; return Task.CompletedTask; });
        _inventory.GetItemAsync(PlayerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _items.GetValueOrDefault(call.ArgAt<string>(1)));
        _inventory.AddItemAsync(Arg.Any<PlayerItem>(), Arg.Any<CancellationToken>())
            .Returns(call => { var i = call.Arg<PlayerItem>(); _items[i.ItemKey] = i; return Task.CompletedTask; });
    }

    private HeroGranter Granter(GameCatalog? catalog = null)
    {
        var resolved = catalog ?? HeroTestConfig.Catalog();

        // Видача оселяє героя в гарнізоні, тож село й гарнізон мусять існувати,
        // інакше granter кине ще до перевірки правила видачі
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0, ServerId);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, ServerId);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return new HeroGranter(_heroes, new HeroShardBank(_heroes, _inventory, _serverContext, resolved),
            _villages, _garrisons, _serverContext, resolved);
    }

    private Hero GivenOwned(string heroKey, int stars = 0)
    {
        var owned = TestKit.Entities.Hero(heroKey, PlayerId, stars: stars);
        _heroes.GetByKeyAsync(PlayerId, heroKey, Arg.Any<CancellationToken>()).Returns(owned);
        return owned;
    }

    [Fact]
    public async Task Grant_ShouldAddHero_WhenNotOwned()
    {
        await Granter().GrantAsync(PlayerId, "warrior_bran", "quest", Now);

        await _heroes.Received(1).AddAsync(
            Arg.Is<Hero>(h => h.HeroKey == "warrior_bran"), Arg.Any<CancellationToken>());
    }

    /// <summary>Дублікат — це 10 осколків на зірки (ціна призову), а не другий рядок героя.</summary>
    [Fact]
    public async Task Grant_ShouldTurnADuplicateIntoShards()
    {
        GivenOwned("mage_iselle");

        await Granter().GrantAsync(PlayerId, "mage_iselle", "banner", Now);

        Assert.Equal(10, _shards["mage_iselle"].Count);
        await _heroes.DidNotReceive().AddAsync(Arg.Any<Hero>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Прокачаному до кінця героєві осколки нічого не дадуть — вони стають універсальними
    /// його рідкості, а не зникають без сліду.
    /// </summary>
    [Fact]
    public async Task Grant_ShouldTurnADuplicateOfAFullyStarredHeroIntoUniversalShards()
    {
        GivenOwned("mage_iselle", stars: 6);

        await Granter().GrantAsync(PlayerId, "mage_iselle", "banner", Now);

        Assert.Equal(10, _items[TestKit.UniversalShards.Unique].Count);
        Assert.False(_shards.ContainsKey("mage_iselle"));
    }

    /// <summary>
    /// Героя, якого немає в каталозі, краще не створювати взагалі:
    /// рядок без стат і без картки полагодити важче, ніж упасти тут.
    /// </summary>
    [Fact]
    public async Task Grant_ShouldThrow_ForUnknownHero()
        => await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Granter().GrantAsync(PlayerId, "dragon_rider", "quest", Now));

    /// <summary>Герой приходить у своєму рідному тірі (GDD §6.1) — без апу й без штрафу за ап.</summary>
    [Fact]
    public async Task Grant_ShouldCreateTheHero_InItsNativeTier()
    {
        var config = HeroTestConfig.Create();
        config.Heroes.Single(h => h.Key == "warrior_bran").NativeTier = 2;

        await Granter(new GameCatalog(config)).GrantAsync(PlayerId, "warrior_bran", "quest", Now);

        await _heroes.Received(1).AddAsync(
            Arg.Is<Hero>(h => h.Tier == 2 && h.NativeTier == 2), Arg.Any<CancellationToken>());
    }
}
