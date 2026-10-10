using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Заточка коваля (GDD §9.12): ціна й шанс спроби. Невдача з'їдає золото, а заточка
    /// не падає.
    ///
    /// Чиста функція від конфіга; кидок винесений сюди, щоб крива шансів мала одне місце
    /// й один набір тестів, а не перевірялась у хендлері через моки.
    /// </summary>
    public class MasteryRules
    {
        private readonly EquipmentConfig _config;

        public MasteryRules(EquipmentConfig config)
        {
            _config = config;
        }

        /// <summary>Ціна спроби з поточної заточки на наступну, у золоті: round10(база · ріст^поточна).</summary>
        public int Cost(int currentMastery)
            => (int)(Math.Round(_config.MasteryBaseGold * Math.Pow(_config.MasteryCostGrowth, currentMastery) / 10) * 10);

        /// <summary>Шанс успіху спроби з поточної заточки; поза списком — останній відомий.</summary>
        public double SuccessChance(int currentMastery)
        {
            var chances = _config.MasterySuccessChances;

            return chances.Count == 0 ? 1.0 : chances[Math.Clamp(currentMastery, 0, chances.Count - 1)];
        }

        /// <summary>Розігрує спробу: true — заточка піднялась.</summary>
        public bool Roll(int currentMastery, IRandomSource random)
            => random.NextDouble() < SuccessChance(currentMastery);
    }
}
