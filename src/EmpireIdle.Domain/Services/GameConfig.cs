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
        public string ProducesResource { get; set; } = null!;

        /// <summary>Базова кількість ресурсу за хвилину на 1 рівні.</summary>
        public int BaseProductionPerMinute { get; set; }
    }
}
