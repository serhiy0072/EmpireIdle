
namespace EmpireIdle.Domain.Services
{

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

        /// <summary>Базовий час апгрейду на 1 рівні, хвилин.</summary>
        public int BaseBuildMinutes { get; set; }

        /// <summary>Коефіцієнт росту часу апгрейду з рівнем: час = BaseBuildMinutes × BuildTimeGrowth^(рівень−1).</summary>
        public double BuildTimeGrowth { get; set; }

        /// <summary>Мінімальний рівень головної будівлі для розблокування.</summary>
        public int RequiresMainBuildingLevel { get; set; }

        /// <summary>Місткість поранених на рівень (0 — не госпіталь).</summary>
        public int WoundedCapacityPerLevel { get; set; }

        /// <summary>
        /// Головна будівля поселення — її рівень гейтить розблокування решти.
        /// Рівно одна на конфіг (townhall / command_center / bunker).
        /// </summary>
        public bool IsMainBuilding { get; set; }

        /// <summary>
        /// Позиція будівлі на карті селища. Домен її не використовує —
        /// це презентація, але тримається тут, щоб при reskin не синхронізувати два джерела.
        /// </summary>
        public BuildingPosition? Position { get; set; }

        /// <summary>
        /// Які ресурси зберігає ця будівля. Порожньо для невиробничих.
        /// Склад тримає базові ресурси, банк — золото.
        /// </summary>
        public List<string>? StoresResources { get; set; }

        /// <summary>
        /// Коефіцієнт росту вартості апгрейду: вартість = Cost × UpgradeCostGrowth^(рівень−1).
        /// Тримати нижчим за BuildTimeGrowth — тоді вузьке горло плавно
        /// мігрує з ресурсів на час, і прискорення продаються природно.
        /// </summary>
        public double UpgradeCostGrowth { get; set; }

    }

}
