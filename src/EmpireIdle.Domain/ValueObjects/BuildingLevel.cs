namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Рівень будівлі. Value Object — незмінний, не може бути нульовим або від'ємним.
    /// </summary>
    public record BuildingLevel
    {
        /// <summary>Числове значення рівня.</summary>
        public int Value { get; }

        /// <summary>Початковий рівень будівлі.</summary>
        public static readonly BuildingLevel Initial = new(1);

        public BuildingLevel(int value)
        {
            // Захисна перевірка, не відмова гравцю: рівень приходить із коду, а не з запиту
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            Value = value;
        }

        /// <summary>Повертає наступний рівень.</summary>
        public BuildingLevel Next() => new BuildingLevel(Value + 1);

        public override string ToString() => Value.ToString();
    }
}
