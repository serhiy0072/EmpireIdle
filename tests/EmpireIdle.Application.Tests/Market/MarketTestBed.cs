using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Market;

/// <summary>
/// Спільне середовище тестів ринку: конфіг з увімкненим ринком, два гравці
/// з селами й гарнізонами, репозиторії-підміни, що пам'ятають додане.
/// </summary>
internal sealed class MarketTestBed
{
    public const string Market = "market";
    public const string Tonic = "tonic";
    public const int MarketOpensAt = 2;

    public static readonly DateTime Now = TestKit.Entities.Now;

    public readonly Guid Seller = Guid.NewGuid();
    public readonly Guid Buyer = Guid.NewGuid();

    public readonly IVillageRepository Villages = Substitute.For<IVillageRepository>();
    public readonly IMarketRepository MarketRepository = Substitute.For<IMarketRepository>();
    public readonly IInventoryRepository Inventory = Substitute.For<IInventoryRepository>();
    public readonly IHeroRepository Heroes = Substitute.For<IHeroRepository>();
    public readonly IGarrisonRepository Garrisons = Substitute.For<IGarrisonRepository>();
    public readonly IUnitOfWork UnitOfWork = Substitute.For<IUnitOfWork>();
    public readonly IServerContext ServerContext = Substitute.For<IServerContext>();

    /// <summary>Лоти, які обробники додали через AddAsync.</summary>
    public readonly List<MarketListing> Added = [];

    public GameCatalog Catalog { get; }
    public MarketPricing Pricing { get; }

    public Village SellerVillage { get; }
    public Village BuyerVillage { get; }
    public Garrison SellerGarrison { get; }
    public Garrison BuyerGarrison { get; }

    public MarketTestBed(int townHallLevel = 3, int gold = 10_000)
    {
        var config = new GameConfigBuilder().WithBuildings(Market).WithHeroes().WithEquipment().Build();

        config.Buildings.Single(b => b.Key == Market).RequiresMainBuildingLevel = MarketOpensAt;
        config.Items.Add(new ItemConfig { Key = Tonic, Type = "boost", DisplayName = "Тонік", Description = "t", Tradeable = true });
        config.Shop = new ShopConfig { Items = [new ShopItemConfig { ItemKey = Tonic, PriceGems = 5 }] };
        config.Market = new MarketConfig
        {
            BuildingKey = Market,
            GoldPerGem = 100,
            GoldPerPower = config.Heroes
                .Select(h => MarketPricing.CategoryOf(h.Rank))
                .Append("weapon")
                .Append("artifact")
                .Distinct()
                .ToDictionary(key => key, _ => 10.0)
        };

        Catalog = new GameCatalog(config);
        Pricing = new MarketPricing(config);

        ServerContext.ServerId.Returns(1);

        (SellerVillage, SellerGarrison) = GivenPlayer(Seller, townHallLevel, gold);
        (BuyerVillage, BuyerGarrison) = GivenPlayer(Buyer, townHallLevel, gold);

        MarketRepository.AddAsync(Arg.Do<MarketListing>(Added.Add), Arg.Any<CancellationToken>());
    }

    public HeroStats HeroStats => new(new HeroProgression(Catalog.Config.HeroSettings), Catalog);

    public MarketDesk Desk => new(MarketRepository, Pricing, new VillageStatus(Catalog), Catalog);

    public MarketGoods Goods => new(Inventory, Heroes, Villages, Garrisons,
        new ItemGranter(Inventory, ServerContext, Substitute.For<IRandomSource>(), new ArtifactRoller(Catalog.Config.Equipment)),
        HeroStats, Catalog);

    public MarketListingProjection Projection => new(Inventory, Heroes, Catalog);

    public int Gold(Village village) => village.Resources.Single(r => r.ResourceType == TestKeys.Gold).Amount;

    /// <summary>Меч продавця з 10 атаки — Power 10, коридор 70–130 золота.</summary>
    public EquipmentItem GivenSword(Guid? owner = null)
    {
        var sword = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon, owner ?? Seller, stats: [("Attack", 10.0)]);
        Inventory.GetEquipmentByIdAsync(sword.Id, Arg.Any<CancellationToken>()).Returns(sword);

        return sword;
    }

    public Hero GivenHero(Guid owner, string heroKey = TestKeys.CommonHero)
    {
        var garrison = owner == Seller ? SellerGarrison : BuyerGarrison;
        var hero = TestKit.Entities.Hero(heroKey, owner, garrison.Id, asLeader: false);

        Heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        Heroes.GetByKeyAsync(owner, heroKey, Arg.Any<CancellationToken>()).Returns(hero);

        return hero;
    }

    public PlayerItem GivenTonics(Guid owner, int count)
    {
        var stack = new PlayerItem(Guid.NewGuid(), owner, Tonic, count);
        Inventory.GetItemAsync(owner, Tonic, Arg.Any<CancellationToken>()).Returns(stack);

        return stack;
    }

    /// <summary>Активний лот, як його лишило б виставлення.</summary>
    public MarketListing GivenListing(MarketListingKind kind, Guid? equipmentId = null, Guid? heroId = null,
        string itemKey = TestKeys.Weapon, int quantity = 1, double units = 10, int price = 100)
    {
        var listing = new MarketListing(Guid.NewGuid(), 1, Seller, kind, equipmentId, heroId, itemKey, quantity, units,
            "weapon", price, taxGold: 5, Now, TimeSpan.FromHours(48));

        MarketRepository.GetByIdAsync(listing.Id, Arg.Any<CancellationToken>()).Returns(listing);

        return listing;
    }

    private (Village, Garrison) GivenPlayer(Guid playerId, int townHallLevel, int gold)
    {
        var village = new Village(Guid.NewGuid(), playerId, "Test", TestKeys.AllResources, 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { [TestKeys.Gold] = gold }, Now);
        village.AddBuilding(TestKeys.Townhall, Catalog.Buildings, Now);
        village.AddBuilding(Market, Catalog.Buildings, Now);

        TestKit.Entities.RaiseLevel(village.Buildings.Single(b => b.Type == TestKeys.Townhall),
            Catalog.Buildings[TestKeys.Townhall], townHallLevel - 1, Now);

        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        Villages.GetByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(village);
        Villages.GetByPlayerIdReadOnlyAsync(playerId, Arg.Any<CancellationToken>()).Returns(village);
        Garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return (village, garrison);
    }
}
