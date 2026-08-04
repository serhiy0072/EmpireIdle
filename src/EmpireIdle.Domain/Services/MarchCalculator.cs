namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Рахує час походу: відстань, швидкість найповільнішого юніта
    /// та середня складність рельєфу на шляху.
    /// </summary>
    public class MarchCalculator
    {
        private readonly TerrainGenerator _terrain;
        private readonly List<UnitConfig> _unitConfigs;

        public MarchCalculator(TerrainGenerator terrain, List<UnitConfig> unitConfigs)
        {
            _terrain = terrain;
            _unitConfigs = unitConfigs;
        }

        /// <summary>
        /// Час у дорозі в одну сторону.
        /// </summary>
        /// <param name="units">Склад армії (тип → кількість).</param>
        public TimeSpan CalculateDuration(int serverId, int fromX, int fromY, int toX, int toY,
            IReadOnlyDictionary<string, int> units)
        {
            var distance = Math.Sqrt(Math.Pow(toX - fromX, 2) + Math.Pow(toY - fromY, 2));
            if (distance <= 0)
                return TimeSpan.Zero;

            // Швидкість колони = швидкість найповільнішого юніта
            var speed = units.Keys
                .Select(type => _unitConfigs.FirstOrDefault(u => u.Key == type))
                .Where(c => c is not null)
                .Select(c => c!.Stats.GetValueOrDefault("Speed", 1.0))
                .DefaultIfEmpty(1.0)
                .Min();

            if (speed <= 0)
                speed = 1.0;

            var terrainFactor = GetAverageMoveCost(serverId, fromX, fromY, toX, toY);

            // Одна клітина за 1/speed хвилин, скоригована на рельєф
            var minutes = distance / speed * terrainFactor;
            return TimeSpan.FromMinutes(minutes);
        }

        /// <summary>
        /// Середня вартість руху по клітинах прямої лінії (алгоритм Брезенхема).
        /// Спрощення MVP: справжній pathfinding з обходом води — post-MVP.
        /// </summary>
        private double GetAverageMoveCost(int serverId, int fromX, int fromY, int toX, int toY)
        {
            var dx = Math.Abs(toX - fromX);
            var dy = Math.Abs(toY - fromY);
            var stepX = fromX < toX ? 1 : -1;
            var stepY = fromY < toY ? 1 : -1;
            var error = dx - dy;

            var x = fromX;
            var y = fromY;
            var total = 0.0;
            var cells = 0;

            while (true)
            {
                if (_terrain.IsInBounds(x, y))
                {
                    var cell = _terrain.GetTerrain(serverId, x, y);
                    // Непрохідна клітина не блокує, а сильно сповільнює (армія обходить)
                    total += cell.Passable ? cell.MoveCost : cell.MoveCost * 3.0;
                    cells++;
                }

                if (x == toX && y == toY)
                    break;

                var doubledError = 2 * error;
                if (doubledError > -dy) { error -= dy; x += stepX; }
                if (doubledError < dx) { error += dx; y += stepY; }
            }

            return cells > 0 ? total / cells : 1.0;
        }
    }
}