using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Правило «новий герой / сузір'я / надлишок у джеми» живе в одному місці
/// саме тому, що його легко розсинхронізувати між джерелами видачі.
/// </summary>
public class HeroGranterTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const int ServerId = 1;

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();

    private static GameConfig Config() => new()
    {
        HeroSettings = new HeroesConfig
        {
            MaxConstellation = 6,
            OverflowGems = new Dictionary<string, int> { ["Common"] = 0, ["Rare"] = 15, ["Unique"] = 40 }
        },
        Heroes =
        [
            new HeroConfig { Key = "warrior_bran", Class = "warrior", Rank = Rarity.Common },
            new HeroConfig { Key = "mage_iselle", Class = "mage", Rank = Rarity.Unique }
        ]
    };

    private HeroGranter Granter()
    {
        _serverContext.ServerId.Returns(ServerId);

        return new HeroGranter(_heroes, _players, _wallets, _serverContext, new GameCatalog(Config()));
    }

    private PlayerWallet GivenWallet()
    {
        var player = new Player(Guid.NewGuid(), "user-1", "a@b.c", "tester", ServerId, Now);
        var wallet = new PlayerWallet(Guid.NewGuid(), "user-1");

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);
        _wallets.GetByUserIdAsync(player.UserId, Arg.Any<CancellationToken>()).Returns(wallet);

        return wallet;
    }

    [Fact]
    public async Task Grant_ShouldAddHero_WhenNotOwned()
    {
        await Granter().GrantAsync(PlayerId, "warrior_bran", "quest", Now);

        await _heroes.Received(1).AddAsync(
            Arg.Is<Hero>(h => h.HeroKey == "warrior_bran"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Grant_ShouldRaiseConstellation_WhenOwnedBelowTheCap()
    {
        var owned = new Hero(Guid.NewGuid(), PlayerId, ServerId, "mage_iselle", Now);
        _heroes.GetByKeyAsync(PlayerId, "mage_iselle", Arg.Any<CancellationToken>()).Returns(owned);

        await Granter().GrantAsync(PlayerId, "mage_iselle", "banner", Now);

        Assert.Equal(1, owned.Constellation);
    }

    /// <summary>
    /// На стелі дублікат стає джемами за рангом. Тихе поглинання означало б
    /// зникнення унікального дропу без сліду.
    /// </summary>
    [Fact]
    public async Task Grant_ShouldConvertToGems_AtTheConstellationCap()
    {
        var owned = new Hero(Guid.NewGuid(), PlayerId, ServerId, "mage_iselle", Now);

        for (var i = 0; i < 6; i++)
            owned.TryAddConstellation(6, Now);

        _heroes.GetByKeyAsync(PlayerId, "mage_iselle", Arg.Any<CancellationToken>()).Returns(owned);

        var wallet = GivenWallet();

        await Granter().GrantAsync(PlayerId, "mage_iselle", "banner", Now);

        Assert.Equal(40, wallet.GemBalance);
        Assert.Equal(6, owned.Constellation);
    }

    /// <summary>
    /// Ставка нуль означає «не конвертувати»: інакше звичайні герої
    /// відкрили б перегін золота в джеми.
    /// </summary>
    [Fact]
    public async Task Grant_ShouldNotTouchTheWallet_WhenTheRankConvertsToZero()
    {
        var owned = new Hero(Guid.NewGuid(), PlayerId, ServerId, "warrior_bran", Now);

        for (var i = 0; i < 6; i++)
            owned.TryAddConstellation(6, Now);

        _heroes.GetByKeyAsync(PlayerId, "warrior_bran", Arg.Any<CancellationToken>()).Returns(owned);

        await Granter().GrantAsync(PlayerId, "warrior_bran", "quest", Now);

        await _wallets.DidNotReceive().GetByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Героя, якого немає в каталозі, краще не створювати взагалі:
    /// рядок без стат і без картки полагодити важче, ніж упасти тут.
    /// </summary>
    [Fact]
    public async Task Grant_ShouldThrow_ForUnknownHero()
        => await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Granter().GrantAsync(PlayerId, "dragon_rider", "quest", Now));
}
