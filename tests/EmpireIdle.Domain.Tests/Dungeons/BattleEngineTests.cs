using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Dungeons;

/// <summary>
/// Покроковий бій данжів: черга ходів, лінії, енергія, стани.
/// Кожен тест ламає рівно одне правило — інакше падіння не показує, яке саме.
/// </summary>
public class BattleEngineTests
{
    private static DungeonsConfig Config() => new()
    {
        MaxEnergy = 100,
        EnergyPerAttack = 25,
        EnergyPerHitTaken = 10,
        DefenseSoftening = 120,
        MinDamageShare = 0.1,
        BaseCritChance = 0,
        CritMultiplier = 1.6,
    };

    private static BattleEngine Engine(DungeonsConfig? config = null) => new(config ?? Config());

    private static Combatant Hero(int index, BattleLine line = BattleLine.Front, double speed = 10,
        double health = 500, double attack = 100, int energy = 0, Dictionary<DungeonStat, double>? unique = null) => new()
    {
        Index = index,
        Side = BattleSide.Heroes,
        Line = line,
        Key = $"hero{index}",
        HeroId = Guid.NewGuid(),
        Attack = attack,
        Defense = 30,
        MaxHealth = health,
        Health = health,
        Speed = speed,
        Energy = energy,
        Statuses = [],
        UniqueStats = unique ?? [],
    };

    private static Combatant Enemy(int index, BattleLine line = BattleLine.Front, double speed = 5,
        double health = 300, double attack = 60) => new()
    {
        Index = index,
        Side = BattleSide.Enemies,
        Line = line,
        Key = $"enemy{index}",
        Attack = attack,
        Defense = 20,
        MaxHealth = health,
        Health = health,
        Speed = speed,
        Energy = 0,
        Statuses = [],
        UniqueStats = [],
    };

    private static BattleState State(params Combatant[] combatants)
        => BattleEngine.NextRound(new BattleState
        {
            Combatants = combatants.ToList(),
            Wave = 1,
            Round = 0,
            Queue = [],
            Seed = 42,
            TurnNumber = 0,
        });

    private static HeroAbilityConfig Strike(string key = "strike", int cost = 50, double multiplier = 1.8,
        AbilityTarget target = AbilityTarget.SingleEnemy, bool ignoresLine = false) => new()
    {
        Key = key,
        DisplayName = key,
        EnergyCost = cost,
        Target = target,
        DamageMultiplier = multiplier,
        IgnoresLine = ignoresLine,
    };

    // ---------- Черга ходів ----------

    /// <summary>Швидкість вирішує порядок; за рівної швидкості — номер у складі, щоб бій лишався відтворюваним.</summary>
    [Fact]
    public void NextRound_ShouldOrderBySpeedThenIndex()
    {
        var state = State(Hero(0, speed: 5), Hero(1, speed: 12), Enemy(2, speed: 12), Enemy(3, speed: 1));

        Assert.Equal([1, 2, 0, 3], state.Queue);
    }

    [Fact]
    public void NextRound_ShouldSkipTheDead()
    {
        var state = State(Hero(0), Enemy(1));
        var dead = state with { Combatants = [state.Combatants[0] with { Health = 0 }, state.Combatants[1]] };

        Assert.Equal([1], BattleEngine.NextRound(dead).Queue);
    }

    // ---------- Лінії ----------

