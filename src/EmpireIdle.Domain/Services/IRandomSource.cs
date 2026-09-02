namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Джерело випадковості. Інтерфейс, а не Random.Shared, бо результат
    /// бачить гравець: роздачу лутбокса, склад втрат чи місце спавну
    /// має бути можливо переграти при розборі скарги.
    ///
    /// У проді — обгортка над Random.Shared; у тестах — сідована реалізація.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Невід'ємне число, менше за maxValue.</summary>
        int Next(int maxValue);

        /// <summary>Число в діапазоні [minValue, maxValue).</summary>
        int Next(int minValue, int maxValue);

        /// <summary>Дробове в [0, 1).</summary>
        double NextDouble();
    }
}
