using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Зважений вибір — спільне ядро ролерів банерів, скринь, артефактів і монет клану. Тести тримають
/// саме ті властивості, на які спираються сіди: один кидок на вибір і межі, що падають на наступний лот.
/// </summary>
public class WeightedPickTests
{
    /// <summary>Джерело з наперед заданими кидками; лічить, скільки разів його спитали.</summary>
    private sealed class Scripted(int next = 0, double fraction = 0) : IRandomSource
    {
        public int Calls { get; private set; }

        public int Next(int maxValue)
        {
            Calls++;
            return next;
        }

        public int Next(int minValue, int maxValue) => throw new NotSupportedException();

        public double NextDouble()
        {
            Calls++;
            return fraction;
        }
    }

    private static readonly (string Key, int Weight)[] Lots = [("a", 2), ("b", 3), ("c", 5)];

    /// <summary>Кидок 0–1 — перший лот, 2–4 — другий, 5–9 — третій: межа належить наступному.</summary>
    [Theory]
    [InlineData(0, "a")]
    [InlineData(1, "a")]
    [InlineData(2, "b")]
    [InlineData(4, "b")]
    [InlineData(5, "c")]
    [InlineData(9, "c")]
    public void ByInteger_ShouldWalkTheCumulativeWeights(int roll, string expected)
    {
        var random = new Scripted(next: roll);

        Assert.Equal(expected, WeightedPick.ByInteger(Lots, l => l.Weight, random).Key);
        Assert.Equal(1, random.Calls);
    }

    /// <summary>Лот із нульовою вагою не випадає ніколи, навіть на межі.</summary>
    [Fact]
    public void ByInteger_ShouldSkipZeroWeight()
    {
        (string Key, int Weight)[] lots = [("never", 0), ("always", 4)];

        Assert.Equal("always", WeightedPick.ByInteger(lots, l => l.Weight, new Scripted(next: 0)).Key);
    }

    [Theory]
    [InlineData(0.0, "a")]
    [InlineData(0.19, "a")]
    [InlineData(0.2, "b")]
    [InlineData(0.49, "b")]
    [InlineData(0.5, "c")]
    [InlineData(0.999, "c")]
    public void ByFraction_ShouldWalkTheCumulativeWeights(double roll, string expected)
    {
        (string Key, double Weight)[] lots = [("a", 2), ("b", 3), ("c", 5)];
        var random = new Scripted(fraction: roll);

        Assert.Equal(expected, WeightedPick.ByFraction(lots, l => l.Weight, random, fallback: default).Key);
        Assert.Equal(1, random.Calls);
    }

    /// <summary>Порожній список віддає запасне значення, але кидок робиться: послідовність сіда не зсувається.</summary>
    [Fact]
    public void ByFraction_ShouldRollEvenWhenThereIsNothingToPick()
    {
        var random = new Scripted(fraction: 0.5);

        Assert.Equal("fallback", WeightedPick.ByFraction<string>([], _ => 1, random, "fallback"));
        Assert.Equal(1, random.Calls);
    }
}
