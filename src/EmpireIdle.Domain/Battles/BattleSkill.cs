using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Battles
{
    /// <summary>
    /// Вміння героя в одномоментному бою — те саме CombatSkill данжу з уже вшитим рівнем, але час у секундах:
    /// хід стає SecondsPerTurn секунд, а скорочення перезарядки героя діє і на активне, і на періодичне —
    /// гравець тут нічого не натискає (рішення 16.10.2026: лише авто).
    /// </summary>
    /// <param name="Cooldown">Секунд між спрацюваннями, вже зі скороченням перезарядки.</param>
    public sealed record BattleSkill(
        string Key,
        AbilityTarget Target,
        double Cooldown,
        double DamageMultiplier,
        double HealPercent,
        double ShieldPercent,
        double ShieldSeconds,
        BattleStatusKind? Status,
        double StatusMagnitude,
        double StatusSeconds,
        bool IgnoresLine,
        SkillFocus Focus = SkillFocus.Nearest)
    {
        /// <param name="cooldownMultiplier">FighterStats.CooldownMultiplier героя: 0.8 — на 20% швидше.</param>
        public static BattleSkill From(CombatSkill skill, double cooldownMultiplier, RealtimeBattleConfig config)
        {
            var tick = config.TickMilliseconds / 1000.0;

            return new BattleSkill(
                skill.Key,
                skill.Target,
                // Не частіше за тик: вміння з нульовою перезарядкою інакше спрацьовувало б нескінченно
                Math.Max(tick, skill.Cooldown * config.SecondsPerTurn * cooldownMultiplier),
                skill.DamageMultiplier,
                skill.HealPercent,
                skill.ShieldPercent,
                skill.ShieldTurns * config.SecondsPerTurn,
                skill.Status,
                skill.StatusMagnitude,
                skill.StatusTurns * config.SecondsPerTurn,
                skill.IgnoresLine,
                skill.Focus);
        }
    }
}
