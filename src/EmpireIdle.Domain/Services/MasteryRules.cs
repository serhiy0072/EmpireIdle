using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Майстерність коваля (GDD §6.4, §9.12): ціна й шанс заточки артефакта. Поломки немає —
    /// невдача лише з'їдає золото, бо в артефакт уже вкладено згодоване спорядження.
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

        /// <summary>Ціна переходу з поточної майстерності на наступну, у золоті.</summary>
        public int Cost(int currentMastery)
            => (int)Math.Round(_config.MasteryBaseGold * Math.Pow(_config.MasteryCostGrowth, currentMastery));

        /// <summary>
        /// Шанс успіху на поточній майстерності. До безпечного рівня — одиниця,
        /// далі спадає лінійно, але не нижче за мінімальний.
        /// </summary>
        public double SuccessChance(int currentMastery)
        {
            if (currentMastery < _config.SafeMasteryLevel)
                return 1.0;

            var risky = currentMastery - _config.SafeMasteryLevel + 1;

            return Math.Max(_config.MinSuccessChance, 1.0 - risky * _config.SuccessDropPerLevel);
        }

        /// <summary>Розігрує спробу: true — майстерність піднялась.</summary>
        public bool Roll(int currentMastery, IRandomSource random)
            => random.NextDouble() < SuccessChance(currentMastery);
    }
}
