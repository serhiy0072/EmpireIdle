namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Вікно дії буста виробництва. Потрібне саме вікно, а не сам множник:
    /// буфер рахується за період, у якому буст міг початись або скінчитись.
    /// </summary>
    public readonly record struct ProductionBoost(double Multiplier, DateTime StartedAt, DateTime ExpiresAt)
    {
        /// <summary>Буста немає — множник 1.0 на порожньому інтервалі.</summary>
        public static readonly ProductionBoost None = new(1.0, DateTime.MinValue, DateTime.MinValue);

        /// <summary>Скільки хвилин інтервалу [from, to) припадає на дію буста.</summary>
        public double OverlapMinutes(DateTime from, DateTime to)
        {
            var start = StartedAt > from ? StartedAt : from;
            var end = ExpiresAt < to ? ExpiresAt : to;

            return end > start ? (end - start).TotalMinutes : 0;
        }
    }
}
