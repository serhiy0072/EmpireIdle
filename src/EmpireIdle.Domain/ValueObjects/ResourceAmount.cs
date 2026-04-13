namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Кількість ресурсу. Value Object — незмінний, порівнюється за значенням.
    /// </summary>
    public class ResourceAmount
    {
        /// <summary>Числове значення кількості ресурсу.</summary>
        public int Value { get; }

        /// <summary>Нульова кількість ресурсу.</summary>
        public static readonly ResourceAmount Zero = new(0);

        /// <param name="value">Кількість. Не може бути від'ємною.</param>
        public ResourceAmount(int value)
        {
            if(value < 0)
                throw new ArgumentException("Кількість ресурсу не може бути від'ємною.", nameof(value));
            Value = value;
        }

        /// <summary>Додає іншу кількість до поточної.</summary>
        public ResourceAmount Add(ResourceAmount other) => new ResourceAmount(Value + other.Value);

        /// <summary>Віднімає кількість від поточної. Кидає виняток якщо недостатньо.</summary>
        public ResourceAmount Substract(ResourceAmount other)
        {
            if(Value < other.Value)
                throw new InvalidOperationException("Недостатньо ресурсів.");
            return new ResourceAmount(Value - other.Value);
        }

        public override string ToString() => Value.ToString();
    }
}
