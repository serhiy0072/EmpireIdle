namespace EmpireIdle.Domain.Services
{

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
}
