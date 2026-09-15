namespace EmpireIdle.Domain.Combat
{
    /// <summary>
    /// Розкидає втрати захисника по стеках.
    ///
    /// Ділиться не за кількістю, а за вагою: стек під пасивками свого
    /// лідера тримає удар краще й утрачає менше. Доки пасивок не було,
    /// `archer` господаря й `archer` союзника були однакові, і пропорція
    /// за кількістю була правильною — тепер ні.
    ///
    /// Скільки втратив сам тип, вирішує бій; тут вирішується лише те,
    /// хто з власників за це заплатив.
    /// </summary>
    public class DefenceLossAllocator
    {
        /// <summary>
        /// Розподіляє втрати кожного типу між його стеками.
        /// </summary>
        /// <param name="stacks">Склад оборони на момент бою.</param>
        /// <param name="lossesByType">Скільки юнітів кожного типу втрачено всього.</param>
        /// <param name="buffs">
        /// Пасивки лідерів. Без них поділ вироджується в пропорцію за
        /// кількістю — саме те, що було до героїв.
        /// </param>
        /// <returns>Втрати по стеках; стеки без втрат не повертаються.</returns>
        public IReadOnlyList<StackLoss> Allocate(
            IReadOnlyList<DefenceStack> stacks,
            IReadOnlyDictionary<string, int> lossesByType,
            DefenceBuffs? buffs = null)
        {
            var resolved = buffs ?? DefenceBuffs.None;
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

                result.AddRange(Distribute(members, lost, resolved));
            }

            return result;
        }

        /// <summary>
        /// Найбільші дробові частини за вагою стека. Вага — кількість,
        /// поділена на множник захисту: подвоєний захист означає вдвічі
        /// менші втрати за того самого розміру.
        ///
        /// Ціла частина кожному, залишок — тим, у кого відкинутий дріб
        /// найбільший. Без цього сума втрат по стеках не збіглася б
        /// із загальною, і юніти або створювалися б, або зникали.
        /// </summary>
        private static IEnumerable<StackLoss> Distribute(List<DefenceStack> members, int lost, DefenceBuffs buffs)
        {
            var weights = members
                .Select(s => s.Count / buffs.For(s.OwnerPlayerId).Defense(s.UnitType))
                .ToList();

            var totalWeight = weights.Sum();

            var exact = members
                .Select((stack, i) => new
                {
                    Stack = stack,
                    Share = weights[i] * lost / totalWeight
                })
                .Select(x => (x.Stack, Whole: (int)Math.Floor(x.Share), Fraction: x.Share - Math.Floor(x.Share)))
                .ToList();

            // Стек не може втратити більше, ніж має: сильна пасивка в сусіда
            // інакше вигнала б цілу частку понад розмір слабкого стека
            for (var i = 0; i < exact.Count; i++)
            {
                if (exact[i].Whole > exact[i].Stack.Count)
                    exact[i] = (exact[i].Stack, exact[i].Stack.Count, 0.0);
            }

            var remainder = lost - exact.Sum(x => x.Whole);

            // Черга на залишок: більший дріб раніше, за рівних — більший стек.
            // Тай-брейк за розміром робить результат детермінованим
            var queue = exact
                .Select((x, index) => (x.Stack, x.Whole, x.Fraction, Index: index))
                .Where(x => x.Whole < x.Stack.Count)
                .OrderByDescending(x => x.Fraction)
                .ThenByDescending(x => x.Stack.Count)
                .ThenBy(x => x.Index)
                .ToList();

            var extra = new int[members.Count];

            for (var i = 0; i < remainder && queue.Count > 0; i++)
            {
                var slot = queue[i % queue.Count];

                if (exact[slot.Index].Whole + extra[slot.Index] < slot.Stack.Count)
                    extra[slot.Index]++;
            }

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
