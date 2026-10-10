using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Результат одного відкриття разом із новим лічильником гарантії.</summary>
    /// <param name="PitySince">Відкриттів поспіль без позиції гарантії — після цього відкриття.</param>
    /// <param name="WasPity">Позицію віддала гарантія, а не кидок.</param>
    public readonly record struct LootboxRoll(LootboxEntryConfig Entry, int PitySince, bool WasPity);

    /// <summary>
    /// Розігрує скриню (GDD §9.12). Чиста функція від таблиці, лічильника гарантії й сіда —
    /// як і банер, тож скаргу «відкрив двадцять і нічого» можна переграти.
    /// </summary>
    public static class LootboxRoller
    {
        public static LootboxRoll Roll(LootboxConfig box, int pitySince, int seed)
        {
            var random = new DeterministicRandom(seed);

            // Порівняння з +1: це відкриття і є тим, на якому гарантія спрацьовує
            var pity = box.PityWithin > 0 && pitySince + 1 >= box.PityWithin;
            var entry = Pick(box, pity ? box.Entries.Where(e => e.Pity) : box.Entries, random);

            // Без гарантії лічильник не потрібен — він лишається нулем
            var since = box.PityWithin == 0 || entry.Pity ? 0 : pitySince + 1;

            return new LootboxRoll(entry, since, pity);
        }

        /// <summary>
        /// Базові шанси позицій у відсотках, у порядку таблиці, — без гарантії, як їх бачить гравець.
        /// </summary>
        public static IReadOnlyList<double> Odds(LootboxConfig box)
        {
            var total = box.Entries.Sum(e => e.Weight);

            return box.Entries
                .Select(e => total > 0 ? Math.Round(e.Weight * 100.0 / total, 2) : 0)
                .ToList();
        }

        /// <summary>
        /// Зважений вибір у порядку таблиці. Порядок з конфіга, а не сортування за ключем,
        /// як у банері: позиції скрині можуть ділити ключ (gems різної кількості).
        /// </summary>
        private static LootboxEntryConfig Pick(LootboxConfig box, IEnumerable<LootboxEntryConfig> pool, IRandomSource random)
        {
            var entries = pool.ToList();
            var total = entries.Sum(e => e.Weight);

            if (total <= 0)
                throw new InvalidOperationException($"Lootbox '{box.Key}' has no entry with positive weight to pick from.");

            var roll = random.Next(total);
            var cumulative = 0;

            foreach (var entry in entries)
            {
                cumulative += entry.Weight;

                if (roll < cumulative)
                    return entry;
            }

            return entries[^1];
        }
    }
}
