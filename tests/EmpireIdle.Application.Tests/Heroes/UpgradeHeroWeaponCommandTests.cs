using EmpireIdle.Application.Heroes.Commands;
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
/// Унікальна зброя героя (GDD §6.4, §9.12): відкриття за 5 шматків, далі 5/10/20/40 до стелі +5.
/// Шматки лежать на герої; чужий герой не відрізняється від неіснуючого.
/// </summary>
public class UpgradeHeroWeaponCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GameCatalog _catalog;

    public UpgradeHeroWeaponCommandTests()
    {
        var config = HeroTestConfig.Create();
        config.HeroSettings.WeaponShardCosts = [5, 5, 10, 20, 40];
        config.HeroSettings.WeaponBonusPercents = new() { [Rarity.Common] = [3, 5, 8, 12, 16] };
        _catalog = new GameCatalog(config);
    }

    private Hero GivenHero(int shards, Guid? owner = null)
    {
        var hero = TestKit.Entities.Hero(HeroTestConfig.CommonHero, owner ?? PlayerId);
        if (shards > 0)
            hero.AddWeaponShards(shards, Now);

        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        return hero;
    }

    private Task Upgrade(Hero hero)
        => new UpgradeHeroWeaponCommandHandler(_heroes, _unitOfWork, new HeroProgression(_catalog.Config.HeroSettings), _catalog,
                new FakeTimeProvider(Now), NullLogger<UpgradeHeroWeaponCommandHandler>.Instance)
            .Handle(new UpgradeHeroWeaponCommand(PlayerId, hero.Id), CancellationToken.None);

    /// <summary>П'ять шматків відкривають зброю, наступні п'ять — +2; решта лишається на герої.</summary>
    [Fact]
    public async Task Handle_ShouldUnlockThenRaiseTheWeapon()
    {
        var hero = GivenHero(shards: 12);

        await Upgrade(hero);
        await Upgrade(hero);

        Assert.Equal(2, hero.WeaponLevel);
        Assert.Equal(2, hero.WeaponShards);
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>На стелі — відмова з причиною, а не тихе ігнорування.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_AtTheCap()
    {
        var hero = GivenHero(shards: 80);
        for (var i = 0; i < 5; i++)
            await Upgrade(hero);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Upgrade(hero));

        Assert.Equal(RefusalReasons.HeroWeaponMaxed.Key, refusal.Reason);
        Assert.Equal(5, hero.WeaponLevel);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WithoutEnoughShards()
    {
        var hero = GivenHero(shards: 3);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Upgrade(hero));

        Assert.Equal(RefusalReasons.HeroWeaponShards.Key, refusal.Reason);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldHideSomeoneElsesHero()
    {
        var hero = GivenHero(shards: 10, owner: Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Upgrade(hero));
        Assert.Equal(0, hero.WeaponLevel);
    }
}
