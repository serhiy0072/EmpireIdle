namespace EmpireIdle.Domain.Services
{
    /// <summary>Конфігурація одного типу юніта.</summary>
    public class UnitConfig
    {
        /// <summary>Унікальний ключ юніта (наприклад "infantry").</summary>
        public string Key { get; set; } = null!;

        /// <summary>Відображувана назва.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>Будівля, потрібна для тренування; null — без вимог.</summary>
        public string? RequiresBuilding { get; set; }

        /// <summary>Ціна викупу одного юніта в gems.</summary>
        public int RecoverCostGems { get; set; }

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
}
