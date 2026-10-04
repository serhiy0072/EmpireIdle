namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Звірі (GDD §5.10): правила приручення й перелік типів.
    /// Усі числа — заглушки до Режисера.
    /// </summary>
    public class BeastsConfig
    {
        /// <summary>Скільки додає до шансу приручення кожен рівень звіринця.</summary>
        public double TameChancePerPenLevel { get; set; }

        /// <summary>Стеля шансу як множник до базового: рівень звіринця не робить приручення гарантованим.</summary>
        public double MaxTameChanceMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Гарантія: після стількох перемог поспіль без звіра наступна перемога його дає.
        /// Лічильник окремий на кожен тип.
        /// </summary>
        public int PityWins { get; set; }

        /// <summary>Найвищий ранг звіра; дублікат понад нього — звичайна здобич.</summary>
        public int MaxRank { get; set; } = 1;

        public List<BeastConfig> Types { get; set; } = new();
    }

    /// <summary>Тип звіра. Приручається з монстра одного типу — і лише з нього.</summary>
    public class BeastConfig
    {
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        /// <summary>Тип монстра, з якого приручається звір (GDD §5.10: тип монстра = тип звіра).</summary>
        public string MonsterKey { get; set; } = null!;

        /// <summary>Базовий шанс приручення за перемогу, (0; 1].</summary>
        public double TameChance { get; set; }
    }
}
