namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Детерміноване джерело випадковості з алгоритмом, зафіксованим у цьому файлі:
    /// xoshiro256** із засівом через SplitMix64.
    ///
    /// Існує окремо від <see cref="SystemRandomSource"/> не заради тестів, а заради
    /// переграшу бою. BCL-клас Random не гарантує сталості послідовності між
    /// версіями рантайму: .NET Core вже міняв алгоритм і документація лишає за
    /// собою право змінити його знову. У звіті про бій зберігається лише сід (§5.8),
    /// тож на Random переграш після апгрейду SDK видав би інший результат, ніж
    /// оригінальний бій — і розібрати скаргу стало б неможливо.
    ///
    /// Доки алгоритм нижче не змінюється, той самий сід дає ту саму послідовність
    /// назавжди й на будь-якій платформі. Змінювати його не можна: це зламає
    /// відтворюваність усіх раніше збережених звітів.
    /// </summary>
    public sealed class DeterministicRandom : IRandomSource
    {
        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        public DeterministicRandom(int seed) : this(unchecked((ulong)(uint)seed))
        {
        }

        public DeterministicRandom(ulong seed)
        {
            // SplitMix64 розгортає один сід у чотири слова стану. Без цього кроку
            // близькі сіди (1, 2, 3) дали б помітно корельовані потоки, а сіди
            // боїв ідуть саме послідовними числами.
            var state = seed;

            _s0 = SplitMix64(ref state);
            _s1 = SplitMix64(ref state);
            _s2 = SplitMix64(ref state);
            _s3 = SplitMix64(ref state);

            // xoshiro не виходить із нульового стану. Ймовірність астрономічна,
            // але наслідок — нескінченна послідовність нулів, тож перевіряємо.
            if ((_s0 | _s1 | _s2 | _s3) == 0)
                _s0 = 0x9E3779B97F4A7C15UL;
        }

        /// <summary>Невід'ємне число, менше за maxValue.</summary>
        public int Next(int maxValue)
        {
            if (maxValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be positive.");

            var range = (ulong)maxValue;

            // Відкидання «хвоста», який не ділиться на діапазон націло:
            // просте взяття залишку дало б зсув на користь малих значень.
            var threshold = (ulong.MaxValue - range + 1) % range;

            ulong value;
            do
            {
                value = NextUInt64();
            }
            while (value < threshold);

            return (int)(value % range);
        }

        /// <summary>Число в діапазоні [minValue, maxValue).</summary>
        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must not exceed maxValue.");

            if (minValue == maxValue)
                return minValue;

            // long, бо різниця int.MinValue..int.MaxValue не влазить в int
            var range = (long)maxValue - minValue;

            return (int)(minValue + (long)NextUInt64(checked((ulong)range)));
        }

        /// <summary>Дробове в [0, 1).</summary>
        public double NextDouble()
        {
            // 53 старші біти — рівно стільки значущих має double,
            // тож кожне представиме значення випадає з однаковою ймовірністю.
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>Наступне слово потоку. Ядро xoshiro256**.</summary>
        private ulong NextUInt64()
        {
            var result = RotateLeft(_s1 * 5, 7) * 9;
            var t = _s1 << 17;

            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);

            return result;
        }

        /// <summary>Незміщене число в [0, range) для внутрішніх потреб.</summary>
        private ulong NextUInt64(ulong range)
        {
            var threshold = (ulong.MaxValue - range + 1) % range;

            ulong value;
            do
            {
                value = NextUInt64();
            }
            while (value < threshold);

            return value % range;
        }

        private static ulong RotateLeft(ulong value, int offset)
            => (value << offset) | (value >> (64 - offset));

        /// <summary>Один крок SplitMix64 — використовується лише для засіву.</summary>
        private static ulong SplitMix64(ref ulong state)
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;

                var z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

                return z ^ (z >> 31);
            }
        }
    }
}
