namespace EmpireIdle.Domain.Services
{
    /// <summary>Числа кланової механіки.</summary>
    public class ClanConfig
    {
        /// <summary>Скільки учасників вміщає клан 1 рівня.</summary>
        public int BaseCapacity { get; set; } = 20;

        /// <summary>Наскільки місткість росте за кожен рівень клану.</summary>
        public int CapacityPerLevel { get; set; } = 5;

        /// <summary>Найбільша частка таймера, яку може зрізати клан — інакше продаж прискорень помирає.</summary>
        public double MaxHelpShare { get; set; } = 0.4;

        /// <summary>Скільки днів неактивності лідера запускає автопередачу.</summary>
        public int LeaderInactivityDays { get; set; } = 7;
    }
}
