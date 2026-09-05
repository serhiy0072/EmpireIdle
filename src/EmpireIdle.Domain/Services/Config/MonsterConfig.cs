namespace EmpireIdle.Domain.Services.Config
{
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
}
