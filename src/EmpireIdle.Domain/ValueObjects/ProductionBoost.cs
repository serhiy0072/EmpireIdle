namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Вікна бустів виробництва. Потрібні саме вікна, а не множник: буфер рахується
    /// за період, у якому буст міг початись або скінчитись.
    ///
    /// Вікон кілька, бо бусти складаються додаванням (GDD §5.10): буст крамниці ×2 і
    /// звір +15% дають ×2.15, а не ×2.3. Кожне вікно несе лише свою надбавку.
    ///
    /// Технології виробітку (рішення 28.10.2026) — не вікно, а постійний множник ресурсу:
    /// ставка × (1 + технології), а бусти крамниці й звірів множать уже її. Зміна рівня технології
    /// матеріалізує виробіток села, тож у межах періоду множник сталий.
    /// </summary>
    public sealed record ProductionBoost(IReadOnlyList<BoostWindow> Windows)
    {
        /// <summary>Технології виробітку «для всіх» ресурсів (0.1 — +10%).</summary>
        public double TechnologyForAll { get; init; }

        /// <summary>Технології виробітку окремих ресурсів — додаються до «для всіх».</summary>
        public IReadOnlyDictionary<string, double> TechnologyByResource { get; init; } = new Dictionary<string, double>();

        /// <summary>Множник ставки ресурсу від технологій; невиробнича будівля — 1.</summary>
        public double TechnologyMultiplier(string? resource)
            => resource is null ? 1 : 1 + TechnologyForAll + TechnologyByResource.GetValueOrDefault(resource);

        /// <summary>Бустів немає.</summary>
        public static readonly ProductionBoost None = new([]);

        /// <summary>Один буст із множником (×2 — надбавка 1.0).</summary>
        public ProductionBoost(double multiplier, DateTime startedAt, DateTime expiresAt)
            : this([new BoostWindow(multiplier - 1, startedAt, expiresAt)])
        {
        }

        /// <summary>Те саме з ще одним вікном.</summary>
        public ProductionBoost With(BoostWindow window) => this with { Windows = [.. Windows, window] };

        /// <summary>
        /// Надбавка за інтервал [from, to) у «хвилинах виробітку»: сума надбавок вікон,
        /// кожна помножена на хвилини свого перекриття з інтервалом.
        /// </summary>
        public double BonusMinutes(DateTime from, DateTime to) => Windows.Sum(w => w.Bonus * w.OverlapMinutes(from, to));
    }

    /// <summary>Одне вікно буста: надбавка до виробітку (0.15 — +15%) і час дії.</summary>
    public readonly record struct BoostWindow(double Bonus, DateTime StartedAt, DateTime ExpiresAt)
    {
        /// <summary>Скільки хвилин інтервалу [from, to) припадає на дію вікна.</summary>
        public double OverlapMinutes(DateTime from, DateTime to)
        {
            var start = StartedAt > from ? StartedAt : from;
            var end = ExpiresAt < to ? ExpiresAt : to;

            return end > start ? (end - start).TotalMinutes : 0;
        }
    }
}
