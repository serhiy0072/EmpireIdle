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
}
