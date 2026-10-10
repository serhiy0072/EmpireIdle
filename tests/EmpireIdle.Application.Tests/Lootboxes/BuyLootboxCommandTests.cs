using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Commands;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Lootboxes.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Application.Shop.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Lootboxes;

/// <summary>Покупка скрині в крамниці (GDD §9.12): списання gems і відкриття одразу, без рюкзака.</summary>
public class BuyLootboxCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private const int Price = 300;
    private const string Trinket = "test_trinket";

    private readonly Guid _playerId = Guid.NewGuid();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly PlayerWallet _wallet = new(Guid.NewGuid(), "user-1");
    private readonly GameConfig _config;

    public BuyLootboxCommandTests()
    {
        _players.GetByIdAsync(_playerId, Arg.Any<CancellationToken>())
            .Returns(new Player(_playerId, "tester", "tester@example.com", "user-1", Now));
        _wallets.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_wallet);
        _wallet.AddGems(new GemAmount(1_000), "test", Now);
        _granter.RewardType.Returns("Resource");

        _config = new GameConfigBuilder()
            .WithLootboxes(boxes => boxes.Single(b => b.Key == TestKeys.SmallChest).Entries.RemoveAll(e => e.Type != "Resource"))
            .Build();

        _config.Shop.Items =
        [
            new ShopItemConfig { ItemKey = TestKeys.SmallChest, PriceGems = Price, MaxPerPurchase = 3 },
            new ShopItemConfig { ItemKey = Trinket, PriceGems = 1 }
        ];
        _config.Items.Add(new ItemConfig { Key = Trinket, Type = "evolution", DisplayName = "Дрібничка", Description = "t" });
    }

    private Task<LootboxPurchase> Buy(string key = TestKeys.SmallChest, int count = 1)
    {
        var catalog = new GameCatalog(_config);
        var opener = new LootboxOpener(Substitute.For<ILootboxRepository>(), Substitute.For<IVillageRepository>(),
            new RewardDispatcher([_granter]), catalog, Substitute.For<IRandomSource>());
        var checkout = new ShopCheckout(_players, _wallets, Substitute.For<IServerRepository>(), Substitute.For<IServerContext>(), catalog);

        return new BuyLootboxCommandHandler(checkout, opener, catalog, _unitOfWork, new FakeTimeProvider(Now),
                NullLogger<BuyLootboxCommandHandler>.Instance)
            .Handle(new BuyLootboxCommand(_playerId, key, count), CancellationToken.None);
    }

    /// <summary>Дві скрині: gems списано, обидві відкриті, у рюкзак нічого не лягло.</summary>
    [Fact]
    public async Task Handle_ShouldChargeAndOpenRightAway()
    {
        var purchase = await Buy(count: 2);

        Assert.Equal(1_000 - Price * 2, purchase.GemBalance);
        Assert.Equal(2, purchase.Drops.Count);
        await _granter.Received(2).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _inventory.DidNotReceive().AddItemAsync(Arg.Any<PlayerItem>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Gems не вистачає — нічого не відкрито й не списано.</summary>
    [Fact]
    public async Task Handle_ShouldOpenNothing_WhenGemsRunShort()
    {
        _wallet.SpendGems(new GemAmount(200), "test", _playerId, Now);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() => Buy(count: 3));

        Assert.Equal(800, _wallet.GemBalance.Value);
        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRespectTheOfferLimit()
    {
        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy(count: 4));

        Assert.Equal(RefusalReasons.ShopMaxPerPurchase.Key, refusal.Reason);
        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }

    /// <summary>Звичайний товар цим шляхом не купується — і gems лишаються.</summary>
    [Fact]
    public async Task Handle_ShouldRefuseAnOfferThatIsNotALootbox()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => Buy(Trinket));

        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }
}
