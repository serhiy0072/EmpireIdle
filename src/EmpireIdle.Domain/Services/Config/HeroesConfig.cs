using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Загальні параметри системи героїв: тіри, стелі, черга, госпіталь.
    /// Числа тут — предмет фази балансування, механіки від них не залежать.
    /// </summary>
    public class HeroesConfig
    {
        /// <summary>Стеля рівня героя (GDD §6.1): інших меж немає — ні ратуша, ні тір рівень не обмежують.</summary>
        public int MaxLevel { get; set; } = 80;

        /// <summary>
        /// Крива досвіду: перехід із рівня L на L+1 коштує ExperienceBase × L^ExperienceExponent.
        /// Заглушки до Режисера.
        /// </summary>
        public double ExperienceBase { get; set; } = 8;

        /// <inheritdoc cref="ExperienceBase"/>
        public double ExperienceExponent { get; set; } = 2.6;

        /// <summary>
        /// Ріст статів за тір, складним відсотком (GDD §6.1): рідний T(n) = TierGrowth^(n−1).
        /// Формула, а не список: тіри відкриваються з рівнем світу, і список довелося б дописувати щоразу.
        /// </summary>
        public double TierGrowth { get; set; } = 1.10;

        /// <summary>
        /// Штраф за кожен ап тіру: піднятий герой слабший за рідного того самого тіру
        /// в EvolutionPenalty^(кількість апів) раз. Нові герої завжди сильніші за старих улюбленців.
        /// </summary>
        public double EvolutionPenalty { get; set; } = 0.95;

        /// <summary>
        /// Ключі предметів еволюції, від переходу 1→2 і далі. Кожен новий тір додає свій предмет.
        /// </summary>
        public List<string> EvolutionItemKeys { get; set; } = new();

        /// <summary>Найвищий тір — скільки переходів описано, плюс перший.</summary>
        public int MaxTier => EvolutionItemKeys.Count + 1;

        /// <summary>Скільки осколків коштує призов будь-якого героя (GDD §6.1): 10 осколків — це герой.</summary>
        public int SummonShards { get; set; } = 10;

        /// <summary>Скільки зірок має герой.</summary>
        public int MaxStars { get; set; } = 5;

        /// <summary>Скільки частинок у зірці.</summary>
        public int PartsPerStar { get; set; } = 6;

        /// <summary>
        /// Ціна кожної частинки в осколках: зовнішній список — зірки, внутрішній — частинки.
        /// Перша зірка дешева (10 разом), частинка п'ятої — 40. Заглушки до Режисера.
        /// </summary>
        public List<List<int>> StarPartCosts { get; set; } = new();

        /// <summary>Скільки бойової міці дає кожна частинка зірки: 0.05 — +5% до всіх статів.</summary>
        public double StarPartBonus { get; set; } = 0.05;

        /// <summary>
        /// Шматки зброї на кожен рівень (GDD §6.4, §9.12): перший — відкриття (+1). Довжина списку — стеля зброї.
        /// </summary>
        public List<int> WeaponShardCosts { get; set; } = new();

        /// <summary>
        /// Бонус зброї до власних статів героя у відсотках за рідкістю героя: значення на +1…+5,
        /// сумарне, не накопичувальне. Довжина кожного списку — як у WeaponShardCosts.
        /// </summary>
        public Dictionary<Rarity, List<double>> WeaponBonusPercents { get; set; } = new();

        public int MaxWeaponLevel => WeaponShardCosts.Count;

        /// <summary>
        /// Ціна шматка зброї в магазині за gems за рідкістю героя (GDD §9.12). Рідкості без ціни
        /// (унікальні) не продаються — їхні шматки лише зі скринь зброї.
        /// </summary>
        public Dictionary<Rarity, int> WeaponShardPriceGems { get; set; } = new();

        /// <summary>
        /// Стеля рівня вміння (GDD §6.1): 6. Зірки відкривають рівні по одному —
        /// без зірок вміння лише першого рівня, на п'ятій зірці доступний шостий.
        /// </summary>
        public int MaxSkillLevel { get; set; } = 6;

        /// <summary>
        /// Склад вмінь за рідкістю героя (ключ — ім'я Rarity). Рідкість без опису не перевіряється —
        /// так тестові фікстури тримають героїв з одним-двома вміннями.
        /// </summary>
        public Dictionary<string, SkillLayoutConfig> SkillLayouts { get; set; } = new();

        /// <summary>
        /// Книги вмінь: яка книга до якої ролі, рідкості й половини. Порожньо — книг у грі немає
        /// (мінімальні фікстури); інакше кожен герой мусить мати книги для своїх половин.
        /// </summary>
        public List<SkillBookConfig> SkillBooks { get; set; } = new();

        /// <summary>
        /// Який тип юніта веде герой кожної ролі (GDD §6.1): ключ — клас героя, значення — ключ юніта.
        /// Порожньо — марш не обмежує тип юнітів (мінімальні фікстури).
        /// </summary>
        public Dictionary<string, string> RoleUnits { get; set; } = new();

        /// <summary>Скільки юнітів у конвої. Заглушка до Режисера.</summary>
        public int ConvoySize { get; set; } = 100;

        /// <summary>
        /// Скільки конвоїв веде герой за рівнем (GDD §6.1: 2 на 1 рівні, 10 на 70). Порожньо — марш
        /// не обмежує кількість юнітів (мінімальні фікстури).
        /// </summary>
        public List<ConvoyStepConfig> ConvoysByLevel { get; set; } = new();

        /// <summary>Навчальний табір (GDD §6.1).</summary>
        public TrainingCampConfig TrainingCamp { get; set; } = new();

        /// <summary>Скільки частинок має повністю прокачаний герой.</summary>
        public int MaxStarParts => MaxStars * PartsPerStar;

        /// <summary>
        /// Обмін універсальних осколків на рідкість вищу (GDD §6.1): ключ — рідкість, з якої міняють,
        /// значення — скільки треба за один осколок наступної. Відкривається, коли всі герої цієї
        /// рідкості прокачані до кінця.
        /// </summary>
        public Dictionary<string, int> UniversalShardUpgrade { get; set; } = new();

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

        /// <summary>
        /// Швидкість героя, якщо тип її не задає. Дорівнює швидкості
        /// найшвидших юнітів, щоб герой без явного стата колону не гальмував.
        /// </summary>
        public double DefaultMarchSpeed { get; set; } = 6.0;
    }
}
