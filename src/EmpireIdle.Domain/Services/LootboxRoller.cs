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
            var pity = PityDue(box, pitySince);
            var entry = Pick(box, pity, seed);

            return new LootboxRoll(entry, Next(box, pitySince, entry.Pity), pity);
        }

        /// <summary>
        /// Чи це відкриття — те, на якому гарантія спрацьовує. Гарантія рахується у відкриттях,
        /// а не в кидках: одне відкриття з Rolls кидків рухає лічильник на одиницю (рішення 12.10.2026).
        /// </summary>
        public static bool PityDue(LootboxConfig box, int pitySince)
            => box.PityWithin > 0 && pitySince + 1 >= box.PityWithin;

        /// <summary>Лічильник після відкриття: позиція гарантії випала — нуль, інакше на одиницю більше.</summary>
        public static int Next(LootboxConfig box, int pitySince, bool pityEntryDropped)
            => box.PityWithin == 0 || pityEntryDropped ? 0 : pitySince + 1;

        /// <summary>Один кидок таблиці; forcePity — кидок лише серед позицій гарантії.</summary>
        public static LootboxEntryConfig Pick(LootboxConfig box, bool forcePity, int seed)
            => Pick(box, forcePity ? box.Entries.Where(e => e.Pity) : box.Entries, new DeterministicRandom(seed));

        /// <summary>
        /// Базові шанси позицій у відсотках, у порядку таблиці, — без гарантії, як їх бачить гравець.
        /// </summary>
        public static IReadOnlyList<double> Odds(LootboxConfig box)
        {
            var total = box.Entries.Sum(e => e.Weight);

            return box.Entries
                // Три знаки: найрідкісніші позиції (есенції в Обозі, 0,025%) не мають округлюватися вниз
                .Select(e => total > 0 ? Math.Round(e.Weight * 100.0 / total, 3) : 0)
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
