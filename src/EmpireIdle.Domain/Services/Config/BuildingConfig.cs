
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

        /// <summary>Місткість поранених на рівень (0 — не госпіталь).</summary>
        public int WoundedCapacityPerLevel { get; set; }

    }

}
