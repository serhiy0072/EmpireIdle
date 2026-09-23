namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Правила данжів: енергія, склад команди, економіка бою й шкала рівнів.
    /// Числа тут, а не в коді: баланс правиться конфігом без перезбирання.
    /// </summary>
    public class DungeonsConfig
    {
        /// <summary>Стеля енергії данжів. Наповнюється за RegenHours із нуля.</summary>
        public int MaxEnergy { get; set; } = 100;

        /// <summary>За скільки годин шкала заповнюється з нуля до стелі.</summary>
        public double RegenHours { get; set; } = 20;

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

        /// <summary>Скільки енергії дає звичайний удар.</summary>
        public int EnergyPerAttack { get; set; } = 25;

        /// <summary>Скільки енергії додає отриманий удар — бита команда теж накопичує.</summary>
        public int EnergyPerHitTaken { get; set; } = 10;

        /// <summary>
        /// Пом'якшення захисту у формулі шкоди: damage = attack × K / (K + defense).
        /// Що більше K, то слабше захист гасить шкоду.
        /// </summary>
        public double DefenseSoftening { get; set; } = 120;

        /// <summary>Частка шкоди, нижче за яку удар не падає навіть проти величезного захисту.</summary>
        public double MinDamageShare { get; set; } = 0.1;

        /// <summary>Стеля ходів на хвилю: нічия рахується поразкою, щоб бій не висів вічно.</summary>
        public int MaxRoundsPerWave { get; set; } = 30;

        /// <summary>Шанс критичного удару без артефактів.</summary>
        public double BaseCritChance { get; set; } = 0.05;

        /// <summary>Множник шкоди на криті.</summary>
        public double CritMultiplier { get; set; } = 1.6;

        /// <summary>Самі данжі.</summary>
        public List<DungeonConfig> Dungeons { get; set; } = new();
    }
}
