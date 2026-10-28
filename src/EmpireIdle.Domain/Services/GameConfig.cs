using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Конфігурація гри. Визначає назви ресурсів, будівель та їх параметри.
    /// Змінюючи конфіг — отримуєш reskin без зміни коду.
    /// </summary>
    public class GameConfig
    {
        /// <summary>Сервер, на який потрапляє новий гравець при реєстрації.</summary>
        public int DefaultServerId { get; set; } = 1;

        /// <summary>
        /// Партія тренування чи прокачки на будівлі першого рівня (рішення 12.10.2026): ліміт росте
        /// з рівнем будівлі юніта лінійно до UnitBatchMax на MaxBuildingLevel.
        /// </summary>
        public int UnitBatchMin { get; set; } = 30;

        /// <summary>Партія тренування чи прокачки на будівлі максимального рівня.</summary>
        public int UnitBatchMax { get; set; } = 150;

        /// <summary>Максимальний рівень юніта.</summary>
        public int MaxUnitLevel { get; set; } = 10;

        /// <summary>
        /// На скільки кожен рівень юніта понад перший підсилює його атаку, захист і здоров'я:
        /// 0.1 — +10% за рівень. Швидкість і вантаж від рівня не ростуть.
        /// </summary>
        public double UnitLevelStatBonus { get; set; } = 0.10;

        /// <summary>Список ресурсів доступних в грі.</summary>
        public List<ResourceConfig> Resources { get; set; } = new();

        /// <summary>Список типів будівель.</summary>
        public List<BuildingConfig> Buildings { get; set; } = new();

        /// <summary>Усі типи юнітів.</summary>
        public List<UnitConfig> Units { get; set; } = new();

        /// <summary>Параметри карти світу.</summary>
        public MapConfig Map { get; set; } = new();

        /// <summary>Типи монстрів на карті.</summary>
        public List<MonsterConfig> Monsters { get; set; } = new();
        /// <summary>Параметри бою: рандом, терейн-модифікатори, частки кошиків втрат.</summary>
        public CombatConfig Combat { get; set; } = new();

        /// <summary>Параметри монетизації: ціни speedup у gems.</summary>
        public MonetizationConfig Monetization { get; set; } = new();

        /// <summary>Асортимент офіційного магазину: пакети gems і лутбокси.</summary>
        public ShopConfig Shop { get; set; } = new();

        /// <summary>Данжі: покроковий бій, енергія, набори артефактів.</summary>
        public DungeonsConfig Dungeons { get; set; } = new();

        /// <summary>Одномоментний бій арени й вежі: тики, формула захисту, перевага типів.</summary>
        public RealtimeBattleConfig RealtimeBattle { get; set; } = new();

        /// <summary>«Тисячна вежа»: крива ворога, склади, нагороди поверхів.</summary>
        public TowerConfig Tower { get; set; } = new();

        /// <summary>Ринок гравців: податок, строки, коридор ціни.</summary>
        public MarketConfig Market { get; set; } = new();

        /// <summary>Звірі: приручення, звіринець, пасивки (GDD §5.10).</summary>
        public BeastsConfig Beasts { get; set; } = new();

        /// <summary>Чат: довжина, антиспам, строк історії.</summary>
        public ChatConfig Chat { get; set; } = new();

        /// <summary>Скринька: строки зберігання листів і оголошень.</summary>
        public MailConfig Mail { get; set; } = new();

        /// <summary>Строки зберігання історії: звіти, завершені забіги, закриті лоти, вирішені заявки.</summary>
        public RetentionConfig Retention { get; set; } = new();

        /// <summary>Нагороди за вхід: серія днів, тиждень, місяць.</summary>
        public LoginRewardsConfig LoginRewards { get; set; } = new();

        /// <summary>Мови інтерфейсу й мова за замовчуванням.</summary>
        public LocalizationConfig Localization { get; set; } = new();

        /// <summary>Переклади назв з конфігів за мовою; мова за замовчуванням їх не потребує.</summary>
        public Dictionary<string, LocaleConfig> Locales { get; set; } = new();

        /// <summary>Усі типи предметів інвентаря.</summary>
        public List<ItemConfig> Items { get; set; } = new();

        /// <summary>Ресурси, з якими починає нове поселення. Ключ — тип ресурсу.</summary>
        public Dictionary<string, int> StartingResources { get; set; } = new();

        /// <summary>Скільки елементів обробляє фоновий сканер за один прогін.</summary>
        public int ScanBatchSize { get; set; } = 500;

        /// <summary>
        /// Як часто вироблене переходить на склад автоматично, секунди (рішення 19.10.2026).
        /// Частіше за тік timer-scan (хвилина) не вийде — сканер просто не встигне.
        /// </summary>
        public int AutoCollectSeconds { get; set; } = 60;

        /// <summary>Активні світи. Фонові джоби проходять по кожному.</summary>
        public List<int> ActiveServerIds { get; set; } = new() { 1 };

        /// <summary>Квести з Config/quests.json — усі гілки: intro, military, milestones, daily, server.</summary>
        public List<QuestConfig> Quests { get; set; } = new();

        /// <summary>
        /// Скільки юнітів свого типу вміщує будівля першого рівня (рішення 13.10.2026): у кожної будівлі юнітів
        /// свій ліміт, що росте геометрично до UnitCapacityMax на MaxBuildingLevel. Марші й поранені не рахуються.
        /// </summary>
        public int UnitCapacityMin { get; set; } = 1500;

        /// <summary>Місткість будівлі юнітів максимального рівня.</summary>
        public int UnitCapacityMax { get; set; } = 300_000;

        /// <summary>Скільки рівнів будівель відкриває один рівень сервера.</summary>
        public int BuildingLevelsPerTier { get; set; } = 10;

        /// <summary>
        /// Абсолютна стеля будівель. Рівень світу росте без межі (GDD §2.7), а ратуша й будівлі —
        /// лише до цього рівня: нові рівні світу відкривають контент, а не поверхи.
        /// </summary>
        public int MaxBuildingLevel { get; set; } = 30;

        /// <summary>Ваги й орієнтири серверного рейтингу.</summary>
        public RatingConfig Rating { get; set; } = new();

        /// <summary>Місткість, кап допомоги, строк неактивності лідера.</summary>
        public ClanConfig Clan { get; set; } = new();

        /// <summary>Усі типи героїв із Config/heroes.json.</summary>
        public List<HeroConfig> Heroes { get; set; } = new();

        /// <summary>Тіри, стелі, черга й госпіталь героїв.</summary>
        public HeroesConfig HeroSettings { get; set; } = new();

        /// <summary>Екіпірування героїв.</summary>
        public EquipmentConfig Equipment { get; set; } = new();

        /// <summary>Скрині: таблиці нагород предметів із Type "lootbox" (GDD §9.12).</summary>
        public List<LootboxConfig> Lootboxes { get; set; } = new();

        /// <summary>Ключові скрині: безкоштовні відкриття на добу, понад них — ключ (рішення 12.10.2026).</summary>
        public List<KeyChestConfig> KeyChests { get; set; } = new();

        /// <summary>Провізія: Їдальня їсть м'ясо, нестача — голод села (рішення 20.10.2026).</summary>
        public ProvisionsConfig Provisions { get; set; } = new();

        /// <summary>Особисті й кланові технології (рішення 28.10.2026).</summary>
        public TechnologiesConfig Technologies { get; set; } = new();
    }
}
