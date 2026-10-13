namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Їжа, яку з'їдає звіринець (рішення 13.10.2026): що більший звіринець, що більше звірів
    /// і що вищі їхні рівні — то більше. Не вистачило — село голодує.
    /// </summary>
    public class FoodUpkeepConfig
    {
        /// <summary>Їжі за годину за кожен рівень будівлі звіринця.</summary>
        public int PerPenLevelPerHour { get; set; }

        /// <summary>Їжі за годину за кожного звіра.</summary>
        public int PerBeastPerHour { get; set; }

        /// <summary>Їжі за годину за кожен рівень кожного звіра.</summary>
        public int PerBeastLevelPerHour { get; set; }

        /// <summary>Множник захисту голодного села.</summary>
        public double HungerDefenceMultiplier { get; set; } = 1.0;

        /// <summary>Множник видобутку всіх ресурсів, крім їжі, поки село голодує.</summary>
        public double HungerProductionMultiplier { get; set; } = 1.0;

        /// <summary>Скільки їжі за годину з'їдає звіринець такого рівня з такими звірами.</summary>
        public int HourlyUpkeep(int penLevel, IEnumerable<int> beastLevels)
            => penLevel <= 0
                ? 0
                : PerPenLevelPerHour * penLevel + beastLevels.Sum(level => PerBeastPerHour + PerBeastLevelPerHour * level);
    }
}
