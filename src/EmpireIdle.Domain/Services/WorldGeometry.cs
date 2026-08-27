namespace EmpireIdle.Domain.Services
{
    /// <summary>Кільце карти, від центру назовні.</summary>
    public enum MapRing
    {
        Centre = 0,
        Middle = 1,
        Outskirts = 2
    }

    /// <summary>
    /// Геометрія світу: кільця, туман, множники виробітку.
    ///
    /// Усе — чисті функції від координат і рівня сервера. У БД нічого не
    /// зберігається: кільце клітини завжди можна порахувати, а збережене
    /// значення розійшлося б із конфігом при першому ж ребалансі.
    /// </summary>
    public class WorldGeometry
    {
        private readonly MapConfig _map;

        public WorldGeometry(MapConfig map) => _map = map;

        /// <summary>Радіус карти в клітинах.</summary>
        public int Radius => Math.Min(_map.Width, _map.Height) / 2;

        /// <summary>Центр карти.</summary>
        public (int X, int Y) Centre => (_map.Width / 2, _map.Height / 2);

        /// <summary>
        /// Відстань Чебишева до центру. Кільця виходять квадратними, як і сітка —
        /// евклідова відстань дала б круги на квадратній карті, і кутові клітини
        /// поводились би не так, як здається гравцю.
        /// </summary>
        public int DistanceToCentre(int x, int y)
        {
            var (cx, cy) = Centre;

            return Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
        }

        /// <summary>Кільце, у якому лежить клітина при заданому рівні сервера.</summary>
        public MapRing RingAt(int x, int y, int serverLevel)
        {
            var distance = DistanceToCentre(x, y);
            var growth = LevelProgress(serverLevel);

            var centre = Scale(_map.Geometry.CentreShare, growth);
            var middle = Scale(_map.Geometry.MiddleShare, growth);

            if (distance <= centre) return MapRing.Centre;
            if (distance <= middle) return MapRing.Middle;

            return MapRing.Outskirts;
        }

        /// <summary>Множник виробітку для клітини.</summary>
        public double ProductionMultiplierAt(int x, int y, int serverLevel)
        {
            var ring = (int)RingAt(x, y, serverLevel);
            var multipliers = _map.Geometry.RingMultipliers;

            return ring < multipliers.Count ? multipliers[ring] : 1.0;
        }

        /// <summary>
        /// Найдальша відстань, на якій дозволено селитись. Росте з рівнем сервера:
        /// місця вистачає всім, а конкуренція за центр посилюється, бо центр
        /// росте повільніше за доступну площу.
        /// </summary>
        public int SettlementBoundary(int serverLevel)
        {
            var geometry = _map.Geometry;
            var share = geometry.FogMinShare
                        + (geometry.FogMaxShare - geometry.FogMinShare) * LevelProgress(serverLevel);

            return (int)(Radius * share);
        }

        /// <summary>Чи відкрита клітина для заселення на цьому рівні сервера.</summary>
        public bool IsWithinFog(int x, int y, int serverLevel)
            => DistanceToCentre(x, y) <= SettlementBoundary(serverLevel);

        /// <summary>Прогрес рівня від 0.0 (перший) до 1.0 (максимальний).</summary>
        private double LevelProgress(int serverLevel)
        {
            if (_map.MaxServerLevel <= 1)
                return 1.0;

            var clamped = Math.Clamp(serverLevel, 1, _map.MaxServerLevel);

            return (clamped - 1.0) / (_map.MaxServerLevel - 1.0);
        }

        /// <summary>Частка радіуса, звужена відповідно до рівня сервера.</summary>
        private int Scale(double shareAtMaxLevel, double growth)
        {
            var atFirst = shareAtMaxLevel * _map.Geometry.RingsAtFirstLevel;
            var share = atFirst + (shareAtMaxLevel - atFirst) * growth;

            return (int)(Radius * share);
        }
    }
}
