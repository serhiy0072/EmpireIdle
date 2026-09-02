namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Обгортка над Random.Shared. Потокобезпечна й без стану — реєструється singleton.
    ///
    /// Живе в Domain, а не в Infrastructure, бо потрібна тестам домену:
    /// вони перевіряють межі й придатність клітин, і сідоване джерело
    /// їм нічого не дає — окремий фейк був би зайвим класом.
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        public int Next(int maxValue) => Random.Shared.Next(maxValue);

        public int Next(int minValue, int maxValue) => Random.Shared.Next(minValue, maxValue);

        public double NextDouble() => Random.Shared.NextDouble();
    }
}
