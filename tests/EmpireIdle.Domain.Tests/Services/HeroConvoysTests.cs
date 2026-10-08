using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Конвої героя (GDD §6.1, рішення 08.10.2026): герой веде лише юнітів своєї ролі,
/// а скільки — визначають конвої за рівнем × розмір конвою.
/// </summary>
public class HeroConvoysTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;

    private static readonly HeroesConfig Settings = new()
    {
        RoleUnits = new Dictionary<string, string> { ["warrior"] = "infantry", ["archer"] = "archer" },
        ConvoySize = 100,
        ConvoysByLevel =
        [
            new ConvoyStepConfig { Level = 1, Convoys = 2 },
            new ConvoyStepConfig { Level = 10, Convoys = 3 },
            new ConvoyStepConfig { Level = 70, Convoys = 10 },
        ],
    };

    private static readonly HeroConfig Warrior = new() { Key = "warrior_bran", DisplayName = "Бран", Class = "warrior" };

    private static HeroConvoys Convoys(HeroesConfig? settings = null) => new(settings ?? Settings);

    private static Dictionary<UnitStackKey, int> Units(string unit, int count, int level = 1)
        => new() { [new UnitStackKey(unit, level)] = count };

    [Theory]
    [InlineData(1, 2)]
    [InlineData(9, 2)]
    [InlineData(10, 3)]
    [InlineData(69, 3)]
    [InlineData(80, 10)]
    public void ConvoysAt_ShouldFollowTheCurve(int heroLevel, int expected)
        => Assert.Equal(expected, Convoys().ConvoysAt(heroLevel));

    [Fact]
    public void UnitOf_ShouldFollowTheRole()
        => Assert.Equal("infantry", Convoys().UnitOf(Warrior));

    /// <summary>Табір піднімає рівень — а з ним і військо, яке герой веде.</summary>
    [Fact]
    public void Capacity_ShouldUseTheCampLevel()
    {
        var hero = TestKit.Entities.Hero(level: 1);
        hero.EnterCamp(0, campLevel: 10, Now);

        Assert.Equal(300, Convoys().Capacity(hero));
    }

    [Fact]
    public void EnsureCanLead_ShouldAccept_OwnUnitsWithinTheConvoys()
        => Convoys().EnsureCanLead(TestKit.Entities.Hero(), Warrior, Units("infantry", 200));

    [Fact]
    public void EnsureCanLead_ShouldRefuse_UnitsOfAnotherRole()
    {
        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            Convoys().EnsureCanLead(TestKit.Entities.Hero(), Warrior, Units("archer", 10)));

        Assert.Equal(RefusalReasons.MarchWrongUnits.Key, refusal.Reason);
        Assert.Equal("archer", refusal.Args["unit"]);
    }

    /// <summary>Рахуються всі рівні юнітів разом: місткість — у головах, не в стеках.</summary>
    [Fact]
    public void EnsureCanLead_ShouldRefuse_MoreThanTheConvoysCarry()
    {
        var units = Units("infantry", 150);
        units[new UnitStackKey("infantry", 2)] = 51;

        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            Convoys().EnsureCanLead(TestKit.Entities.Hero(), Warrior, units));

        Assert.Equal(RefusalReasons.MarchOverCapacity.Key, refusal.Reason);
        Assert.Equal(201, refusal.Args["sent"]);
    }

    /// <summary>Без ролей і кривої в конфігу обмежень немає — так живуть мінімальні фікстури.</summary>
    [Fact]
    public void EnsureCanLead_ShouldNotLimit_WithoutConvoyConfig()
        => Convoys(new HeroesConfig()).EnsureCanLead(TestKit.Entities.Hero(), Warrior, Units("archer", 10_000));
}
