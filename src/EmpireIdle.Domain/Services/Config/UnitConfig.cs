
namespace EmpireIdle.Domain.Services.Config
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

        /// <summary>
        /// Час тренування одного юніта 1 рівня, секунд (партія = ×кількість). Секунди, а не хвилини:
        /// партія з 150 юнітів 1 рівня триває 10 хвилин — 4 секунди на юніта (рішення 13.10.2026).
        /// </summary>
        public int BaseTrainSeconds { get; set; }

        /// <summary>
        /// Ріст часу тренування й прокачки на 1 рівень юніта — окремо від ціни (LevelUpCostGrowth):
        /// партія 10 рівня має тренуватись 2,5 години, а не дні, але коштувати як і раніше.
        /// </summary>
        public double TrainTimeGrowth { get; set; } = 1.35;

        /// <summary>
        /// Мінімальний рівень будівлі з RequiresBuilding.
        /// Рівень, а не сама наявність: інакше казарма 1 рівня відкриває
        /// всю армію одразу, і качати її немає причин.
        /// </summary>
        public int RequiresBuildingLevel { get; set; } = 1;

        /// <summary>
        /// Бойові стати: ключ → значення (Attack, Defense, Speed…).
        /// Config-driven: додав стат у JSON — код не змінюється.
        /// </summary>
        public Dictionary<string, double> Stats { get; set; } = new();

        /// <summary>
        /// Коефіцієнт росту вартості й часу прокачки на 1 рівень юніта:
        /// крок(level) = base × LevelUpCostGrowth^(level-1). Стрибок через
        /// кілька рівнів — сума кроків (§5.2 GDD).
        /// </summary>
        public double LevelUpCostGrowth { get; set; } = 1.35;
    }
}
