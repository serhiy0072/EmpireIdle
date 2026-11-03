using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Combat
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
        /// Той самий бонус плюс бойові технології власника війська (рішення 28.10.2026). Відсотки технологій
        /// стають в один пул із вміннями героя: діють і без героя, і на всі конвої, а не лише на конвої його ролі.
        /// </summary>
        public StackBuff With(TechnologyBonuses technologies)
        {
            var attack = new Dictionary<string, double>(_attack);
            var defense = new Dictionary<string, double>(_defense);

            Add(attack, technologies, TechnologyStat.Attack);
            Add(defense, technologies, TechnologyStat.Defense);

            return attack.Count == 0 && defense.Count == 0 ? None : new StackBuff(attack, defense);
        }

        private static void Add(Dictionary<string, double> percents, TechnologyBonuses technologies, TechnologyStat stat)
        {
            foreach (var (target, value) in technologies.Targets(stat))
            {
                var key = target ?? HeroCombatModifiers.AllUnits;
                percents[key] = percents.GetValueOrDefault(key, 0.0) + value * 100;
            }
        }

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
