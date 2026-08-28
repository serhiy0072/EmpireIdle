namespace EmpireIdle.Domain.Services
{

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
}
