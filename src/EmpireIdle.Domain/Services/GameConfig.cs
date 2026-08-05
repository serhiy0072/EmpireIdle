namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Конфігурація гри. Визначає назви ресурсів, будівель та їх параметри.
    /// Змінюючи конфіг — отримуєш reskin без зміни коду.
    /// </summary>
    public class GameConfig
    {
        /// <summary>Назва гри (наприклад "EmpireIdle", "SpaceIdle").</summary>
        public string GameName { get; set; } = null!;

        /// <summary>Список ресурсів доступних в грі.</summary>
        public List<ResourceConfig> Resources { get; set; } = new();

        /// <summary>Список типів будівель.</summary>
        public List<BuildingConfig> Buildings { get; set; } = new();

        /// <summary>Список типів зон для будівель.</summary>
        public List<ZoneConfig> Zones { get; set; } = new();

        /// <summary>Усі типи юнітів.</summary>
        public List<UnitConfig> Units { get; set; } = new();

        /// <summary>Параметри карти світу.</summary>
        public MapConfig Map { get; set; } = new();

        /// <summary>Типи монстрів на карті.</summary>
        public List<MonsterConfig> Monsters { get; set; } = new();
        /// <summary>Параметри бою.</summary>
        public CombatConfig Combat { get; set; } = new();
    }

    /// <summary>Конфігурація одного типу ресурсу.</summary>
    public class ResourceConfig
    {
        /// <summary>Унікальний ключ ресурсу (наприклад "gold", "credits").</summary>
        public string Key { get; set; } = null!;

        /// <summary>Відображувана назва (наприклад "Gold", "Credits").</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>Назва іконки для фронтенду.</summary>
        public string Icon { get; set; } = null!;
    }

    /// <summary>Конфігурація одного типу будівлі.</summary>
    public class BuildingConfig
    {
        /// <summary>Унікальний ключ будівлі (наприклад "farm", "mine").</summary>
        public string Key { get; set; } = null!;

        /// <summary>Відображувана назва.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>Який ресурс виробляє ця будівля.</summary>
        public string? ProducesResource { get; set; } = null!;

        /// <summary>Базова кількість ресурсу за хвилину на 1 рівні.</summary>
        public int BaseProductionPerMinute { get; set; }

        /// <summary>Вартість апгрейду на 1 рівні — список пар «ресурс → кількість».
        /// Реальна вартість рівня = кожен Amount × поточний рівень.</summary>
        public List<ResourceCost> Cost { get; set; } = new();

        /// <summary>Базова місткість буфера будівлі на 1 рівні.</summary>
        public int BaseStorage { get; set; }

        /// <summary>Коефіцієнт росту місткості з рівнем. Формула: BaseStorage * StorageGrowth^(рівень-1).</summary>
        public double StorageGrowth { get; set; }

        /// <summary>Базовий час апгрейду на 1 рівні, хвилин.</summary>
        public int BaseBuildMinutes { get; set; }

        /// <summary>Коефіцієнт росту часу апгрейду з рівнем: час = BaseBuildMinutes × BuildTimeGrowth^(рівень−1).</summary>
        public double BuildTimeGrowth { get; set; }
        /// <summary>Зона, у якій дозволено будувати; null — поза зонами (Ратуша, Стіни).</summary>
        public string? AllowedZone { get; set; }

        /// <summary>Мінімальний рівень Ратуші для розблокування будівлі.</summary>
        public int RequiresTownHallLevel { get; set; }

        /// <summary>Скільки населення додає кожен рівень цієї будівлі (0 — не житлова).</summary>
        public int PopulationPerLevel { get; set; }

    }

    /// <summary>Одна складова вартості — скільки якого ресурсу.</summary>
    public class ResourceCost
    {
        public string Resource { get; set; } = null!;
        public int Amount { get; set; }
    }

    /// <summary>Конфігурація типу зони села.</summary>
    public class ZoneConfig
    {
        /// <summary>Тип зони (plain, forest, mountain, water).</summary>
        public string Type { get; set; } = null!;

        /// <summary>Кількість слотів під будівлі у цій зоні.</summary>
        public int Slots { get; set; }
    }

    /// <summary>Конфігурація одного типу юніта.</summary>
    public class UnitConfig
    {
        /// <summary>Унікальний ключ юніта (наприклад "infantry").</summary>
        public string Key { get; set; } = null!;

        /// <summary>Відображувана назва.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>Вартість тренування одного юніта.</summary>
        public List<ResourceCost> Cost { get; set; } = new();

        /// <summary>Час тренування одного юніта, хвилин (партія = ×кількість).</summary>
        public int BaseTrainMinutes { get; set; }

        /// <summary>
        /// Бойові стати: ключ → значення (Attack, Defense, Speed…).
        /// Config-driven: додав стат у JSON — код не змінюється.
        /// </summary>
        public Dictionary<string, double> Stats { get; set; } = new();
    }

    /// <summary>Параметри карти світу (per-server у майбутньому).</summary>
    public class MapConfig
    {
        /// <summary>Ширина карти в клітинах.</summary>
        public int Width { get; set; } = 1000;

        /// <summary>Висота карти в клітинах.</summary>
        public int Height { get; set; } = 1000;

        /// <summary>Рівень світу: гейтить появу типів монстрів (тимчасово з конфіга).</summary>
        public int ServerLevel { get; set; } = 1;

        /// <summary>Скільки клітин карти припадає на одного монстра (щільність спавну).</summary>
        public int CellsPerMonster { get; set; } = 500;

        /// <summary>Сід генерації терейну — той самий сід дає ту саму карту.</summary>
        public int TerrainSeed { get; set; }

        /// <summary>Типи місцевості з їхніми вагами та властивостями.</summary>
        public List<TerrainConfig> Terrains { get; set; } = new();
    }

    /// <summary>Тип місцевості: частота появи та ігрові властивості клітини.</summary>
    public class TerrainConfig
    {
        /// <summary>Ключ типу (plain, forest, water…).</summary>
        public string Type { get; set; } = null!;

        /// <summary>Відносна частота появи на карті.</summary>
        public int Weight { get; set; }

        /// <summary>Чи може армія проходити через клітину.</summary>
        public bool Passable { get; set; } = true;

        /// <summary>Множник часу проходу (1.0 — звичайний, 2.0 — удвічі повільніше).</summary>
        public double MoveCost { get; set; } = 1.0;

        /// <summary>Чи можна розміщувати село або монстра.</summary>
        public bool Habitable { get; set; } = true;
    }

    /// <summary>Конфігурація типу монстра.</summary>
    public class MonsterConfig
    {
        /// <summary>Унікальний ключ (wolf, orc…).</summary>
        public string Key { get; set; } = null!;

        /// <summary>Відображувана назва.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>Мінімальний рівень монстра цього типу.</summary>
        public int MinLevel { get; set; } = 1;

        /// <summary>Максимальний рівень монстра цього типу.</summary>
        public int MaxLevel { get; set; } = 1;

        /// <summary>Мінімальний рівень сервера, з якого тип з'являється на карті.</summary>
        public int RequiresServerLevel { get; set; }

        /// <summary>Склад загону монстра на MinLevel.</summary>
        public List<UnitStack> Units { get; set; } = new();

        /// <summary>Коефіцієнт росту кількості юнітів з рівнем.</summary>
        public double UnitGrowth { get; set; } = 1.5;

        /// <summary>Нагорода за перемогу на 1 рівні.</summary>
        public List<ResourceCost> Rewards { get; set; } = new();

        /// <summary>Коефіцієнт росту нагороди з рівнем.</summary>
        public double RewardGrowth { get; set; } = 1.5;
    }
    /// <summary>Склад загону: тип юніта і кількість.</summary>
    public class UnitStack
    {
        public string UnitType { get; set; } = null!;
        public int Count { get; set; }
    }

    /// <summary>Параметри бойової системи.</summary>
    public class CombatConfig
    {
        /// <summary>Нижня межа випадкового множника.</summary>
        public double RandomMin { get; set; } = 0.7;

        /// <summary>Верхня межа випадкового множника.</summary>
        public double RandomMax { get; set; } = 1.4;

        /// <summary>Розкид нормального розподілу навколо 1.0.</summary>
        public double RandomSigma { get; set; } = 0.15;

        /// <summary>Бонуси типів юнітів на різній місцевості.</summary>
        public List<TerrainBonus> TerrainBonuses { get; set; } = new();
    }

    /// <summary>Модифікатор сили типу юніта на певній місцевості.</summary>
    public class TerrainBonus
    {
        public string Terrain { get; set; } = null!;
        public string UnitType { get; set; } = null!;

        /// <summary>Множник сили (1.25 = +25%).</summary>
        public double Modifier { get; set; } = 1.0;
    }
}
