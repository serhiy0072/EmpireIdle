using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Активне вміння героя для покрокового бою в данжі. У героя їх два:
    /// слабке за половину шкали енергії й сильне за повну. У маршах і обороні
    /// вміння не діють — там працюють пасивки, і це навмисний поділ:
    /// армійський бій рахується однією формулою, без черги ходів.
    /// </summary>
    public class HeroAbilityConfig
    {
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        /// <summary>Скільки енергії з'їдає застосування: 50 — слабке, 100 — сильне.</summary>
        public int EnergyCost { get; set; }

        public AbilityTarget Target { get; set; }

        /// <summary>Множник шкоди від атаки виконавця; 0 — вміння не б'є.</summary>
        public double DamageMultiplier { get; set; }

        /// <summary>Лікування як частка від максимального здоров'я цілі; 0 — не лікує.</summary>
        public double HealPercent { get; set; }

        /// <summary>Щит як частка від максимального здоров'я цілі; 0 — не дає щита.</summary>
        public double ShieldPercent { get; set; }

        /// <summary>Стан, який накладає вміння. null — жодного.</summary>
        public BattleStatusKind? Status { get; set; }

        /// <summary>Сила стану: шкода отрути за хід або частка множника для бафів.</summary>
        public double StatusMagnitude { get; set; }

        /// <summary>Скільки ходів тримається стан.</summary>
        public int StatusTurns { get; set; }

        /// <summary>
        /// Дістає задню лінію попри живу передню — ознака далекобійних
        /// і магічних умінь.
        /// </summary>
        public bool IgnoresLine { get; set; }
    }
}
