namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Кількість coins (ігрова валюта). Value Object — незмінний.
    /// </summary>
    public record CoinAmount
    {
        public int Value { get; }

        public static readonly CoinAmount Zero = new(0);

        public CoinAmount(int value)
        {
            if (value < 0)
                throw new ArgumentException("Кількість coins не може бути від'ємною.", nameof(value));
            Value = value;
        }

        public CoinAmount Add(CoinAmount other) => new(Value + other.Value);

        public CoinAmount Subtract(CoinAmount other)
        {
            if (other.Value > Value)
                throw new InvalidOperationException("Недостатньо coins.");
            return new(Value - other.Value);
        }

        public override string ToString() => Value.ToString();
    }
}
