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

        /// <summary>Скільки рівнів може мати сервер. Останній — максимальна геометрія.</summary>
        public int MaxServerLevel { get; set; } = 3;

        /// <summary>Межі кілець і туману як частки радіуса карти.</summary>
        public MapGeometryConfig Geometry { get; set; } = new();
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

    /// <summary>
    /// Геометрія світу в частках радіуса карти, не в клітинах.
    ///
    /// Частки, бо вони виражають дизайн, а не розмір: «центр — п'ята частина
    /// радіуса» лишається правдою на 300×300 і на 500×500. Абсолютні відстані
    /// довелося б переписувати руками при кожній зміні розміру, і помилка
    /// дала б кільце за межами карти.
    /// </summary>
    public class MapGeometryConfig
    {
        /// <summary>
        /// Зовнішні межі кілець як частки радіуса, від центру назовні,
        /// на МАКСИМАЛЬНОМУ рівні сервера.
        ///
        /// На одну менше за RingMultipliers: останнє кільце — все, що далі
        /// за останню межу, окремої межі для нього не треба.
        /// </summary>
        public List<double> RingBoundaries { get; set; } = new();

        /// <summary>Множник виробітку за кільцями, від центру назовні.</summary>
        public List<double> RingMultipliers { get; set; } = new();

        /// <summary>На скільки кільця вужчі на першому рівні відносно максимального.</summary>
        public double RingsAtFirstLevel { get; set; } = 0.40;

        /// <summary>Доступна для заселення частка радіуса на першому рівні сервера.</summary>
        public double FogMinShare { get; set; } = 0.40;

        /// <summary>Доступна частка на максимальному рівні.</summary>
        public double FogMaxShare { get; set; } = 1.0;
    }
}
