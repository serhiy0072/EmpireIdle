namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Корм, який з'їдає звіринець (рішення 13.10.2026, корм замість їжі — 20.10.2026): що більший звіринець,
    /// що більше звірів і що вищі їхні рівні — то більше. Не вистачило — голодні звірі, а не село.
    /// </summary>
    public class BeastUpkeepConfig
    {
        /// <summary>Корму за годину за кожен рівень будівлі звіринця.</summary>
        public int PerPenLevelPerHour { get; set; }

        /// <summary>Корму за годину за кожного звіра.</summary>
        public int PerBeastPerHour { get; set; }

        /// <summary>Корму за годину за кожен рівень кожного звіра.</summary>
        public int PerBeastLevelPerHour { get; set; }

        /// <summary>Скільки корму за годину з'їдає звіринець такого рівня з такими звірами.</summary>
        public int HourlyUpkeep(int penLevel, IEnumerable<int> beastLevels)
            => penLevel <= 0
                ? 0
                : PerPenLevelPerHour * penLevel + beastLevels.Sum(level => PerBeastPerHour + PerBeastLevelPerHour * level);
    }
}
