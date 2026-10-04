namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Рівні звірів (GDD §5.10): скільки корму на рівень і де стеля від рангу.
    /// Окремо від приручення: годування кличе лише це, а екран звіринця — обидва.
    /// </summary>
    public sealed class BeastProgression
    {
        private readonly GameCatalog _catalog;

        public BeastProgression(GameCatalog catalog) => _catalog = catalog;

        /// <summary>Скільки досвіду (корму) треба з рівня <paramref name="level"/> на наступний.</summary>
        public int ExperienceToNext(int level)
        {
            var settings = _catalog.Config.Beasts;

            return (int)Math.Ceiling(settings.BaseExperience * Math.Pow(settings.ExperienceGrowth, Math.Max(0, level - 1)));
        }

        /// <summary>Надбавка пасивки звіра на його рівні: 0.15 — +15%.</summary>
        public double Bonus(Config.BeastConfig beast, int level) => beast.BaseBonus + beast.BonusPerLevel * Math.Max(0, level - 1);

        /// <summary>Стеля рівня для рангу: дублікат піднімає ранг — і стелю разом із ним.</summary>
        public int MaxLevel(int rank) => rank * _catalog.Config.Beasts.LevelsPerRank;
    }
}
