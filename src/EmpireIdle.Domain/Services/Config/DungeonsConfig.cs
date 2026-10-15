namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Правила данжів: енергія гравця, склад команди, економіка бою й шкала рівнів.
    /// Числа тут, а не в коді: баланс правиться конфігом без перезбирання.
    /// </summary>
    public class DungeonsConfig
    {
        /// <summary>Стеля енергії данжів. Наповнюється за RegenHours із нуля.</summary>
        public int MaxEnergy { get; set; } = 100;

        /// <summary>За скільки годин шкала заповнюється з нуля до стелі.</summary>
        public double RegenHours { get; set; } = 20;

        /// <summary>
        /// Ціни поповнення енергії до стелі за gems (рішення 12.10.2026): n-те поповнення доби UTC
        /// коштує n-ту ціну, кількість цін — денний ліміт. Порожньо — поповнення вимкнене.
        /// </summary>
        public List<int> RefillPricesGems { get; set; } = new();

        /// <summary>Скільки енергії з'їдає один забіг — незалежно від рівня.</summary>
        public int EnergyPerRun { get; set; } = 10;

        /// <summary>Скільки героїв можна взяти в команду.</summary>
        public int TeamSize { get; set; } = 4;

        /// <summary>Класи героїв, що стають у передню лінію; решта — у задню.</summary>
        public List<string> FrontLineClasses { get; set; } = ["warrior", "knight"];

        /// <summary>Максимальний рівень забігу.</summary>
        public int MaxLevel { get; set; } = 3;

        /// <summary>Скільки звичайних хвиль на рівні 1; кожен наступний рівень додає одну.</summary>
        public int BaseWaves { get; set; } = 1;

        /// <summary>Множник стат ворогів за рівень забігу, починаючи з першого.</summary>
        public List<double> LevelPowerMultipliers { get; set; } = [1.0, 1.8, 3.2];

        /// <summary>Множник ресурсної нагороди за рівень забігу.</summary>
        public List<double> LevelRewardMultipliers { get; set; } = [1.0, 2.0, 3.5];

        /// <summary>
        /// Досвід героїв у спільний запас гравця за успішний забіг рівня 1 (GDD §6.1);
        /// вищі рівні множаться тим самим LevelRewardMultipliers. 0 — досвіду не дають.
        /// </summary>
        public long HeroExperiencePerRun { get; set; }

        /// <summary>
        /// Пом'якшення захисту у формулі шкоди: damage = attack × K / (K + defense).
        /// Що більше K, то слабше захист гасить шкоду.
        /// </summary>
        public double DefenseSoftening { get; set; } = 120;

        /// <summary>Частка шкоди, нижче за яку удар не падає навіть проти величезного захисту.</summary>
        public double MinDamageShare { get; set; } = 0.1;

        /// <summary>Стеля раундів на хвилю: нічия завершує забіг, щоб бій не висів вічно.</summary>
        public int MaxRoundsPerWave { get; set; } = 30;

        /// <summary>Шанс критичного удару без артефактів.</summary>
        public double BaseCritChance { get; set; } = 0.05;

        /// <summary>Множник шкоди на криті без сили криту з наборів і заточки.</summary>
        public double CritMultiplier { get; set; } = 1.6;

        /// <summary>Стеля шансу криту разом із базовим: більше — і бій вирішує кидок, а не склад.</summary>
        public double MaxCritChance { get; set; } = 0.6;

        /// <summary>Стеля вампіризму: частка завданої шкоди, що повертається здоров'ям.</summary>
        public double MaxLifesteal { get; set; } = 0.3;

        /// <summary>Стеля зменшення отриманої шкоди.</summary>
        public double MaxDamageReduction { get; set; } = 0.8;

        /// <summary>Стеля шансу блоку.</summary>
        public double MaxBlockChance { get; set; } = 0.4;

        /// <summary>Скільки шкоди гасить блок без сили блоку: 0.3 — удар слабшає на 30%.</summary>
        public double BaseBlockPower { get; set; } = 0.3;

        /// <summary>Стеля сили блоку разом із базовою.</summary>
        public double MaxBlockPower { get; set; } = 0.8;

        /// <summary>Стеля оберегу: щит на старті забігу часткою максимального здоров'я.</summary>
        public double MaxWard { get; set; } = 0.5;

        /// <summary>Скільки власних ходів героя живе щит оберегу.</summary>
        public int WardTurns { get; set; } = 2;

        /// <summary>Самі данжі.</summary>
        public List<DungeonConfig> Dungeons { get; set; } = new();
    }
}
