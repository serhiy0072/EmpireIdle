using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Dungeons;

/// <summary>Старт забігу: оберіг з наборів ставить героєві щит ще до першого ходу.</summary>
public class BattleBuilderTests
{
    private static readonly DungeonConfig Dungeon = new()
    {
        Key = "pit",
        ArtifactSetKey = "pit",
        Waves =
        [
            new DungeonEnemyConfig { Key = "rat", DisplayName = "Щур", Wave = 1, Line = BattleLine.Front, Attack = 10, Defense = 5, Health = 100, Speed = 5 },
        ],
        Boss = [new DungeonEnemyConfig { Key = "king", DisplayName = "Король", Line = BattleLine.Front, Attack = 20, Defense = 5, Health = 300, Speed = 5 }],
    };

    private static DungeonHero Hero(double ward)
        => new(Guid.NewGuid(), "warrior_bran", "warrior", 100, 30, 400, 10,
            ward > 0 ? new Dictionary<DungeonStat, double> { [DungeonStat.Ward] = ward } : [], []);

    [Fact]
    public void Start_ShouldShieldTheHero_ForTheWardShareOfMaxHealth()
    {
        var builder = new BattleBuilder(new DungeonsConfig { WardTurns = 2 });

        var hero = builder.Start([Hero(ward: 0.16)], Dungeon, level: 1, seed: 1).Combatants[0];

        Assert.Equal(64, hero.ShieldPoints, 3);
        var shield = Assert.Single(hero.Statuses);
        Assert.Equal(BattleStatusKind.Shield, shield.Kind);
        Assert.Equal(2, shield.TurnsLeft);
    }

    /// <summary>Оберіг понад стелю щит не збільшує: 0.9 × 400 обрізається до 0.5 × 400.</summary>
    [Fact]
    public void Start_ShouldCapTheWard()
    {
        var builder = new BattleBuilder(new DungeonsConfig { MaxWard = 0.5 });

        var hero = builder.Start([Hero(ward: 0.9)], Dungeon, level: 1, seed: 1).Combatants[0];

        Assert.Equal(200, hero.ShieldPoints, 3);
    }

    [Fact]
    public void Start_ShouldGiveNoShield_WithoutAWard()
    {
        var hero = new BattleBuilder(new DungeonsConfig()).Start([Hero(ward: 0)], Dungeon, level: 1, seed: 1).Combatants[0];

        Assert.Equal(0, hero.ShieldPoints);
        Assert.Empty(hero.Statuses);
    }
}
