namespace EmpireIdle.Domain.Services.Config
{

    /// <summary>Параметри карти світу (per-server у майбутньому).</summary>
    public class MapConfig
    {
        /// <summary>Ширина карти в клітинах.</summary>
        public int Width { get; set; } = 1000;

        /// <summary>Висота карти в клітинах.</summary>
        public int Height { get; set; } = 1000;

        /// <summary>Скільки клітин карти припадає на одного монстра (щільність спавну).</summary>
        public int CellsPerMonster { get; set; } = 500;

        /// <summary>Сід генерації терейну — той самий сід дає ту саму карту.</summary>
        public int TerrainSeed { get; set; }

        /// <summary>Типи місцевості з їхніми вагами та властивостями.</summary>
        public List<TerrainConfig> Terrains { get; set; } = new();

        /// <summary>
        /// Рівень світу, на якому туман і кільця досягають повного розміру. Сам рівень світу
        /// стелі не має (GDD §2.7): далі світ росте контентом, а не площею.
        /// </summary>
        public int FullyOpenAtLevel { get; set; } = 3;

        /// <summary>Межі кілець і туману як частки радіуса карти.</summary>
        public MapGeometryConfig Geometry { get; set; } = new();

        /// <summary>Правила росту рівня світу.</summary>
        public ServerEvolutionConfig Evolution { get; set; } = new();
    }
}
