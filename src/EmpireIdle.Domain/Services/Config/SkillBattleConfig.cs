using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Ефект вміння в покроковому бою данжу: активного (гравець вирішує, коли) чи
    /// періодичного (спрацьовує саме). У маршах і обороні не діє — там армійський бій
    /// рахується однією формулою без черги ходів, і вміння дає лише бонус війську.
    /// </summary>
    public class SkillBattleConfig
    {
        public AbilityTarget Target { get; set; }

        /// <summary>
        /// Скільки власних ходів героя між застосуваннями. Відлік іде й на старті бою:
        /// вміння не готове з першого ходу, як колись порожня шкала енергії.
        /// </summary>
        public int Cooldown { get; set; }

        /// <summary>Множник шкоди від атаки виконавця; 0 — вміння не б'є.</summary>
        public double DamageMultiplier { get; set; }

        /// <summary>Лікування як частка від максимального здоров'я цілі; 0 — не лікує.</summary>
        public double HealPercent { get; set; }

        /// <summary>Щит як частка від максимального здоров'я цілі; 0 — не дає щита.</summary>
        public double ShieldPercent { get; set; }

        /// <summary>
        /// Скільки ходів носія тримається щит. Стан згасає наприкінці ходу носія, тож щит
        /// на себе з 1 зник би в тому ж ході — для Self потрібно щонайменше 2.
        /// </summary>
        public int ShieldTurns { get; set; }

        /// <summary>Стан, який накладає вміння. null — жодного.</summary>
        public BattleStatusKind? Status { get; set; }

        /// <summary>Сила стану: шкода отрути за хід або частка множника для бафів.</summary>
        public double StatusMagnitude { get; set; }

        /// <summary>Скільки ходів тримається стан.</summary>
        public int StatusTurns { get; set; }

        /// <summary>Дістає задню лінію попри живу передню — ознака далекобійних умінь.</summary>
        public bool IgnoresLine { get; set; }

        /// <summary>
        /// Множник шкоди, лікування й щита на кожному рівні вміння, від першого:
        /// [1.0, 1.2, …, 2.0] — це «100–200% шкоди залежно від рівня».
        /// </summary>
        public List<double> LevelScale { get; set; } = new();
    }
}
