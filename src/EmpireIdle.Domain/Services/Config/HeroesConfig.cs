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
        /// Скільки джемів дає дублікат понад стелю сузір'я: ключ — назва рангу.
        /// Рахується як чверть вартості ролла, тобто чотири надлишкових
        /// дублікати повертають один ролл.
        ///
        /// Це єдине місце, де джеми з'являються не з покупки й не з квесту,
        /// тому числа тут чіпати обережно.
        /// </summary>
        public Dictionary<string, int> OverflowGems { get; set; } = new();

        /// <summary>
        /// Жорсткий кап одночасних маршів. Кількість маршів і так дорівнює
        /// кількості вільних героїв, це стеля поверх неї.
        /// </summary>
        public int MaxMarches { get; set; } = 8;

        /// <summary>
        /// Вартість лікування за один рівень героя. Множиться на рівень:
        /// десятий рівень коштує вдесятеро дорожче за перший.
        /// </summary>
        public List<ResourceCost> HealCostPerLevel { get; set; } = new();

        /// <summary>Будівля, без якої лікувати нікому.</summary>
        public string HealBuildingKey { get; set; } = "hospital";

        /// <summary>Базовий час підняття рівня, хвилин. Множиться на цільовий рівень.</summary>
        public double BaseLevelUpMinutes { get; set; } = 4;

        /// <summary>
        /// Будівля, у якій купуються й качаються герої. Ключем із конфіга,
        /// а не рядком у коді: гейт має мінятися разом із рештою балансу.
        /// </summary>
        public string BuildingKey { get; set; } = "heroeshall";

        /// <summary>
        /// Ростер класів. Валідатор звіряє з ним HeroConfig.Class
        /// і придатність зброї — інакше друкарська помилка в JSON
        /// дала б героя, якому не підходить жоден предмет.
        /// </summary>
        public List<string> Classes { get; set; } = new();
    }
}
