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
}
