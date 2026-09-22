using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Результат спроби заточки.</summary>
    public enum EnhancementOutcome
    {
        /// <summary>Рівень піднявся.</summary>
        Success = 0,

        /// <summary>Рівень не піднявся, предмет цілий.</summary>
        Failure = 1,

        /// <summary>Рівень не піднявся, предмет зламаний.</summary>
        Broken = 2
    }

    /// <summary>
    /// Правила заточки: вартість, шанси, ремонт. Чиста функція від конфіга —
    /// нічого не зберігає й нічого не змінює.
    ///
    /// Кидок винесений сюди, а не в хендлер, щоб крива шансів мала одне
    /// місце й один набір тестів: у хендлері її довелося б перевіряти
    /// через моки репозиторіїв.
    /// </summary>
    public class EnhancementRules
    {
        private readonly EquipmentConfig _config;

        public EnhancementRules(EquipmentConfig config)
        {
            _config = config;
        }

        /// <summary>Вартість переходу з поточного рівня на наступний.</summary>
        public int EnhanceCost(int currentLevel)
            => (int)Math.Round(_config.EnhanceBaseGold * Math.Pow(_config.EnhanceCostGrowth, currentLevel));

        /// <summary>Вартість ремонту зброї, зламаної на цьому рівні.</summary>
        public int RepairGems(int level)
            => _config.RepairGemsBase + _config.RepairGemsPerLevel * Math.Max(0, level);

        /// <summary>
        /// Шанс успіху на поточному рівні. До безпечного рівня — одиниця,
        /// далі спадає лінійно, але не нижче за стелю знизу.
        /// </summary>
        public double SuccessChance(int currentLevel)
        {
            if (currentLevel < _config.SafeEnhancementLevel)
                return 1.0;

            var risky = currentLevel - _config.SafeEnhancementLevel + 1;

            return Math.Max(_config.MinSuccessChance, 1.0 - risky * _config.SuccessDropPerLevel);
        }

        /// <summary>
        /// Розігрує спробу. Два кидки, а не один: невдача й поломка —
        /// різні події, і шанс поломки не має залежати від того,
        /// наскільки саме не пощастило.
        /// </summary>
        public EnhancementOutcome Roll(int currentLevel, IRandomSource random)
        {
            if (random.NextDouble() < SuccessChance(currentLevel))
                return EnhancementOutcome.Success;

            return random.NextDouble() < _config.BreakChanceOnFailure
                ? EnhancementOutcome.Broken
                : EnhancementOutcome.Failure;
        }
    }
}
