using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Combat
{
    /// <summary>
    /// Крит героя поза данжем (рішення 16.10.2026): армійський бій ударів не кидає, тож крит входить
    /// у силу героя очікуваною шкодою — атака × (1 + шанс × (множник − 1)). Шанс і множник ті самі,
    /// що й у данжі: прихована база BaseCritChance плюс CritChance героя, множник CritMultiplier плюс CritPower.
    /// </summary>
    public static class HeroCrit
    {
        public const string CritChance = "CritChance";
        public const string CritPower = "CritPower";

        /// <param name="heroStats">Підсумкові стати героя (HeroStats.Compute) — відсотки, як у конфігу.</param>
        public static double DamageMultiplier(IReadOnlyDictionary<string, double> heroStats, DungeonsConfig config)
        {
            var chance = Math.Min(config.MaxCritChance, config.BaseCritChance + heroStats.GetValueOrDefault(CritChance) / 100);
            var multiplier = config.CritMultiplier + heroStats.GetValueOrDefault(CritPower) / 100;

            return 1 + chance * (multiplier - 1);
        }
    }
}
