namespace EmpireIdle.Domain.Services
{
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
