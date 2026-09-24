namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Нагороди за вхід (GDD §8.12). Порожні списки — нагороди цього виду
    /// немає. Вміст — заглушки до Режисера.
    /// </summary>
    public class LoginRewardsConfig
    {
        /// <summary>Серія днів: n-й елемент — нагорода n-го дня поспіль.</summary>
        public List<LoginRewardDayConfig> Daily { get; set; } = new();

        public List<RewardConfig> Weekly { get; set; } = new();

        public List<RewardConfig> Monthly { get; set; } = new();
    }
}
