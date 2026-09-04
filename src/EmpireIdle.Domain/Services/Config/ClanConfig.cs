namespace EmpireIdle.Domain.Services
{
    /// <summary>Числа кланової механіки.</summary>
    public class ClanConfig
    {
        /// <summary>Скільки учасників вміщає клан. Фіксована — рівнів клану немає.</summary>
        public int Capacity { get; set; } = 200;

        /// <summary>Найбільша частка таймера, яку може зрізати клан — інакше продаж прискорень помирає.</summary>
        public double MaxHelpShare { get; set; } = 0.4;

        /// <summary>Яку частку повного таймера зрізає одна допомога.</summary>
        public double HelpSharePerPlayer { get; set; } = 0.02;

        /// <summary>Скільки годин живе запит на допомогу.</summary>
        public int HelpRequestLifetimeHours { get; set; } = 24;

        /// <summary>Скільки днів неактивності лідера запускає автопередачу.</summary>
        public int LeaderInactivityDays { get; set; } = 7;
    }
}
