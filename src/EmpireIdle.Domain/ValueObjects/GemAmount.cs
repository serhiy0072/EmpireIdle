namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Кількість gems (преміум валюта). Value Object — незмінний.
    /// </summary>
    public record GemAmount
    {
        /// <summary>Числове значення.</summary>
        public int Value { get; }

        public static readonly GemAmount Zero = new(0);

        /// <param name="value">Кількість gems. Не може бути від'ємною.</param>
        public GemAmount(int value)
        {
            if (value < 0)
                throw new ArgumentException("Кількість gems не може бути від'ємною.", nameof(value));
            Value = value;
        }

        public GemAmount Add(GemAmount other) => new(Value + other.Value);

        public GemAmount Subtract(GemAmount other)
        {
            if (other.Value > Value)
                throw new InvalidOperationException("Недостатньо gems.");
            return new(Value - other.Value);
        }

        public override string ToString() => Value.ToString();
    }
}
