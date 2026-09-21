namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Ключ армійського стеку: тип юніта й рівень разом. Той самий тип на
    /// різних рівнях — різні стеки: гравець може мати 50 піхоти 3 рівня
    /// й 20 щойно навчених 1 рівня одночасно.
    ///
    /// Названо не "UnitStack", щоб не зіткнутися з Services.Config.UnitStack —
    /// той описує склад загону монстра (тип+кількість), не рівень.
    /// </summary>
    public readonly record struct UnitStackKey(string UnitType, int Level)
    {
        public override string ToString() => $"{UnitType}@{Level}";

        /// <summary>
        /// Розбирає ключ у форматі "unitType@level" — так стеки з рівнями
        /// їдуть по дроту в JSON-словниках (HTTP-запити маршів, лікування, викупу).
        /// </summary>
        public static UnitStackKey Parse(string key)
        {
            var separator = key.LastIndexOf('@');

            if (separator < 0 || !int.TryParse(key[(separator + 1)..], out var level))
                throw new FormatException($"Invalid unit stack key '{key}'. Expected format 'unitType@level'.");

            return new UnitStackKey(key[..separator], level);
        }
    }
}
