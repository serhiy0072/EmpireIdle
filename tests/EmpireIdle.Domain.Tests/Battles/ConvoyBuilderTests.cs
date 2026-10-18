using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Battles;

/// <summary>
/// Конвої одномоментного бою: кількість за рівнем героя, юніти з гарнізону найвищими рівнями наперед,
/// сила з юнітів і бонусів героя. Піхота TestKit: атака 10, захист 12, здоров'я 12.
/// </summary>
public class ConvoyBuilderTests
{
    private const int ConvoySize = 100;

    private readonly ConvoyBuilder _builder;

    public ConvoyBuilderTests()
    {
        var config = new GameConfigBuilder()
            .WithUnits()
            .WithHeroes(h =>
            {
                h.RoleUnits = new() { ["warrior"] = TestKeys.Infantry, ["knight"] = TestKeys.Cavalry, ["archer"] = TestKeys.Archer, ["mage"] = TestKeys.Archer };
                h.ConvoySize = ConvoySize;
                h.ConvoysByLevel = [new ConvoyStepConfig { Level = 1, Convoys = 2 }, new ConvoyStepConfig { Level = 10, Convoys = 3 }];
            })
            .Build();

        config.Equipment.UnitStatPower = 0.25;
        config.RealtimeBattle.ConvoyAttackScale = 0.15;
        config.RealtimeBattle.ConvoyHealthScale = 1.0;
        config.RealtimeBattle.ConvoyDefenseScale = 12;
        config.RealtimeBattle.UnitAttackIntervals = new() { [TestKeys.Infantry] = 1.25, [TestKeys.Cavalry] = 1.0, [TestKeys.Archer] = 1.4 };

        _builder = new ConvoyBuilder(new GameCatalog(config));
    }

    private static UnitPool Pool(params (string Unit, int Level, int Count)[] stacks)
        => new(stacks.ToDictionary(s => new UnitStackKey(s.Unit, s.Level), s => s.Count));

    /// <summary>Повний конвой першого рівня: 100 юнітів × стат × масштаб; захист — одного юніта × масштаб.</summary>
    [Fact]
    public void Build_ShouldTurnAFullConvoyIntoFighterStats()
    {
        var convoy = Assert.Single(_builder.Build(new ConvoyLeader("warrior", 1), Pool((TestKeys.Infantry, 1, 100))));

        Assert.Equal((TestKeys.Infantry, 100), (convoy.UnitType, convoy.Units));
        Assert.Equal(1000 * 0.15, convoy.Led.Attack, 6);
        Assert.Equal(12 * 12, convoy.Led.Defense, 6);
        Assert.Equal(1200, convoy.Led.MaxHealth, 6);
        Assert.Equal(1.25, convoy.Led.AttackInterval, 6);
        Assert.Equal(1, convoy.Led.CritMultiplier);
    }

    /// <summary>Герой 10 рівня веде три конвої; юнітів на два з половиною — третій неповний, четвертого немає.</summary>
    [Fact]
    public void Build_ShouldFillConvoysInTurn_AndLeaveThePartialOneWeaker()
    {
        var convoys = _builder.Build(new ConvoyLeader("warrior", 10), Pool((TestKeys.Infantry, 1, 250)));

        Assert.Equal([100, 100, 50], convoys.Select(c => c.Units));
        Assert.Equal(convoys[0].Led.Attack / 2, convoys[2].Led.Attack, 6);
        Assert.Equal(convoys[0].Led.Defense, convoys[2].Led.Defense, 6);
    }

    /// <summary>Юнітів немає — у бій не виходить жоден конвой.</summary>
    [Fact]
    public void Build_ShouldSkipEmptyConvoys()
        => Assert.Empty(_builder.Build(new ConvoyLeader("warrior", 10), Pool((TestKeys.Archer, 1, 300))));

    /// <summary>Перший конвой бере найвищий рівень; захист — середній множник рівнів у конвої.</summary>
    [Fact]
    public void Build_ShouldTakeTheHighestLevelsFirst()
    {
        var convoys = _builder.Build(new ConvoyLeader("warrior", 1), Pool((TestKeys.Infantry, 1, 100), (TestKeys.Infantry, 3, 50), (TestKeys.Infantry, 2, 100)));

        // Перший: 50 × 1.2 + 50 × 1.1 = 115 юніт-рівнів; другий: 50 × 1.1 + 50 × 1.0 = 105
        Assert.Equal(115 * 10 * 0.15, convoys[0].Led.Attack, 6);
        Assert.Equal(1.15 * 12 * 12, convoys[0].Led.Defense, 6);
        Assert.Equal(105 * 10 * 0.15, convoys[1].Led.Attack, 6);
    }

    /// <summary>Двоє героїв однієї ролі ділять пул: другому лишається те, що не взяв перший.</summary>
    [Fact]
    public void Build_ShouldShareThePoolBetweenHeroesOfOneRole()
    {
        var pool = Pool((TestKeys.Infantry, 1, 300));

        var first = _builder.Build(new ConvoyLeader("warrior", 10), pool);
        var second = _builder.Build(new ConvoyLeader("warrior", 10), pool);

        Assert.Equal(3, first.Count);
        Assert.Empty(second);
        Assert.Equal(0, pool.Left(TestKeys.Infantry));
    }

    /// <summary>
    /// Бонуси героя: відсотки множать атаку, захист і здоров'я, пласкі стати спорядження — порівну на всі
    /// конвої героя. Без героя конвой лишається з голими статами, але здоров'я не змінює.
    /// </summary>
    [Fact]
    public void Build_ShouldApplyTheHerosTroopBonuses_OnlyWhileTheHeroLives()
    {
        var leader = new ConvoyLeader("warrior", 1, AttackMultiplier: 1.2, DefenseMultiplier: 1.1, UnitAttack: 80, UnitDefense: 40);

        var convoy = _builder.Build(leader, Pool((TestKeys.Infantry, 1, 200)))[0];

        // Пласке: 80 × 0.25 / 2 конвої = 10 атаки, 40 × 0.25 / 2 = 5 здоров'я
        Assert.Equal((1000 + 10) * 1.2 * 0.15, convoy.Led.Attack, 6);
        Assert.Equal(12 * 1.1 * 12, convoy.Led.Defense, 6);
        Assert.Equal((1200 + 5) * 1.1, convoy.Led.MaxHealth, 6);

        Assert.Equal(1000 * 0.15, convoy.Leaderless.Attack, 6);
        Assert.Equal(12 * 12, convoy.Leaderless.Defense, 6);
        Assert.Equal(convoy.Led.MaxHealth, convoy.Leaderless.MaxHealth, 6);
    }

    /// <summary>Вороги вежі не мають гарнізону: усі конвої повні, одного рівня юнітів.</summary>
    [Fact]
    public void BuildFull_ShouldFillEveryConvoyWithOneUnitLevel()
    {
        var convoys = _builder.BuildFull(new ConvoyLeader("knight", 10), unitLevel: 3);

        Assert.Equal(3, convoys.Count);
        Assert.All(convoys, c => Assert.Equal((TestKeys.Cavalry, 100), (c.UnitType, c.Units)));
        Assert.Equal(100 * 16 * 1.2 * 0.15, convoys[0].Led.Attack, 6);
    }
}
