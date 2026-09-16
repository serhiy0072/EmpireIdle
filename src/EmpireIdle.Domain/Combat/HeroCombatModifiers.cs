using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Combat
{
    /// <summary>
    /// Збирає бонуси героя його війську. Чиста функція від конфіга
    /// й стану героя: нічого не зберігає, нічого не перераховує.
    ///
    /// Поранений герой не дає нічого. Саме тому поразка болить: лідер
    /// лежить у госпіталі, і гарнізон воює без бонусу, поки його
    /// не вилікують або не призначать іншого.
    /// </summary>
    public class HeroCombatModifiers
    {
        /// <summary>Ціль «усе військо».</summary>
        public const string AllUnits = "all";

        private readonly GameCatalog _catalog;

        public HeroCombatModifiers(GameCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Бонуси, які цей герой дає своєму стеку.</summary>
        public StackBuff For(Hero? hero)
        {
            // Бонус гасить лише поранення. Deployed — звичайний стан героя, що веде марш,
            // і саме в ньому він б'ється; IsAvailable тут хибний критерій
            if (hero is null || hero.State == HeroState.Wounded)
                return StackBuff.None;

            if (!_catalog.Heroes.TryGetValue(hero.HeroKey, out var config) || config.Passives.Count == 0)
                return StackBuff.None;

            var attack = new Dictionary<string, double>();
            var defense = new Dictionary<string, double>();

            foreach (var passive in config.Passives)
            {
                if (hero.Constellation < passive.UnlockConstellation)
                    continue;

                var percent = passive.BasePercent
                    + passive.PercentPerConstellation * (hero.Constellation - passive.UnlockConstellation);

                var target = string.IsNullOrWhiteSpace(passive.Target) ? AllUnits : passive.Target;

                var bucket = string.Equals(passive.Stat, "Attack", StringComparison.OrdinalIgnoreCase)
                    ? attack
                    : defense;

                bucket[target] = bucket.GetValueOrDefault(target, 0.0) + percent;
            }

            return new StackBuff(attack, defense);
        }
    }
}
