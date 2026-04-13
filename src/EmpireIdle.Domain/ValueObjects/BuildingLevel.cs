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
            if (value <= 0)
                throw new ArgumentException("Рівень будівлі має бути більше нуля.", nameof(value));
            Value = value;
        }

        /// <summary>Повертає наступний рівень.</summary>
        public BuildingLevel Next() => new BuildingLevel(Value + 1);

        public override string ToString() => Value.ToString();
    }
}
