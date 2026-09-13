namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Ділить колону, коли посольство вміщає не всіх.
    ///
    /// Ділиться пропорційно, а не за пріоритетом типів: гравець не задавав
    /// порядку, і будь-який вибір «хто цінніший» був би вигадкою за нього.
    /// Залишок від ділення роздається найбільшим дробовим часткам, а при
    /// рівних — за іменем типу, щоб результат не залежав від порядку ключів
    /// у словнику й від прогону до прогону.
    /// </summary>
    public static class ReinforcementSplit
    {
        /// <param name="units">Уся колона.</param>
        /// <param name="slots">Скільки юнітів посольство ще прийме.</param>
        /// <returns>Що лишається в союзника і що йде додому.</returns>
        public static (Dictionary<string, int> Accepted, Dictionary<string, int> Rejected) Take(
            IReadOnlyDictionary<string, int> units, int slots)
        {
            var total = units.Values.Where(c => c > 0).Sum();

            var accepted = new Dictionary<string, int>();
            var rejected = new Dictionary<string, int>();

            if (total == 0 || slots <= 0)
            {
                foreach (var (type, count) in units.Where(u => u.Value > 0))
                    rejected[type] = count;

                return (accepted, rejected);
            }

            if (slots >= total)
            {
                foreach (var (type, count) in units.Where(u => u.Value > 0))
                    accepted[type] = count;

                return (accepted, rejected);
            }

            var shares = units
                .Where(u => u.Value > 0)
                .Select(u => new
                {
                    Type = u.Key,
                    u.Value,
                    Exact = (double)u.Value * slots / total
                })
                .ToList();

            foreach (var share in shares)
                accepted[share.Type] = (int)Math.Floor(share.Exact);

            var left = slots - accepted.Values.Sum();

            foreach (var share in shares
                .OrderByDescending(s => s.Exact - Math.Floor(s.Exact))
                .ThenBy(s => s.Type)
                .Take(left))
                accepted[share.Type]++;

            foreach (var share in shares)
            {
                var back = share.Value - accepted[share.Type];

                if (back > 0)
                    rejected[share.Type] = back;
            }

            foreach (var empty in accepted.Where(a => a.Value <= 0).Select(a => a.Key).ToList())
                accepted.Remove(empty);

            return (accepted, rejected);
        }
    }
}
