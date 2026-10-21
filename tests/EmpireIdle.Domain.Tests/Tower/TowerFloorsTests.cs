using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Tower;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Tower;

/// <summary>
/// Поверх із номера: крива між опорними точками, ступінь набору, ротація складів, бос і шаблони героїв.
/// Вежа на 100 поверхів: рівень героїв 1→20, юнітів 1→3, умінь 1→2, множник 0.5→1.5; набір з 50-го — ×1.05.
/// </summary>
public class TowerFloorsTests
{
    private readonly GameConfig _config;
    private readonly TowerFloors _floors;

    public TowerFloorsTests()
    {
        _config = new GameConfigBuilder()
            .WithUnits()
            .WithHeroes(h =>
            {
                h.RoleUnits = new() { ["warrior"] = TestKeys.Infantry, ["knight"] = TestKeys.Cavalry, ["archer"] = TestKeys.Archer, ["mage"] = TestKeys.Archer };
                h.ConvoySize = 100;
                h.ConvoysByLevel = [new ConvoyStepConfig { Level = 1, Convoys = 2 }, new ConvoyStepConfig { Level = 10, Convoys = 3 }];
            })
            .Build();

        foreach (var hero in _config.Heroes)
        {
            hero.BaseStats["Health"] = 500;
            hero.StatGrowth["Health"] = 20;
        }

        var battle = _config.RealtimeBattle;
        battle.HeroHealthScale = 4;
        battle.HeroAttackIntervals = new() { ["warrior"] = 1.25, ["knight"] = 1.0, ["archer"] = 1.3, ["mage"] = 1.3 };
        battle.UnitAttackIntervals = new() { [TestKeys.Infantry] = 1.25, [TestKeys.Cavalry] = 1.0, [TestKeys.Archer] = 1.4 };

        _config.Tower = new TowerConfig
        {
            MaxFloor = 100,
            Anchors =
            [
                new TowerAnchorConfig { Floor = 1, HeroLevel = 1, UnitLevel = 1, SkillLevel = 1, StatMultiplier = 0.5 },
                new TowerAnchorConfig { Floor = 100, HeroLevel = 20, UnitLevel = 3, SkillLevel = 2, StatMultiplier = 1.5 }
            ],
            SetSteps = [new TowerSetStepConfig { FromFloor = 1, Multiplier = 1 }, new TowerSetStepConfig { FromFloor = 50, Multiplier = 1.05 }],
            Archetypes =
            [
                new TowerArchetypeConfig { Key = "duel", DisplayName = "Duel", Classes = ["warrior", "mage"] },
                new TowerArchetypeConfig { Key = "pair", DisplayName = "Pair", Classes = ["warrior", "warrior"] }
            ],
            BossArchetype = "duel",
            BossLane = 1,
            BossHealthScale = 2.5,
            HeroRarities = [new TowerRarityStepConfig { FromFloor = 1, Rarities = [Rarity.Common, Rarity.Unique] }]
        };

        _floors = new TowerFloors(new GameCatalog(_config));
    }

    /// <summary>На опорних точках — рівно їхні числа; набір з 50-го множить зверху.</summary>
    [Fact]
    public void Describe_ShouldTakeTheAnchorsAsTheyAre()
    {
        var first = _floors.Describe(1);
        var top = _floors.Describe(100);

        Assert.Equal((1, 1, 1, 0.5), (first.HeroLevel, first.UnitLevel, first.SkillLevel, first.StatMultiplier));
        Assert.Equal((20, 3, 2), (top.HeroLevel, top.UnitLevel, top.SkillLevel));
        Assert.Equal(1.5 * 1.05, top.StatMultiplier, 6);
    }

    /// <summary>Між опорами рівні — лінійно з округленням, множник — геометрично.</summary>
    [Fact]
    public void Describe_ShouldInterpolateBetweenAnchors()
    {
        var floor = _floors.Describe(50);
        var t = 49 / 99.0;

        Assert.Equal((10, 2, 1), (floor.HeroLevel, floor.UnitLevel, floor.SkillLevel));
        Assert.Equal(0.5 * Math.Pow(3, t) * 1.05, floor.StatMultiplier, 6);
        Assert.Equal(1.05, floor.SetStep.Multiplier);
    }

    /// <summary>Вище — не слабше: множник не падає від поверху до поверху.</summary>
    [Fact]
    public void Describe_ShouldNeverWeakenTheEnemyHigherUp()
    {
        var multipliers = Enumerable.Range(1, 100).Select(f => _floors.Describe(f).StatMultiplier).ToList();

        Assert.True(multipliers.Zip(multipliers.Skip(1)).All(p => p.Second >= p.First));
    }

