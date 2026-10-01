using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services
{
    /// <summary>
    /// Перша пара кидків у діапазоні — найбільші можливі (точка на самій межі туману),
    /// далі — середина діапазону. Так тест детерміновано б'є в край карти.
    /// </summary>
    internal sealed class EdgeFirstRandom : IRandomSource
    {
        private int _rangeCalls;

        public int Next(int maxValue) => 0;

        public int Next(int minValue, int maxValue)
            => _rangeCalls++ < 2 ? maxValue - 1 : (minValue + maxValue) / 2;

        public double NextDouble() => 0.5;
    }
}
