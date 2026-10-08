using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Tests.Dungeons;

/// <summary>
/// Збережений бій має читатись назад без утрат: між ходами гравця стан
/// лежить у базі рядком, і будь-яка втрачена властивість — це загублений забіг.
/// </summary>
public class BattleSerializerTests
{
    private static BattleState State() => new()
    {
        Combatants =
        [
            new Combatant
            {
                // Нульові значення навмисні: саме вони зникали, коли серіалізатор
                // пропускав типові, і стан переставав читатись
                Index = 0,
                Side = BattleSide.Heroes,
                Line = BattleLine.Front,
                Key = "warrior_bran",
                HeroId = Guid.NewGuid(),
                Attack = 120,
                Defense = 40,
                MaxHealth = 600,
                Health = 600,
                Speed = 4,
                Skills =
                [
                    new CombatSkill
                    {
                        Key = "warrior_bran_smash", Kind = SkillKind.Active, Target = AbilityTarget.SingleEnemy,
                        Cooldown = 3, CooldownLeft = 0, DamageMultiplier = 2.2,
                    },
                ],
                Statuses = [new BattleStatus { Kind = BattleStatusKind.Taunt, Magnitude = 0, TurnsLeft = 2 }],
                UniqueStats = new Dictionary<DungeonStat, double> { [DungeonStat.CritChance] = 0.12 },
            },
            new Combatant
            {
                Index = 1,
                Side = BattleSide.Enemies,
                Line = BattleLine.Back,
                Key = "sunken_crypt_seer",
                Attack = 85,
                Defense = 12,
                MaxHealth = 240,
                Health = 240,
                Speed = 9,
                ShieldPoints = 0,
                Statuses = [],
                UniqueStats = [],
            },
        ],
        Wave = 1,
        Round = 0,
        Queue = [1, 0],
        Seed = 12345,
        TurnNumber = 0,
    };

    [Fact]
    public void Roundtrip_ShouldKeepEveryField_IncludingDefaults()
    {
        var original = State();

        var restored = BattleSerializer.Read(BattleSerializer.Write(original));

        Assert.Equal(original.Wave, restored.Wave);
        Assert.Equal(original.Round, restored.Round);
        Assert.Equal(original.Seed, restored.Seed);
        Assert.Equal(original.TurnNumber, restored.TurnNumber);
        Assert.Equal(original.Queue, restored.Queue);

        var hero = restored.Combatants[0];
        Assert.Equal(0, hero.Index);
        Assert.Equal(BattleSide.Heroes, hero.Side);
        Assert.Equal(0, hero.Skills[0].CooldownLeft);
        Assert.Equal(SkillKind.Active, hero.Skills[0].Kind);
        Assert.Equal(original.Combatants[0].HeroId, hero.HeroId);
        Assert.Equal(0.12, hero.UniqueStats[DungeonStat.CritChance]);
        Assert.Equal(BattleStatusKind.Taunt, hero.Statuses[0].Kind);

        var enemy = restored.Combatants[1];
        Assert.Equal(BattleSide.Enemies, enemy.Side);
        Assert.Equal(BattleLine.Back, enemy.Line);
        Assert.Null(enemy.HeroId);
    }

    /// <summary>
    /// Забіг, збережений до вмінь (зі шкалою енергії), читається як бій без вмінь, а не падає:
    /// зайве поле ігнорується, а відсутній список вмінь — порожній.
    /// </summary>
    [Fact]
    public void Read_ShouldAcceptABattleSavedBeforeSkills()
    {
        const string legacy = """
            {"combatants":[{"index":0,"side":"Heroes","line":"Front","key":"warrior_bran","heroId":null,
            "attack":10,"defense":5,"maxHealth":100,"health":100,"speed":4,"energy":50,"shieldPoints":0,
            "statuses":[],"uniqueStats":{}}],"wave":1,"round":1,"queue":[0],"seed":7,"turnNumber":3}
            """;

        var restored = BattleSerializer.Read(legacy);

        Assert.Empty(restored.Combatants[0].Skills);
        Assert.Equal(3, restored.TurnNumber);
    }
}
