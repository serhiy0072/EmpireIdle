using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Shop.Commands;
using EmpireIdle.Application.Tests.Heroes;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Shop;

/// <summary>
/// Шматки зброї звичайних і рідкісних героїв за gems (GDD §6.4, §9.12): ціна за рідкістю героя,
/// унікальні не продаються. Шматки лягають на героя, gems списуються разом.
/// </summary>
public class BuyHeroWeaponShardsCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PlayerWallet _wallet = new(Guid.NewGuid(), "user-1");
    private readonly GameCatalog _catalog;

    public BuyHeroWeaponShardsCommandTests()
    {
        var config = HeroTestConfig.Create();
        config.HeroSettings.WeaponShardPriceGems = new() { [Rarity.Common] = 250, [Rarity.Rare] = 750 };
        _catalog = new GameCatalog(config);

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(new Player(PlayerId, "tester", "tester@example.com", "user-1", Now));
        _wallets.GetByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_wallet);
    }

    private Hero GivenHero(string heroKey)
    {
        var hero = TestKit.Entities.Hero(heroKey, PlayerId);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        return hero;
    }

    private Task<int> Buy(Hero hero, int count)
        => new BuyHeroWeaponShardsCommandHandler(_heroes, _players, _wallets, _catalog, _unitOfWork,
                new FakeTimeProvider(Now), NullLogger<BuyHeroWeaponShardsCommandHandler>.Instance)
            .Handle(new BuyHeroWeaponShardsCommand(PlayerId, hero.Id, count), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldChargeByHeroRarity_AndAddTheShards()
    {
        _wallet.AddGems(new GemAmount(1_000), "test", Now);
        var hero = GivenHero(HeroTestConfig.CommonHero);

        var balance = await Buy(hero, 3);

        Assert.Equal(250, balance);
        Assert.Equal(3, hero.WeaponShards);
    }

    /// <summary>Унікальні шматки — лише зі скринь зброї: відмова, gems на місці.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_ForAUniqueHero()
    {
        _wallet.AddGems(new GemAmount(10_000), "test", Now);
        var hero = GivenHero(HeroTestConfig.UniqueHero);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy(hero, 1));

        Assert.Equal(RefusalReasons.HeroWeaponNotSold.Key, refusal.Reason);
        Assert.Equal(10_000, _wallet.GemBalance.Value);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WithoutEnoughGems()
    {
        _wallet.AddGems(new GemAmount(400), "test", Now);
        var hero = GivenHero(HeroTestConfig.CommonHero);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() => Buy(hero, 2));
        Assert.Equal(0, hero.WeaponShards);
    }
}
