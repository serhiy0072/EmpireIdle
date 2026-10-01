using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Рахує час походу: відстань, швидкість найповільнішого юніта
    /// та середня складність рельєфу на шляху.
    /// </summary>
    public class MarchCalculator
    {
        private readonly TerrainGenerator _terrain;
        private readonly GameCatalog _catalog;

        public MarchCalculator(TerrainGenerator terrain, GameCatalog catalog)
        {
            _terrain = terrain;
            _catalog = catalog;
        }

        /// <summary>
        /// Час у дорозі в одну сторону.
        /// </summary>
        /// <param name="units">Склад армії (тип → кількість).</param>
        /// <param name="heroSpeed">
        /// Швидкість героя, що веде похід. Колона йде за найповільнішим,
        /// і герой у цьому рахунку нарівні з юнітами: інакше підкріплення
        /// з самого героя завжди йшло б базовою швидкістю, а важкий герой
        /// не сповільнював би легку кінноту.
        /// </param>
        public TimeSpan CalculateDuration(int serverId, int fromX, int fromY, int toX, int toY,
            IReadOnlyDictionary<UnitStackKey, int> units, double? heroSpeed = null)
        {
            // Швидкість колони = швидкість найповільнішого учасника. Рівень юніта
            // на швидкість не впливає — прокачка стосується бою, не логістики.
            var speeds = units.Keys
                .Select(stack => _catalog.Units.GetValueOrDefault(stack.UnitType))
                .Where(c => c is not null)
                .Select(c => c!.Stats.GetValueOrDefault("Speed", 1.0))
                .ToList();

            if (heroSpeed is { } hero && hero > 0)
                speeds.Add(hero);

            return CalculateDuration(serverId, fromX, fromY, toX, toY, speeds.DefaultIfEmpty(1.0).Min());
        }

        /// <summary>Час дороги із заданою швидкістю колони — для розвідників, у яких юнітів немає.</summary>
        public TimeSpan CalculateDuration(int serverId, int fromX, int fromY, int toX, int toY, double speed)
        {
            var distance = Math.Sqrt(Math.Pow(toX - fromX, 2) + Math.Pow(toY - fromY, 2));
            if (distance <= 0)
                return TimeSpan.Zero;

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
