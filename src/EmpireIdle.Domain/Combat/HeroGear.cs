namespace EmpireIdle.Domain.Combat
{
    /// <summary>
    /// Бонуси армії зі спорядження героїв (рішення 15.10.2026): «TroopsAttack» і «TroopsDefense»
    /// з порогів наборів і заточки — відсотки юнітам типу класу героя, «MarchSpeed» — відсоток швидкості
    /// маршу, який герой веде. Підсумкові стати героя рахує застосунок (йому потрібен інвентар),
    /// а домен лише читає звідси відсотки.
    /// </summary>
    public sealed class HeroGear
    {
        public const string TroopsAttack = "TroopsAttack";
        public const string TroopsDefense = "TroopsDefense";
        public const string MarchSpeed = "MarchSpeed";

        /// <summary>Без спорядження: бонуси дають лише вміння.</summary>
        public static readonly HeroGear None = new(new Dictionary<Guid, IReadOnlyDictionary<string, double>>());

        private readonly IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, double>> _stats;

        /// <param name="stats">Підсумкові стати кожного героя з вдягненим (HeroStats.Compute).</param>
        public HeroGear(IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, double>> stats)
        {
            _stats = stats;
        }

        /// <summary>Відсоток атаки військам цього героя; 0 — нічого не дає.</summary>
        public double Attack(Guid heroId) => Stat(heroId, TroopsAttack);

        /// <summary>Відсоток захисту військам цього героя.</summary>
        public double Defense(Guid heroId) => Stat(heroId, TroopsDefense);

        /// <summary>Відсоток швидкості маршу від спорядження цього героя.</summary>
        public double Speed(Guid heroId) => Stat(heroId, MarchSpeed);

        private double Stat(Guid heroId, string stat)
            => _stats.TryGetValue(heroId, out var stats) ? stats.GetValueOrDefault(stat) : 0;
    }
}