    /// <summary>Кожен десятий — бос: склад боса, сам бос — на своїй доріжці.</summary>
    [Fact]
    public void Describe_ShouldPutTheBossOnEveryTenthFloor()
    {
        var floor = _floors.Describe(30);

        Assert.True(floor.IsBoss);
        Assert.Equal("duel", floor.Archetype.Key);
        Assert.Equal([false, true], floor.Enemies.Select(e => e.Boss));
    }

    /// <summary>Звичайні поверхи чергують склади: сусіди різні.</summary>
    [Fact]
    public void Describe_ShouldRotateArchetypesBetweenNeighbours()
    {
        Assert.Equal("pair", _floors.Describe(1).Archetype.Key);
        Assert.Equal("duel", _floors.Describe(2).Archetype.Key);
        Assert.False(_floors.Describe(1).IsBoss);
    }

    /// <summary>Шаблони героїв — свого класу й рідкості, і той самий поверх дає тих самих героїв.</summary>
    [Fact]
    public void Describe_ShouldPickStableTemplatesOfTheLanesClass()
    {
        var floor = _floors.Describe(3);
        var again = _floors.Describe(3);

        Assert.Equal(floor.Enemies.Select(e => e.Hero.Key), again.Enemies.Select(e => e.Hero.Key));
        Assert.All(floor.Enemies.Zip(floor.Archetype.Classes), p => Assert.Equal(p.Second, p.First.Hero.Class));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Describe_ShouldRefuseFloorsOutsideTheTower(int floor)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _floors.Describe(floor));

    /// <summary>
    /// Загін поверху: стати рівня × множник поверху, у боса здоров'я ×2.5, конвої повні й теж × множник.
    /// </summary>
    [Fact]
    public void Squad_ShouldScaleHeroesAndConvoys_AndStrengthenTheBoss()
    {
        var floor = _floors.Describe(10);
        var squad = _floors.Squad(floor);
        var progression = new HeroProgression(_config.HeroSettings);
        var mage = floor.Enemies[1].Hero;
        var level = floor.HeroLevel;

        var bossHealth = progression.StatValue(mage, "Health", level, mage.NativeTier, mage.NativeTier, 0, 0) * floor.StatMultiplier * 2.5 * 4;
        Assert.Equal(bossHealth, squad[1].Hero.MaxHealth, 6);
        Assert.Equal(progression.StatValue(mage, "Attack", level, mage.NativeTier, mage.NativeTier, 0, 0) * floor.StatMultiplier, squad[1].Hero.Attack, 6);

        var warrior = floor.Enemies[0].Hero;
        Assert.Equal(progression.StatValue(warrior, "Health", level, warrior.NativeTier, warrior.NativeTier, 0, 0) * floor.StatMultiplier * 4, squad[0].Hero.MaxHealth, 6);

        // Рівень героя 3 — два конвої піхоти по 100 юнітів 1 рівня: атака 10 × 100 × 0.15 (типовий масштаб) × множник
        Assert.Equal(2, squad[0].Convoys.Count);
        Assert.All(squad[0].Convoys, c => Assert.Equal(100, c.Units));
        Assert.Equal(1000 * _config.RealtimeBattle.ConvoyAttackScale * floor.StatMultiplier, squad[0].Convoys[0].Led.Attack, 6);
        Assert.Equal(TestKeys.Infantry, squad[0].UnitType);
    }

    /// <summary>Тінь б'є тими самими вміннями, що й герой гравця, якщо рівень героя їх уже відкрив.</summary>
    [Fact]
    public void Squad_ShouldGiveShadowsTheirUnlockedSkills()
    {
        var bran = _config.Heroes.Single(h => h.Key == TestKeys.CommonHero);
        bran.Skills =
        [
            Skill("strike", unlockLevel: 1),
            Skill("late", unlockLevel: 50, SkillKind.Periodic)
        ];

        var floors = new TowerFloors(new GameCatalog(_config));
        var floor = Enumerable.Range(1, 100).Select(floors.Describe).First(f => f.Enemies.Any(e => e.Hero.Key == TestKeys.CommonHero));
        var member = floors.Squad(floor)[floor.Enemies.First(e => e.Hero.Key == TestKeys.CommonHero).Lane];

        var skill = Assert.Single(member.Skills!);
        Assert.Equal("strike", skill.Key);
    }

    private static HeroSkillConfig Skill(string key, int unlockLevel, SkillKind kind = SkillKind.Active) => new()
    {
        Key = key,
        DisplayName = key,
        Kind = kind,
        UnlockLevel = unlockLevel,
        Troops = new SkillTroopBonusConfig { Stat = "Attack", Percents = [5, 6, 7, 8, 9, 10] },
        Battle = new SkillBattleConfig { Target = AbilityTarget.SingleEnemy, Cooldown = 3, DamageMultiplier = 2, LevelScale = [1.0, 1.2, 1.4, 1.6, 1.8, 2.0] }
    };
}
