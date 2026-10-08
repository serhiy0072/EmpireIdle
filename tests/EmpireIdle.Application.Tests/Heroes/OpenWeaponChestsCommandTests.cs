using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Скриня зброї (GDD §6.4, §9.12): з рюкзака, для обраного героя трійки — 1 шматок його зброї за скриню.
/// Рівень зброї сам не росте; герой поза трійкою чи чужий — відмова, скрині не згорають.
/// </summary>
public class OpenWeaponChestsCommandTests
{
    private const string Chest = "weapon_chest";
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GameCatalog _catalog;

    public OpenWeaponChestsCommandTests()
    {
        var config = HeroTestConfig.Create();
        config.Items.Add(new ItemConfig
        {
            Key = Chest,
            Type = "weaponchest",
            WeaponHeroes = [HeroTestConfig.UniqueHero],
            WeaponShards = 1
        });
        _catalog = new GameCatalog(config);
    }

    private Hero GivenHero(string heroKey = HeroTestConfig.UniqueHero, Guid? owner = null)
    {
        var hero = TestKit.Entities.Hero(heroKey, owner ?? PlayerId);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        return hero;
    }

    private PlayerItem GivenChests(int count)
    {
        var stack = new PlayerItem(Guid.NewGuid(), PlayerId, Chest, count);
        _inventory.GetItemAsync(PlayerId, Chest, Arg.Any<CancellationToken>()).Returns(stack);
        return stack;
    }

    private Task Open(Hero hero, int count)
        => new OpenWeaponChestsCommandHandler(_heroes, _inventory, _unitOfWork, _catalog, new FakeTimeProvider(Now),
                NullLogger<OpenWeaponChestsCommandHandler>.Instance)
            .Handle(new OpenWeaponChestsCommand(PlayerId, hero.Id, Chest, count), CancellationToken.None);

    /// <summary>Три скрині — три шматки на герої; рівень зброї не змінюється сам.</summary>
    [Fact]
    public async Task Handle_ShouldAddOneShardPerChest()
    {
        var hero = GivenHero();
        var stack = GivenChests(5);

        await Open(hero, 3);

        Assert.Equal(3, hero.WeaponShards);
        Assert.Equal(0, hero.WeaponLevel);
        Assert.Equal(2, stack.Count);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRemoveTheStack_WhenTheLastChestIsOpened()
    {
        var hero = GivenHero();
        var stack = GivenChests(2);

        await Open(hero, 2);

        _inventory.Received(1).RemoveItem(stack);
    }

    /// <summary>Герой не з трійки скрині — відмова з причиною, скрині лишаються.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_ForAHeroOutsideTheChestTrio()
    {
        var hero = GivenHero(HeroTestConfig.CommonHero);
        var stack = GivenChests(5);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Open(hero, 1));

        Assert.Equal(RefusalReasons.HeroWeaponNotInChest.Key, refusal.Reason);
        Assert.Equal(5, stack.Count);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WithoutEnoughChests()
    {
        var hero = GivenHero();
        GivenChests(1);

        await Assert.ThrowsAsync<RequirementNotMetException>(() => Open(hero, 2));
        Assert.Equal(0, hero.WeaponShards);
    }

    [Fact]
    public async Task Handle_ShouldHideSomeoneElsesHero()
    {
        var hero = GivenHero(owner: Guid.NewGuid());
        GivenChests(5);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Open(hero, 1));
    }
}
