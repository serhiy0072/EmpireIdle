using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Вміння героя (GDD §6.1, рішення 08.10.2026). Рівень героя відкриває вміння,
    /// книги піднімають його рівень, зірки стелять, до якого рівня можна дійти.
    ///
    /// Що саме робить вміння, описують окремі частини: бонус війську, ефект у данжі,
    /// небойовий бонус. Яка частина обов'язкова, залежить від Kind — це перевіряє валідатор.
    /// </summary>
    public class HeroSkillConfig
    {
        /// <summary>Ключ, унікальний у межах героя. Ним клієнт називає активне вміння в данжі.</summary>
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        public SkillHalf Half { get; set; }

        public SkillKind Kind { get; set; }

        /// <summary>З якого рівня героя вміння працює. Активне — завжди з першого: без нього данж нічим бити.</summary>
        public int UnlockLevel { get; set; } = 1;

        /// <summary>
        /// Бонус війську в маршах і обороні. Обов'язковий для бойових вмінь (Active, Passive, Periodic),
        /// у небойових його немає.
        /// </summary>
        public SkillTroopBonusConfig? Troops { get; set; }

        /// <summary>Що вміння робить у покроковому бою данжу. Обов'язковий для Active і Periodic.</summary>
        public SkillBattleConfig? Battle { get; set; }

        /// <summary>Небойовий бонус. Обов'язковий для Utility.</summary>
        public SkillUtilityConfig? Utility { get; set; }
    }
}
