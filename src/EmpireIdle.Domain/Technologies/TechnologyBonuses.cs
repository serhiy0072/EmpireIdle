using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Technologies
{
    /// <summary>
    /// Сумарні бонуси технологій гравця: стат і ціль → сума. Бонус без цілі діє на все,
    /// тож значення для конкретної цілі — це «для всіх» плюс «саме для неї».
    /// </summary>
    public sealed class TechnologyBonuses
    {
        private readonly Dictionary<(TechnologyStat Stat, string Target), double> _values = new();

        /// <summary>Ключ «для всіх» у словнику — порожній рядок, бо кортеж із null незручний як ключ.</summary>
        private const string All = "";

        /// <summary>Порожній набір. Щоразу новий: набір змінюваний, і спільний екземпляр зіпсував би кожен наступний.</summary>
        public static TechnologyBonuses None => new();

        public void Add(TechnologyStat stat, string? target, double value)
        {
            var key = (stat, target ?? All);
            _values[key] = _values.GetValueOrDefault(key) + value;
        }

        /// <summary>Бонус для цілі: «для всіх» плюс «саме для неї»; без цілі — лише «для всіх».</summary>
        public double Of(TechnologyStat stat, string? target = null)
            => _values.GetValueOrDefault((stat, All)) + (target is null ? 0 : _values.GetValueOrDefault((stat, target)));

        /// <summary>Сума двох наборів — особисті й кланові технології діють разом.</summary>
        public TechnologyBonuses Plus(TechnologyBonuses other)
        {
            var sum = new TechnologyBonuses();

            foreach (var ((stat, target), value) in _values.Concat(other._values))
                sum.Add(stat, target == All ? null : target, value);

            return sum;
        }

        public bool IsEmpty => _values.Count == 0;
    }
}
