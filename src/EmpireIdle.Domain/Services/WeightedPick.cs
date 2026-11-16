namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Зважений вибір — одне місце замість копії в кожному ролері. Два варіанти не зводяться до одного навмисно:
    /// цілі ваги тягнуть ціле число (<see cref="IRandomSource.Next(int)"/>), дробові — дріб
    /// (<see cref="IRandomSource.NextDouble"/>). Той самий сід має давати той самий лот, що й до об'єднання,
    /// тож кожен варіант повторює свій старий алгоритм крок у крок, зокрема один кидок на вибір.
    /// Порядок елементів — частина результату: хто сортує (банер) чи ні (скриня), вирішує викликач.
    /// </summary>
    public static class WeightedPick
    {
        /// <summary>Вибір за цілими вагами. Сума ваг має бути додатна — перевіряє викликач, бо лише він знає, що сказати в помилці.</summary>
        public static T ByInteger<T>(IReadOnlyList<T> items, Func<T, int> weight, IRandomSource random)
        {
            var total = 0;

            foreach (var item in items)
                total += weight(item);

            var roll = random.Next(total);
            var cumulative = 0;

            foreach (var item in items)
            {
                cumulative += weight(item);

                if (roll < cumulative)
                    return item;
            }

            // Недосяжно: roll < total
            return items[^1];
        }

        /// <summary>
        /// Вибір за дробовими вагами. Кидок робиться завжди, навіть для порожнього списку: джерело випадковості
        /// має зсунутись однаково, є з чого обирати чи ні. Порожній список повертає <paramref name="fallback"/>.
        /// </summary>
        public static T ByFraction<T>(IReadOnlyList<T> items, Func<T, double> weight, IRandomSource random, T fallback)
        {
            var total = 0.0;

            foreach (var item in items)
                total += weight(item);

            var roll = random.NextDouble() * total;

            foreach (var item in items)
            {
                var share = weight(item);

                if (roll < share)
                    return item;

                roll -= share;
            }

            // Похибка double на останньому кроці — беремо останній
            return items.Count == 0 ? fallback : items[^1];
        }
    }
}
