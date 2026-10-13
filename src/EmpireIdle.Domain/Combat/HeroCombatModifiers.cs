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
        private readonly HeroConvoys _convoys;

        public HeroCombatModifiers(GameCatalog catalog)
        {
            _catalog = catalog;
            _skills = new HeroSkills(catalog.Config.HeroSettings);
            _convoys = new HeroConvoys(catalog.Config.HeroSettings);
        }

        /// <summary>
        /// Бонуси, які цей герой дає своєму стеку. Так діє лідер гарнізону: його бонус —
        /// на всю оборону, з ціллю кожного вміння як є.
        /// </summary>
        public StackBuff For(Hero? hero)
        {
            var (attack, defense) = Percents(hero);

            return attack.Count == 0 && defense.Count == 0 ? StackBuff.None : new StackBuff(attack, defense);
        }

        /// <summary>
        /// Бонуси героїв маршу (GDD §6.1, рішення 08.10.2026): кожен герой веде конвої своєї ролі,
        /// і його бойові вміння діють лише на них — ціль «усе військо» стає «мої конвої»,
        /// а ціль чужого типу юнітів у марші нічого не дає. Без ролей у конфігу — як For для кожного.
        /// </summary>
        public StackBuff ForMarch(IEnumerable<Hero?> heroes)
        {
            var attack = new Dictionary<string, double>();
            var defense = new Dictionary<string, double>();

            foreach (var hero in heroes)
            {
                var (heroAttack, heroDefense) = Percents(hero);
                var own = hero is null ? null : _convoys.UnitOf(_catalog.FindHero(hero.HeroKey));

                Merge(attack, heroAttack, own);
                Merge(defense, heroDefense, own);
            }

            return attack.Count == 0 && defense.Count == 0 ? StackBuff.None : new StackBuff(attack, defense);
        }

        private static void Merge(Dictionary<string, double> into, Dictionary<string, double> from, string? ownUnit)
        {
            foreach (var (target, percent) in from)
            {
                // Роль не задана — бонус як є; задана — лише на власні конвої
                var key = ownUnit is null ? target : target == AllUnits || target == ownUnit ? ownUnit : null;

                if (key is not null)
                    into[key] = into.GetValueOrDefault(key, 0.0) + percent;
            }
        }

        /// <summary>Сирі відсотки відкритих бойових вмінь героя за ціллю.</summary>
        private (Dictionary<string, double> Attack, Dictionary<string, double> Defense) Percents(Hero? hero)
        {
            var attack = new Dictionary<string, double>();
            var defense = new Dictionary<string, double>();

            // Бонус гасить поранення. Deployed — звичайний стан героя, що веде марш,
            // і саме в ньому він б'ється; IsAvailable тут хибний критерій
            if (hero is null || hero.State == HeroState.Wounded)
                return (attack, defense);

            if (!_catalog.Heroes.TryGetValue(hero.HeroKey, out var config) || config.Skills.Count == 0)
                return (attack, defense);

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

            return (attack, defense);
        }
    }
}
