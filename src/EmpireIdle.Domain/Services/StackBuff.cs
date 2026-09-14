namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Множники, які герой дає своєму стеку. Порожній примірник —
    /// StackBuff.None — повертає 1.0 на будь-який запит, тож бойова
    /// формула не мусить розрізняти «герой є» і «героя немає».
    /// </summary>
    public sealed class StackBuff
    {
        /// <summary>Жодного бонусу: усі множники 1.0.</summary>
        public static readonly StackBuff None = new(new(), new());

        private readonly Dictionary<string, double> _attack;
        private readonly Dictionary<string, double> _defense;

        internal StackBuff(Dictionary<string, double> attack, Dictionary<string, double> defense)
        {
            _attack = attack;
            _defense = defense;
        }

        /// <summary>Множник атаки для типу юніта (1.0 — без бонусу).</summary>
        public double Attack(string unitType) => Lookup(_attack, unitType);

        /// <summary>Множник захисту для типу юніта.</summary>
        public double Defense(string unitType) => Lookup(_defense, unitType);

        /// <summary>
        /// Бонус на все військо й бонус на конкретний тип складаються:
        /// це різні вміння одного героя, спроєктовані разом, і множити
        /// їх одне на одне означало б випадкову нелінійність.
        /// </summary>
        private static double Lookup(Dictionary<string, double> percents, string unitType)
        {
            var total = percents.GetValueOrDefault(HeroCombatModifiers.AllUnits, 0.0)
                + percents.GetValueOrDefault(unitType, 0.0);

            return 1.0 + total / 100.0;
        }
    }
}
