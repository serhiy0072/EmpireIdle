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

        /// <summary>Будівлі, які отримує нове поселення (у порядку створення).</summary>
        public List<string> StartingBuildings { get; set; } = new();

        /// <summary>Максимальний розмір партії тренування.</summary>
        public int MaxTrainingBatchSize { get; set; } = 5;


        /// <summary>Назва гри (наприклад "EmpireIdle", "SpaceIdle").</summary>
        public string GameName { get; set; } = null!;

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

        /// <summary>Усі типи предметів інвентаря.</summary>
        public List<ItemConfig> Items { get; set; } = new();

        /// <summary>Ресурси, з якими починає нове поселення. Ключ — тип ресурсу.</summary>
        public Dictionary<string, int> StartingResources { get; set; } = new();

        /// <summary>Скільки елементів обробляє фоновий сканер за один прогін.</summary>
        public int ScanBatchSize { get; set; } = 500;

        /// <summary>Активні світи. Фонові джоби проходять по кожному.</summary>
        public List<int> ActiveServerIds { get; set; } = new() { 1 };

        /// <summary>Квести з Config/quests.json — усі гілки: intro, military, milestones, daily, server.</summary>
        public List<QuestConfig> Quests { get; set; } = new();

        // <summary>
        /// Скільки юнітів гарнізон може тримати на кожен рівень будівлі, що гейтить юніта (казарми, стайня).
        /// У ліміт входять юніти в гарнізоні та в черзі тренування.
        /// Не входять: юніти в маршах (зняті з гарнізону), поранені
        /// й відновлювані (не б'ються).
        /// </summary>
        public int ArmyCapacityPerBarracksLevel { get; set; }

        /// <summary>
        /// Скільки рівнів будівель відкриває один рівень сервера.
        /// Стеля ратуші = ServerLevel × BuildingLevelsPerTier.
        /// </summary>
        public int BuildingLevelsPerTier { get; set; } = 10;
    }
}
