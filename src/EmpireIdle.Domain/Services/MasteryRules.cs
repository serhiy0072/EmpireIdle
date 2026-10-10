using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Заточка коваля (GDD §9.12): ціна, шанс спроби й поломка. Невдача з'їдає золото, а заточка
    /// не падає; на пізніх рангах невдача може ще й зламати предмет.
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

        /// <summary>Шанс поломки при невдачі з поточної заточки; поза списком — останній відомий.</summary>
        public double BreakChance(int currentMastery)
        {
            var chances = _config.MasteryBreakChances;

            return chances.Count == 0 ? 0.0 : chances[Math.Clamp(currentMastery, 0, chances.Count - 1)];
        }

        /// <summary>Ціна ремонту зламаного предмета в gems.</summary>
        public int RepairGems(int mastery) => _config.RepairGemsPerMastery * mastery;

        /// <summary>
        /// Розігрує спробу. Поломка — окремий кидок і лише після невдачі: так шанс поломки в конфігу
        /// читається як «з невдалих спроб ламається стільки», а не змішується з шансом успіху.
        /// </summary>
        public MasteryOutcome Roll(int currentMastery, IRandomSource random)
        {
            if (random.NextDouble() < SuccessChance(currentMastery))
                return MasteryOutcome.Success;

            return random.NextDouble() < BreakChance(currentMastery) ? MasteryOutcome.Broken : MasteryOutcome.Failed;
        }
    }
}
