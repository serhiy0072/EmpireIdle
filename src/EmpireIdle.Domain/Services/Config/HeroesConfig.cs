namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Загальні параметри системи героїв: тіри, стелі, черга, госпіталь.
    /// Числа тут — предмет фази балансування, механіки від них не залежать.
    /// </summary>
    public class HeroesConfig
    {
        /// <summary>Скільки рівнів героя відкриває один тір. Стеля тіру = Tier × це число.</summary>
        public int LevelsPerTier { get; set; } = 10;

        /// <summary>Найвищий досяжний тір.</summary>
        public int MaxTier { get; set; } = 3;

        /// <summary>
        /// Множник базових стат за тіром, від першого до MaxTier.
        /// Без нього еволюція піднімала б лише стелю, і два герої різних тірів
        /// на десятому рівні були б однакові.
        /// </summary>
        public List<double> TierStatMultipliers { get; set; } = new();

        /// <summary>
        /// Ключі предметів еволюції, від переходу 1→2 і далі.
        /// Записів рівно на один менше за MaxTier.
        /// </summary>
        public List<string> EvolutionItemKeys { get; set; } = new();

        /// <summary>Стеля сузір'я — скільки дублікатів герой поглинає.</summary>
        public int MaxConstellation { get; set; } = 6;

        /// <summary>
        /// Предмет, у який перетворюється дублікат понад стелю сузір'я.
        /// Без нього унікальний дроп на C6 просто зникав би — а з банерами
        /// це щоденна ситуація, не крайній випадок.
        /// </summary>
        public string OverflowShardItemKey { get; set; } = null!;

        /// <summary>
        /// Скільки уламків дає надлишковий дублікат: ключ — назва рангу
        /// (Common, Rare, Unique). Рядком, а не enum: біндер конфігурації
        /// надійно розбирає лише рядкові ключі словника.
        /// </summary>
        public Dictionary<string, int> OverflowShards { get; set; } = new();

        /// <summary>
        /// Жорсткий кап одночасних маршів. Кількість маршів і так дорівнює
        /// кількості вільних героїв, це стеля поверх неї.
        /// </summary>
        public int MaxMarches { get; set; } = 8;

        /// <summary>Скільки хвилин герой лежить у госпіталі за кожен свій рівень.</summary>
        public double HealMinutesPerLevel { get; set; } = 3;

        /// <summary>Ціна миттєвого лікування героя в gems.</summary>
        public int InstantHealCostGems { get; set; } = 20;

        /// <summary>Базовий час підняття рівня, хвилин. Множиться на цільовий рівень.</summary>
        public double BaseLevelUpMinutes { get; set; } = 4;

        /// <summary>
        /// Рівень ратуші, з якого відкривається зала героїв і видається
        /// стартовий герой. Той самий поріг, що й вихід на глобальну карту.
        /// </summary>
        public int UnlockTownHallLevel { get; set; } = 3;

        /// <summary>
        /// Ростер класів. Валідатор звіряє з ним HeroConfig.Class
        /// і придатність зброї — інакше друкарська помилка в JSON
        /// дала б героя, якому не підходить жоден предмет.
        /// </summary>
        public List<string> Classes { get; set; } = new();
    }
}