    /// <summary>Поки жива передня лінія, тил недосяжний — саме це робить танка потрібним.</summary>
    [Fact]
    public void CanTarget_ShouldProtectTheBackLine_WhileTheFrontStands()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));
        var actor = state.Combatants[0];

        Assert.True(BattleEngine.CanTarget(state, actor, null, 1));
        Assert.False(BattleEngine.CanTarget(state, actor, null, 2));
    }

    [Fact]
    public void CanTarget_ShouldOpenTheBackLine_WhenTheFrontIsDown()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));
        var fallen = state with { Combatants = [state.Combatants[0], state.Combatants[1] with { Health = 0 }, state.Combatants[2]] };

        Assert.True(BattleEngine.CanTarget(fallen, fallen.Combatants[0], null, 2));
    }

    /// <summary>Далекобійне вміння дістає тил попри живу передню лінію.</summary>
    [Fact]
    public void CanTarget_ShouldReachTheBackLine_WithALineIgnoringAbility()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));

        Assert.True(BattleEngine.CanTarget(state, state.Combatants[0], Strike(ignoresLine: true), 2));
    }

    /// <summary>Провокація перебиває лінію: удари йдуть у того, хто провокує, навіть із тилу.</summary>
    [Fact]
    public void CanTarget_ShouldForceTheTaunter()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));
        var taunted = state with
        {
            Combatants =
            [
                state.Combatants[0],
                state.Combatants[1],
                state.Combatants[2] with { Statuses = [new BattleStatus { Kind = BattleStatusKind.Taunt, Magnitude = 0, TurnsLeft = 2 }] },
            ],
        };

        Assert.True(BattleEngine.CanTarget(taunted, taunted.Combatants[0], null, 2));
        Assert.False(BattleEngine.CanTarget(taunted, taunted.Combatants[0], null, 1));
    }

    // ---------- Удари й енергія ----------

    [Fact]
    public void Execute_BasicAttack_ShouldDealDamageAndChargeBothSides()
    {
        var state = State(Hero(0), Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1), ability: null);

        var enemy = result.State.Combatants[1];
        Assert.True(enemy.Health < enemy.MaxHealth);
        Assert.Equal(25, result.State.Combatants[0].Energy);
        Assert.Equal(10, enemy.Energy);
        Assert.Equal(1, result.State.TurnNumber);
    }

    [Fact]
    public void Execute_Ability_ShouldSpendEnergyAndHitHarder()
    {
        var state = State(Hero(0, energy: 100), Enemy(1));
        var engine = Engine();

        var basic = engine.Execute(state, 0, new BattleAction(null, 1), null);
        var ability = engine.Execute(state, 0, new BattleAction("strike", 1), Strike(cost: 100, multiplier: 2.0));

        var afterBasic = basic.State.Combatants[1].Health;
        var afterAbility = ability.State.Combatants[1].Health;

        Assert.True(afterAbility < afterBasic);
        Assert.Equal(0, ability.State.Combatants[0].Energy);
    }

    /// <summary>Вміння по площі б'є всіх живих ворогів, правило ліній на нього не діє.</summary>
    [Fact]
    public void Execute_AreaAbility_ShouldHitEveryEnemy()
    {
        var state = State(Hero(0, energy: 100), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));
        var area = Strike("storm", cost: 100, multiplier: 1.2, target: AbilityTarget.AllEnemies);

        var result = Engine().Execute(state, 0, new BattleAction("storm", null), area);

        Assert.True(result.State.Combatants[1].Health < result.State.Combatants[1].MaxHealth);
        Assert.True(result.State.Combatants[2].Health < result.State.Combatants[2].MaxHealth);
        Assert.Equal(2, result.Log.Effects.Count);
    }

    // ---------- Підтримка ----------

    [Fact]
    public void Execute_Heal_ShouldRestoreHealthWithoutExceedingTheMaximum()
    {
        var hurt = Hero(0) with { Health = 100 };
        var state = State(hurt, Hero(1, energy: 100), Enemy(2));
        var heal = new HeroAbilityConfig
        {
            Key = "mend", DisplayName = "mend", EnergyCost = 50,
            Target = AbilityTarget.SingleAlly, HealPercent = 0.3,
        };

        var result = Engine().Execute(state, 1, new BattleAction("mend", 0), heal);

        Assert.Equal(250, result.State.Combatants[0].Health);
        // Підтримка не б'є, тож приросту енергії за удар немає: лишається 100 − 50
        Assert.Equal(50, result.State.Combatants[1].Energy);
    }

    /// <summary>Щит поглинає шкоду до свого запасу, а решта йде у здоров'я.</summary>
    [Fact]
    public void Execute_Shield_ShouldAbsorbDamageFirst()
    {
        var shielded = Hero(0) with
        {
            ShieldPoints = 1000,
            Statuses = [new BattleStatus { Kind = BattleStatusKind.Shield, Magnitude = 0, TurnsLeft = 3 }],
        };
        var state = State(shielded, Enemy(1));

        var result = Engine().Execute(state, 1, new BattleAction(null, 0), null);

        var hero = result.State.Combatants[0];
        Assert.Equal(hero.MaxHealth, hero.Health);
        Assert.True(hero.ShieldPoints < 1000);
        Assert.True(result.Log.Effects[0].ShieldAbsorbed > 0);
    }

    // ---------- Стани ----------

    [Fact]
    public void Execute_ShouldSkipTheTurn_WhenStunned()
    {
        var stunned = Hero(0) with
        {
            Statuses = [new BattleStatus { Kind = BattleStatusKind.Stun, Magnitude = 0, TurnsLeft = 1 }],
        };
        var state = State(stunned, Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1), null);

        Assert.True(result.Log.Stunned);
        Assert.Equal(result.State.Combatants[1].MaxHealth, result.State.Combatants[1].Health);
        // Стан згорає наприкінці ходу — наступний хід уже вільний
        Assert.Empty(result.State.Combatants[0].Statuses);
    }

    [Fact]
    public void Execute_Poison_ShouldBiteBeforeTheTurnAndExpire()
    {
        var poisoned = Hero(0) with
        {
            Statuses = [new BattleStatus { Kind = BattleStatusKind.Poison, Magnitude = 40, TurnsLeft = 1 }],
        };
        var state = State(poisoned, Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1), null);

        Assert.Equal(460, result.State.Combatants[0].Health);
        Assert.Empty(result.State.Combatants[0].Statuses);
    }

    /// <summary>Повторне накладання оновлює тривалість, а не складає ефекти.</summary>
    [Fact]
    public void Execute_ShouldRefreshAStatus_InsteadOfStacking()
    {
        var state = State(Hero(0, energy: 100), Enemy(1));
        var venom = new HeroAbilityConfig
        {
            Key = "venom", DisplayName = "venom", EnergyCost = 50, Target = AbilityTarget.SingleEnemy,
            DamageMultiplier = 1.0, Status = BattleStatusKind.Poison, StatusMagnitude = 30, StatusTurns = 3,
        };
        var engine = Engine();

        var first = engine.Execute(state, 0, new BattleAction("venom", 1), venom);
        var second = engine.Execute(first.State with { Combatants = first.State.Combatants }, 0, new BattleAction("venom", 1), venom);

        var poison = second.State.Combatants[1].Statuses.Where(s => s.Kind == BattleStatusKind.Poison).ToList();
        Assert.Single(poison);
        Assert.Equal(3, poison[0].TurnsLeft);
    }

    // ---------- Унікальні стати артефактів ----------

    [Fact]
    public void Execute_Lifesteal_ShouldHealTheAttacker()
    {
        var vampire = Hero(0, unique: new Dictionary<DungeonStat, double> { [DungeonStat.Lifesteal] = 0.5 }) with { Health = 200 };
        var state = State(vampire, Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1), null);

        Assert.True(result.State.Combatants[0].Health > 200);
    }

    [Fact]
    public void Execute_DamageReduction_ShouldSoftenTheHit()
    {
        var plain = State(Hero(0), Enemy(1));
        var tough = State(Hero(0, unique: new Dictionary<DungeonStat, double> { [DungeonStat.DamageReduction] = 0.5 }), Enemy(1));
        var engine = Engine();

        var hitPlain = engine.Execute(plain, 1, new BattleAction(null, 0), null).State.Combatants[0].Health;
        var hitTough = engine.Execute(tough, 1, new BattleAction(null, 0), null).State.Combatants[0].Health;

        Assert.True(hitTough > hitPlain);
    }

    // ---------- Детермінізм ----------

    /// <summary>Той самий стан і сід дають той самий хід — інакше бій не відтворити з журналу.</summary>
    [Fact]
    public void Execute_ShouldBeReproducible_ForTheSameSeed()
    {
        var config = Config();
        config.BaseCritChance = 0.5;

        var state = State(Hero(0), Enemy(1));

        var first = new BattleEngine(config).Execute(state, 0, new BattleAction(null, 1), null);
        var second = new BattleEngine(config).Execute(state, 0, new BattleAction(null, 1), null);

        Assert.Equal(first.State.Combatants[1].Health, second.State.Combatants[1].Health);
        Assert.Equal(first.Log.Effects[0].Critical, second.Log.Effects[0].Critical);
    }

    // ---------- Автобій ----------

    [Fact]
    public void ChooseAuto_ShouldPreferTheStrongerAbility_WhenTheGaugeIsFull()
    {
        var state = State(Hero(0, energy: 100), Enemy(1));
        var abilities = new List<HeroAbilityConfig> { Strike("weak", 50, 1.4), Strike("strong", 100, 2.4) };

        var action = Engine().ChooseAuto(state, 0, abilities);

        Assert.Equal("strong", action.AbilityKey);
    }

    [Fact]
    public void ChooseAuto_ShouldFallBackToABasicAttack_WhenTheGaugeIsShort()
    {
        var state = State(Hero(0, energy: 20), Enemy(1));

        var action = Engine().ChooseAuto(state, 0, [Strike("weak", 50, 1.4)]);

        Assert.Null(action.AbilityKey);
        Assert.Equal(1, action.TargetIndex);
    }

    /// <summary>Лікування в повну команду — змарнована енергія, тож автобій його не бере.</summary>
    [Fact]
    public void ChooseAuto_ShouldNotHeal_AHealthyTeam()
    {
        var state = State(Hero(0, energy: 100), Enemy(1));
        var heal = new HeroAbilityConfig
        {
            Key = "mend", DisplayName = "mend", EnergyCost = 100, Target = AbilityTarget.SingleAlly, HealPercent = 0.3,
        };

        Assert.Null(Engine().ChooseAuto(state, 0, [heal]).AbilityKey);
    }

    [Fact]
    public void ChooseAuto_ShouldFinishOffTheWeakestReachableEnemy()
    {
        var state = State(Hero(0), Enemy(1, health: 300), Enemy(2, health: 80));

        var action = Engine().ChooseAuto(state, 0, []);

        Assert.Equal(2, action.TargetIndex);
    }
}
