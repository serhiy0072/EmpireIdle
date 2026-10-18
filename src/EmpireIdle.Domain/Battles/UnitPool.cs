using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Battles
{
    /// <summary>
    /// Юніти гарнізону, з яких загін набирає конвої. Герої однієї ролі ділять спільний пул:
    /// хто стоїть у загоні раніше, той бере першим, і завжди найвищий рівень наперед.
    /// Пул — лише рахунок на один бій: загальна кількість юнітів гравця не змінюється (рішення 16.10.2026).
    /// </summary>
    public sealed class UnitPool
    {
        private readonly Dictionary<UnitStackKey, int> _left;

        public UnitPool(IReadOnlyDictionary<UnitStackKey, int> units)
        {
            _left = units.Where(u => u.Value > 0).ToDictionary(u => u.Key, u => u.Value);
        }

        /// <summary>Порожній пул — для ворогів, чиї конвої завжди повні, і для тестів.</summary>
        public static UnitPool Empty => new(new Dictionary<UnitStackKey, int>());

        /// <summary>Скільки юнітів цього типу ще не розібрали.</summary>
        public int Left(string unitType) => _left.Where(u => u.Key.UnitType == unitType).Sum(u => u.Value);

        /// <summary>Забирає до count юнітів типу — найвищі рівні першими. Повертає, скільки якого рівня взято.</summary>
        public IReadOnlyList<(int Level, int Count)> Take(string unitType, int count)
        {
            var taken = new List<(int Level, int Count)>();

            foreach (var key in _left.Keys.Where(k => k.UnitType == unitType).OrderByDescending(k => k.Level).ToList())
            {
                if (count == 0)
                    break;

                var amount = Math.Min(count, _left[key]);
                taken.Add((key.Level, amount));
                count -= amount;

                if (_left[key] == amount)
                    _left.Remove(key);
                else
                    _left[key] -= amount;
            }

            return taken;
        }
    }
}
