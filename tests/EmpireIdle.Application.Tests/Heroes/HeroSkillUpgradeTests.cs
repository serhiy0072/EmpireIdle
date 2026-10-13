using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// Прокачка вмінь книгами (GDD §6.1, рішення 08.10.2026): книга своєї ролі, рідкості й половини,
/// вміння відкрите рівнем героя, наступний рівень відкриває зірка. Відмова книгу не з'їдає.
/// </summary>
public class HeroSkillUpgradeTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private const string AttackBook = "skill_book_warrior_common_attack";
    private const string DefenseBook = "skill_book_warrior_common_defense";

    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GameCatalog _catalog;
    private readonly Dictionary<string, PlayerItem> _items = new();

    public HeroSkillUpgradeTests()
    {
        var config = HeroTestConfig.Create();

        config.Items.Add(new ItemConfig { Key = AttackBook, DisplayName = "Книга атаки воїна", Type = "skillbook" });
        config.Items.Add(new ItemConfig { Key = DefenseBook, DisplayName = "Книга захисту воїна", Type = "skillbook" });
        config.HeroSettings.SkillBooks =
        [
            new SkillBookConfig { ItemKey = AttackBook, Class = "warrior", Rarity = Rarity.Common, Half = SkillHalf.Attack },
            new SkillBookConfig { ItemKey = DefenseBook, Class = "warrior", Rarity = Rarity.Common, Half = SkillHalf.Defense },
        ];

        var hero = config.Heroes.First(h => h.Key == HeroTestConfig.CommonHero);
        hero.Skills = [Passive("fury", SkillHalf.Attack, unlockLevel: 1), Passive("wall", SkillHalf.Defense, unlockLevel: 20)];

        _catalog = new GameCatalog(config);

        _inventory.GetItemAsync(PlayerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _items.GetValueOrDefault(call.ArgAt<string>(1)));
    }

    private static HeroSkillConfig Passive(string key, SkillHalf half, int unlockLevel) => new()
    {
        Key = key, DisplayName = key, Half = half, Kind = SkillKind.Passive, UnlockLevel = unlockLevel,
        Troops = new SkillTroopBonusConfig { Stat = "Attack", Percents = [1, 2, 3, 4, 5, 6] },
    };

    private Hero GivenHero(int level = 1, int stars = 0)
    {
        var hero = TestKit.Entities.Hero(HeroTestConfig.CommonHero, PlayerId, level: level, stars: stars);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        return hero;
    }

    private void GivenBooks(string key, int count) => _items[key] = new PlayerItem(Guid.NewGuid(), PlayerId, key, count);

    private Task Upgrade(Hero hero, string skillKey)
        => new UpgradeHeroSkillCommandHandler(_heroes, _inventory, _unitOfWork, new HeroSkills(_catalog.Config.HeroSettings),
                _catalog, new FakeTimeProvider(Now), NullLogger<UpgradeHeroSkillCommandHandler>.Instance)
            .Handle(new UpgradeHeroSkillCommand(PlayerId, hero.Id, skillKey), CancellationToken.None);

    [Fact]
    public async Task Upgrade_ShouldRaiseTheSkill_ForOneBookOfItsHalf()
    {
        var hero = GivenHero(stars: 1);
        GivenBooks(AttackBook, 3);

        await Upgrade(hero, "fury");

        Assert.Equal(2, hero.SkillLevel("fury"));
        Assert.Equal(2, _items[AttackBook].Count);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Книга іншої половини не підходить, навіть якщо вона є.</summary>
    [Fact]
    public async Task Upgrade_ShouldRefuse_WithOnlyTheOtherHalfsBook()
    {
        var hero = GivenHero(level: 20, stars: 1);
        GivenBooks(AttackBook, 3);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Upgrade(hero, "wall"));

        Assert.Equal(RefusalReasons.HeroSkillNoBook.Key, refusal.Reason);
        Assert.Equal(1, hero.SkillLevel("wall"));
        Assert.Equal(3, _items[AttackBook].Count);
    }

    [Fact]
    public async Task Upgrade_ShouldRefuse_ASkillLockedByHeroLevel()
    {
        var hero = GivenHero(level: 19, stars: 1);
        GivenBooks(DefenseBook, 3);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Upgrade(hero, "wall"));

        Assert.Equal(RefusalReasons.HeroSkillLocked.Key, refusal.Reason);
        Assert.Equal(20, refusal.Args["level"]);
        Assert.Equal(3, _items[DefenseBook].Count);
    }

    /// <summary>Без зірок вміння стоїть на першому рівні — і книга лишається в інвентарі.</summary>
    [Fact]
    public async Task Upgrade_ShouldRefuse_AboveTheStarCap_WithoutSpendingTheBook()
    {
        var hero = GivenHero(stars: 0);
        GivenBooks(AttackBook, 3);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Upgrade(hero, "fury"));

        Assert.Equal(RefusalReasons.HeroSkillStarCapped.Key, refusal.Reason);
        Assert.Equal(1, refusal.Args["stars"]);
        Assert.Equal(3, _items[AttackBook].Count);
    }

    [Fact]
    public async Task Upgrade_ShouldRefuse_AtTheTopLevel()
    {
        var hero = GivenHero(stars: 5);
        GivenBooks(AttackBook, 10);

        for (var i = 0; i < 5; i++)
            await Upgrade(hero, "fury");

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Upgrade(hero, "fury"));

        Assert.Equal(RefusalReasons.HeroSkillMaxed.Key, refusal.Reason);
        Assert.Equal(6, hero.SkillLevel("fury"));
        Assert.Equal(5, _items[AttackBook].Count);
    }

    [Fact]
    public async Task Upgrade_ShouldNotFind_AnUnknownSkill()
    {
        var hero = GivenHero(stars: 1);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Upgrade(hero, "missing"));
    }

    [Fact]
    public async Task Upgrade_ShouldNotFind_SomeoneElsesHero()
    {
        var stranger = TestKit.Entities.Hero(HeroTestConfig.CommonHero, Guid.NewGuid(), stars: 1);
        _heroes.GetByIdAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Upgrade(stranger, "fury"));
    }
}
