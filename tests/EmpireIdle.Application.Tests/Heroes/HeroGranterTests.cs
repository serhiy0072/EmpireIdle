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
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();

    private HeroGranter Granter()
    {
        _serverContext.ServerId.Returns(ServerId);

        // Видача оселяє героя в гарнізоні, тож село й гарнізон мусять існувати,
        // інакше granter кине ще до перевірки правила видачі
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0, ServerId);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, ServerId);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return new HeroGranter(_heroes, _players, _wallets, _villages, _garrisons, _serverContext,
            HeroTestConfig.Catalog());
    }

    private PlayerWallet GivenWallet()
    {
        var player = new Player(Guid.NewGuid(), "tester", "a@b.c", "user-1", Now, ServerId);
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
        var owned = new Hero(Guid.NewGuid(), PlayerId, ServerId, "mage_iselle", Guid.NewGuid(), asLeader: true, Now);
        _heroes.GetByKeyAsync(PlayerId, "mage_iselle", Arg.Any<CancellationToken>()).Returns(owned);

        await Granter().GrantAsync(PlayerId, "mage_iselle", "banner", Now);

        Assert.Equal(1, owned.Constellation);
    }

    /// <summary>
    /// На стелі дублікат стає печатками призову за рангом. Тихе поглинання означало б
    /// зникнення унікального дропу без сліду.
    /// </summary>
    [Fact]
    public async Task Grant_ShouldConvertToSeals_AtTheConstellationCap()
    {
        var owned = new Hero(Guid.NewGuid(), PlayerId, ServerId, "mage_iselle", Guid.NewGuid(), asLeader: true, Now);

        for (var i = 0; i < 6; i++)
            owned.TryAddConstellation(6, Now);

        _heroes.GetByKeyAsync(PlayerId, "mage_iselle", Arg.Any<CancellationToken>()).Returns(owned);

        var wallet = GivenWallet();

        await Granter().GrantAsync(PlayerId, "mage_iselle", "banner", Now);

        Assert.Equal(40, wallet.SealBalance);
        Assert.Equal(6, owned.Constellation);
    }

    /// <summary>
    /// Ставка нуль означає «не конвертувати»: інакше звичайні герої
    /// відкрили б перегін золота в джеми.
    /// </summary>
    [Fact]
    public async Task Grant_ShouldNotTouchTheWallet_WhenTheRankConvertsToZero()
    {
        var owned = new Hero(Guid.NewGuid(), PlayerId, ServerId, "warrior_bran", Guid.NewGuid(), asLeader: true, Now);

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
