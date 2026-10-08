using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Вміння конкретного героя (GDD §6.1, рішення 08.10.2026): чи відкрите й якого рівня.
    /// Дві осі, що доповнюють одна одну: рівень героя відкриває вміння, зірки стелять його рівень.
    /// Чиста функція від конфіга й стану героя — нічого не зберігає.
    /// </summary>
    public class HeroSkills
    {
        private readonly HeroesConfig _config;

        public HeroSkills(HeroesConfig config)
        {
            _config = config;
        }

        public static bool IsUnlocked(Hero hero, HeroSkillConfig skill) => hero.Level >= skill.UnlockLevel;

        /// <summary>
        /// Рівень вміння, що діє зараз; 0 — вміння ще закрите рівнем героя. Піднятий книгами рівень
        /// не губиться при скиданні рівня героя: вміння знову відкриється вже з ним.
        /// </summary>
        public int LevelOf(Hero hero, HeroSkillConfig skill)
            => IsUnlocked(hero, skill) ? Math.Min(hero.SkillLevel(skill.Key), _config.MaxSkillLevel) : 0;

        /// <summary>Книга, що піднімає вміння цієї половини в героя такої ролі й рідкості; null — книги немає.</summary>
        public SkillBookConfig? BookFor(HeroConfig hero, SkillHalf half)
            => _config.SkillBooks.FirstOrDefault(b => b.Class == hero.Class && b.Rarity == hero.Rank && b.Half == half);

        /// <summary>
        /// До якого рівня вміння можна дійти з поточними зірками: зірки + 1, але не вище MaxSkillLevel.
        /// Без зірок — лише перший рівень, на п'ятій зірці — шостий.
        /// </summary>
        public int LevelCap(Hero hero)
            => Math.Min(_config.MaxSkillLevel, hero.StarParts / Math.Max(1, _config.PartsPerStar) + 1);

        /// <summary>
        /// Значення з пер-рівневого списку конфіга. Рівень понад довжину списку бере останнє:
        /// стелю рівня змінили, а список ще не дописали — вміння не має обнулитися.
        /// </summary>
        public static double At(IReadOnlyList<double> values, int level)
            => values.Count == 0 || level < 1 ? 0 : values[Math.Min(level, values.Count) - 1];

        /// <summary>Сумарний небойовий бонус героя у відсотках для ефекту <paramref name="effect"/>.</summary>
        public double UtilityPercent(Hero hero, HeroConfig? config, string effect)
        {
            if (config is null)
                return 0;

            return config.Skills
                .Where(s => s.Utility is not null && s.Utility.Effect == effect)
                .Sum(s => At(s.Utility!.Percents, LevelOf(hero, s)));
        }
    }
}
