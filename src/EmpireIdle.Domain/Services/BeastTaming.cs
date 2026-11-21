using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Шанс приручення (GDD §5.10): базовий за типом звіра плюс бонус за рівень
    /// звіринця, підсилений технологіями звіроловства, зі стелею. Чиста функція — її ж показує діалог маршу до відправки.
    /// </summary>
    public sealed class BeastTaming
    {
        private readonly GameCatalog _catalog;

        public BeastTaming(GameCatalog catalog) => _catalog = catalog;

        /// <summary>Звір, якого дає монстр цього типу; null — монстр не приручається.</summary>
        public BeastConfig? ForMonster(string monsterType) => _catalog.BeastsByMonster.GetValueOrDefault(monsterType);

        /// <param name="penLevel">Рівень звіринця; 0 — звіринця немає.</param>
        /// <param name="technologyBonus">
        /// Технології TameChance (0.1 — шанс у 1.1 раза більший). Множать шанс, а не додають пункти:
        /// +50% до 10% — це 15%, а не 60%. Стеля конфігу лишається над усім.
        /// </param>
        public double ChanceFor(BeastConfig beast, int penLevel, double technologyBonus = 0)
        {
            var settings = _catalog.Config.Beasts;

            var chance = (beast.TameChance + settings.TameChancePerPenLevel * Math.Max(0, penLevel)) * (1 + Math.Max(0, technologyBonus));

            return Math.Min(chance, Math.Min(1.0, beast.TameChance * settings.MaxTameChanceMultiplier));
        }
    }
}
