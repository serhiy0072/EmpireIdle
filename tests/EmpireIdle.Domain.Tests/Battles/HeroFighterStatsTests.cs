using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Battles;

/// <summary>Стати героя для одномоментного бою: відсотки стають частками, стелі тримають.</summary>
public class HeroFighterStatsTests
{
    private static readonly RealtimeBattleConfig Battle = new()
    {
        HeroHealthScale = 4,
        HeroAttackIntervals = new() { ["knight"] = 1.0 },
        MaxAttackSpeed = 0.5,
        MaxCooldownReduction = 0.35,
        CooldownReductionPerTurn = 0.12
    };

    private static readonly DungeonsConfig Caps = new()
    {
        BaseCritChance = 0.1,
        CritMultiplier = 1.6,
        MaxCritChance = 0.6,
        MaxBlockChance = 0.4,
        BaseBlockPower = 0.3,
        MaxBlockPower = 0.8,
        MaxDamageReduction = 0.8,
        MaxLifesteal = 0.3,
        MaxWard = 0.5
    };

    private static FighterStats From(Dictionary<string, double> stats) => HeroFighterStats.From(stats, "knight", Battle, Caps);

    /// <summary>Голий герой: атака й захист як є, здоров'я ×4, темп класу, прихована база криту.</summary>
    [Fact]
    public void From_ShouldTakeBaseStats_AndTheHiddenCrit()
    {
        var fighter = From(new() { ["Attack"] = 120, ["Defense"] = 80, ["Health"] = 500 });

        Assert.Equal(120, fighter.Attack);
        Assert.Equal(80, fighter.Defense);
        Assert.Equal(2000, fighter.MaxHealth);
        Assert.Equal(1.0, fighter.AttackInterval, 6);
        Assert.Equal(0.1, fighter.CritChance, 6);
        Assert.Equal(1.6, fighter.CritMultiplier, 6);
        Assert.Equal(0.3, fighter.BlockPower, 6);
        Assert.Equal(1, fighter.CooldownMultiplier, 6);
    }

    /// <summary>Відсотки статів стають частками: 15% швидкості атаки — удар кожні 1/1.15 с.</summary>
    [Fact]
    public void From_ShouldTurnPercentsIntoShares()
    {
        var fighter = From(new()
        {
            ["AttackSpeed"] = 15,
            ["CritChance"] = 20,
            ["CritPower"] = 35,
            ["BlockChance"] = 10,
            ["BlockPower"] = 20,
            ["DamageReduction"] = 12,
            ["Lifesteal"] = 8,
            ["Ward"] = 16,
            ["SkillDamage"] = 30,
            ["CooldownReduction"] = 8
        });

        Assert.Equal(1 / 1.15, fighter.AttackInterval, 6);
        Assert.Equal(0.3, fighter.CritChance, 6);
        Assert.Equal(1.95, fighter.CritMultiplier, 6);
        Assert.Equal(0.1, fighter.BlockChance, 6);
        Assert.Equal(0.5, fighter.BlockPower, 6);
        Assert.Equal(0.12, fighter.DamageReduction, 6);
        Assert.Equal(0.08, fighter.Lifesteal, 6);
        Assert.Equal(0.16, fighter.Ward, 6);
        Assert.Equal(0.3, fighter.SkillDamage, 6);
        Assert.Equal(0.92, fighter.CooldownMultiplier, 6);
    }

    /// <summary>Хід перезарядки з набору tide/hollow — 12% у цьому бою, разом зі скороченням корони.</summary>
    [Fact]
    public void From_ShouldCountCooldownTurnsAsShares()
    {
        var fighter = From(new() { ["CooldownTurns"] = 1, ["CooldownReduction"] = 8 });

        Assert.Equal(0.8, fighter.CooldownMultiplier, 6);
    }

    /// <summary>Понад стелі не росте нічого: інакше бій вирішували б кидки, а не склад.</summary>
    [Fact]
    public void From_ShouldCapEveryStat()
    {
        var fighter = From(new()
        {
            ["AttackSpeed"] = 200,
            ["CritChance"] = 90,
            ["BlockChance"] = 90,
            ["BlockPower"] = 90,
            ["DamageReduction"] = 95,
            ["Lifesteal"] = 90,
            ["Ward"] = 90,
            ["CooldownReduction"] = 30,
            ["CooldownTurns"] = 2
        });

        Assert.Equal(1 / 1.5, fighter.AttackInterval, 6);
        Assert.Equal(0.6, fighter.CritChance, 6);
        Assert.Equal(0.4, fighter.BlockChance, 6);
        Assert.Equal(0.8, fighter.BlockPower, 6);
        Assert.Equal(0.8, fighter.DamageReduction, 6);
        Assert.Equal(0.3, fighter.Lifesteal, 6);
        Assert.Equal(0.5, fighter.Ward, 6);
        Assert.Equal(0.65, fighter.CooldownMultiplier, 6);
    }

    [Fact]
    public void From_ShouldRefuseAClassWithoutAnInterval()
        => Assert.Throws<InvalidOperationException>(() => HeroFighterStats.From(new Dictionary<string, double>(), "mage", Battle, Caps));
}
