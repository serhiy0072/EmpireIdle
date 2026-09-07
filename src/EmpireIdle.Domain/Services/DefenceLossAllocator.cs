using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Розкидає втрати захисника по стеках.
    ///
    /// Усередині одного типу всі стеки однакові за якістю — `archer`
    /// господаря і `archer` союзника мають ту саму Defense. Тому тут
    /// розподіл строго пропорційний кількості, а якість юнітів впливає
    /// на те, скільки втратив сам тип, і вирішується раніше, у бою.
    /// </summary>
    public class DefenceLossAllocator
    {
        /// <summary>
        /// Розподіляє втрати кожного типу між його стеками.
        /// </summary>
        /// <param name="stacks">Склад оборони на момент бою.</param>
        /// <param name="lossesByType">Скільки юнітів кожного типу втрачено всього.</param>
        /// <returns>Втрати по стеках; стеки без втрат не повертаються.</returns>
        public IReadOnlyList<StackLoss> Allocate(
            IReadOnlyList<DefenceStack> stacks,
            IReadOnlyDictionary<string, int> lossesByType)
        {
            var result = new List<StackLoss>();

            foreach (var group in stacks.GroupBy(s => s.UnitType))
            {
                if (!lossesByType.TryGetValue(group.Key, out var lost) || lost <= 0)
                    continue;

                var members = group.Where(s => s.Count > 0).ToList();
                var total = members.Sum(s => s.Count);

                if (total == 0)
                    continue;

                // Бій не може вбити більше, ніж стояло в обороні
                lost = Math.Min(lost, total);

                result.AddRange(Distribute(members, lost, total));
            }

            return result;
        }

        /// <summary>
        /// Найбільші дробові частини: кожен стек отримує цілу частину своєї
        /// частки, а залишок дістається тим, у кого відкинутий дріб найбільший.
        /// Без цього сума втрат по стеках не збіглася б із загальною, і юніти
        /// або створювалися б, або зникали.
        /// </summary>
        private static IEnumerable<StackLoss> Distribute(List<DefenceStack> members, int lost, int total)
        {
            var exact = members
                .Select(s => (Stack: s, Share: (double)s.Count * lost / total))
                .Select(x => (x.Stack, Whole: (int)Math.Floor(x.Share), Fraction: x.Share - Math.Floor(x.Share)))
                .ToList();

            var assigned = exact.Sum(x => x.Whole);
            var remainder = lost - assigned;

            // Черга на залишок: більший дріб раніше, за рівних — більший стек.
            // Тай-брейк за розміром робить результат детермінованим
            var queue = exact
                .Select((x, index) => (x.Stack, x.Whole, x.Fraction, Index: index))
                .OrderByDescending(x => x.Fraction)
                .ThenByDescending(x => x.Stack.Count)
                .ThenBy(x => x.Index)
                .ToList();

            var extra = new int[members.Count];

            for (var i = 0; i < remainder; i++)
                extra[queue[i].Index]++;

            for (var i = 0; i < exact.Count; i++)
            {
                var totalLost = exact[i].Whole + extra[i];

                if (totalLost <= 0)
                    continue;

                yield return new StackLoss(exact[i].Stack.OwnerPlayerId, exact[i].Stack.UnitType, totalLost);
            }
        }
    }
}
