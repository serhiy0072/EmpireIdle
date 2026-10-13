using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Application.Shop.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Shop;

public class BuyShopItemCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
    private const string Essence = "hero_essence_t2";
    private const int Price = 300;

    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly PlayerWallet _wallet;

    public BuyShopItemCommandTests()
    {
        var player = new Player(_playerId, "tester", "tester@example.com", "user-1", Now);
        _players.GetByIdAsync(_playerId, Arg.Any<CancellationToken>()).Returns(player);

        _wallet = new PlayerWallet(Guid.NewGuid(), "user-1");
        _wallet.AddGems(new GemAmount(1_000), "test", Now);
        _wallets.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_wallet);

        _granter.RewardType.Returns("Item");
        _serverContext.ServerId.Returns(1);
    }

    /// <summary>Світ заданого рівня, що досяг його <paramref name="daysAgo"/> днів тому.</summary>
    private void GivenWorld(int level, int daysAgo)
    {
        var server = new Server(1, "Test", Now.AddDays(-daysAgo - 100));
        for (var i = 1; i < level - 1; i++)
            server.RaiseLevel(Now.AddDays(-daysAgo - 50));
        if (level > 1)
            server.RaiseLevel(Now.AddDays(-daysAgo));

        _servers.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(server);
    }

    private BuyShopItemCommandHandler Handler(int? windowLevel = null)
    {
        // Каталог не приймає конфіг без ратуші — беремо мінімальний із TestKit і додаємо крамницю
        var config = new GameConfigBuilder().WithResources().WithBuildings().Build();
        config.Items = [new ItemConfig { Key = Essence, DisplayName = "Essence", Description = "", Type = "evolution" }];
        config.Shop = new ShopConfig
        {
            Items = [new ShopItemConfig { ItemKey = Essence, PriceGems = Price, MaxPerPurchase = 3, ServerLevel = windowLevel, WindowDays = 14 }]
        };

        return new BuyShopItemCommandHandler(
            _players,
            _wallets,
            _servers,
            _serverContext,
            new GameCatalog(config),
            new RewardDispatcher([_granter]),
            _unitOfWork,
            new FakeTimeProvider(Now),
            NullLogger<BuyShopItemCommandHandler>.Instance);
    }

    private Task<int> Buy(string itemKey = Essence, int count = 1, int? windowLevel = null)
        => Handler(windowLevel).Handle(new BuyShopItemCommand(_playerId, itemKey, count), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldChargeGemsAndGrantTheItems_InOneTransaction()
    {
        RewardContext? granted = null;
        await _granter.GrantAsync(Arg.Do<RewardContext>(c => granted = c), Arg.Any<CancellationToken>());

        var balance = await Buy(count: 2);

        Assert.Equal(1_000 - Price * 2, balance);
        Assert.Equal(1_000 - Price * 2, _wallet.GemBalance.Value);
        Assert.NotNull(granted);
        Assert.Equal(Essence, granted!.Reward.Key);
        Assert.Equal(2, granted.Reward.Amount);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Gems вистачає на одну штуку з двох — покупка відхиляється цілком, нічого не списано.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenGemsCoverOnlyPartOfThePurchase()
    {
        _wallet.SpendGems(new GemAmount(1_000 - Price), "test", _playerId, Now);

        var error = await Assert.ThrowsAsync<NotEnoughResourcesException>(() => Buy(count: 2));

        Assert.Equal(Price * 2, error.Need);
        Assert.Equal(Price, _wallet.GemBalance.Value);
        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReject_MoreThanTheOfferAllowsPerPurchase()
    {
        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy(count: 4));
        Assert.Equal(RefusalReasons.ShopMaxPerPurchase.Key, refusal.Reason);

        Assert.Equal(1_000, _wallet.GemBalance.Value);
    }

    [Fact]
    public async Task Handle_ShouldReject_AnItemThatIsNotOnSale()
        => await Assert.ThrowsAsync<EntityNotFoundException>(() => Buy("teleport"));

    /// <summary>Предмет апу продається вікном після відкриття тіру (GDD §6.1): у вікні — купується.</summary>
    [Fact]
    public async Task Handle_ShouldSell_InsideTheWindow()
    {
        GivenWorld(level: 2, daysAgo: 3);

        var balance = await Buy(windowLevel: 2);

        Assert.Equal(1_000 - Price, balance);
    }

    /// <summary>Вікно минуло — відмова до списання: gems цілі, предмета немає.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_AfterTheWindow()
    {
        GivenWorld(level: 2, daysAgo: 20);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy(windowLevel: 2));

        Assert.Equal(RefusalReasons.ShopOfferClosed.Key, refusal.Reason);
        Assert.Equal(1_000, _wallet.GemBalance.Value);
        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Світ ще не дійшов до тіру — вікно не відкрилось.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_BeforeTheWorldReachesTheTier()
    {
        GivenWorld(level: 1, daysAgo: 3);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy(windowLevel: 2));

        Assert.Equal(RefusalReasons.ShopOfferClosed.Key, refusal.Reason);
    }
}
