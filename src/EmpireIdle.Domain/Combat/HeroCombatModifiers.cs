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
        private readonly HeroSkills _skills;

        public HeroCombatModifiers(GameCatalog catalog)
        {
            _catalog = catalog;
            _skills = new HeroSkills(catalog.Config.HeroSettings);
        }

        /// <summary>Бонуси, які цей герой дає своєму стеку.</summary>
        public StackBuff For(Hero? hero)
        {
            // Бонус гасить поранення й ринок. Deployed — звичайний стан героя, що веде марш,
            // і саме в ньому він б'ється; IsAvailable тут хибний критерій
            if (hero is null || hero.State is HeroState.Wounded or HeroState.OnMarket)
                return StackBuff.None;

            if (!_catalog.Heroes.TryGetValue(hero.HeroKey, out var config) || config.Skills.Count == 0)
                return StackBuff.None;

            var attack = new Dictionary<string, double>();
            var defense = new Dictionary<string, double>();

            // Бонус війську дає кожне відкрите бойове вміння — і пасивка, і активне з періодичним:
            // ті в данжі б'ють самі, а в армійському бою черги ходів немає (GDD §6.1)
            foreach (var skill in config.Skills)
            {
                if (skill.Troops is not { } troops)
                    continue;

                var level = _skills.LevelOf(hero, skill);

                if (level == 0)
                    continue;

                var percent = HeroSkills.At(troops.Percents, level);

                var target = string.IsNullOrWhiteSpace(troops.Target) ? AllUnits : troops.Target;

                var bucket = string.Equals(troops.Stat, "Attack", StringComparison.OrdinalIgnoreCase)
                    ? attack
                    : defense;

                bucket[target] = bucket.GetValueOrDefault(target, 0.0) + percent;
            }

            return new StackBuff(attack, defense);
        }
    }
}
