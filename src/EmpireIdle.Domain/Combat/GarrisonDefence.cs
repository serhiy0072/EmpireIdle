using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Combat
{
    /// <summary>
    /// Хто боронить село (рішення 15.10.2026): лідера гарнізону більше немає — у бій сам іде найсильніший
    /// герой кожного класу, до GarrisonDefenders героїв. Склад рахується на момент бою, тож змінюється
    /// сам, а марш на нього не впливає: герой у дорозі теж боронить дім.
    /// </summary>
    public static class GarrisonDefence
    {
        /// <param name="candidates">Герої господаря з класом і силою зі спорядженням.</param>
        /// <param name="count">Скільки героїв боронить.</param>
        public static List<Hero> Pick(IEnumerable<(Hero Hero, string Class, double Power)> candidates, int count)
        {
            // Поранений лежить у госпіталі: він не боронить і бонусу не дає
            var bestOfEachClass = candidates
                .Where(c => c.Hero.State != HeroState.Wounded)
                .GroupBy(c => c.Class)
                .Select(g => Strongest(g).First());

            return Strongest(bestOfEachClass)
                .Take(count)
                .Select(c => c.Hero)
                .ToList();
        }

        /// <summary>За рівної сили — хто прийшов раніше: склад не стрибає між однаковими героями.</summary>
        private static IOrderedEnumerable<(Hero Hero, string Class, double Power)> Strongest(
            IEnumerable<(Hero Hero, string Class, double Power)> candidates)
            => candidates
                .OrderByDescending(c => c.Power)
                .ThenBy(c => c.Hero.AcquiredAt)
                .ThenBy(c => c.Hero.Id);
    }
}
