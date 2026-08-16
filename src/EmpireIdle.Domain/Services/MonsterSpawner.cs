namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Підбирає монстрів для засіву карти: тип (за рівнем сервера),
    /// рівень (за відстанню до центру) і придатну вільну клітину.
    /// </summary>
    public class MonsterSpawner
    {
        private readonly TerrainGenerator _terrain;
        private readonly MapConfig _mapConfig;
        private readonly GameCatalog _catalog;

        public MonsterSpawner(TerrainGenerator terrain, MapConfig mapConfig, GameCatalog catalog)
        {
            _terrain = terrain;
            _mapConfig = mapConfig;
            _catalog = catalog;
        }

        /// <summary>Скільки монстрів має бути на карті за поточної щільності.</summary>
        public int GetTargetPopulation()
            => _mapConfig.Width * _mapConfig.Height / Math.Max(1, _mapConfig.CellsPerMonster);

        /// <summary>
        /// Підбирає параметри одного монстра.
        /// </summary>
        /// <param name="serverId">Світ.</param>
        /// <param name="isOccupied">Перевірка зайнятості клітини (звертається до БД).</param>
        /// <returns>null, якщо вільного місця не знайшлося.</returns>
        public async Task<(string Type, int Level, int X, int Y)?> TrySpawnAsync(
            int serverId,
            Func<int, int, Task<bool>> isOccupied,
            int maxAttempts = 50)
        {
            // Доступні типи: ті, що відкрились на поточному рівні світу
            var available = _catalog.Monsters.Values
                .Where(m => m.RequiresServerLevel <= _mapConfig.ServerLevel)
                .ToList();

            if (available.Count == 0)
                return null;

            var random = Random.Shared;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var x = random.Next(_mapConfig.Width);
                var y = random.Next(_mapConfig.Height);

                if (!_terrain.IsHabitable(serverId, x, y))
                    continue;

                if (await isOccupied(x, y))
                    continue;

                var config = available[random.Next(available.Count)];
                var level = PickLevel(config, x, y, random);

                return (config.Key, level, x, y);
            }

            return null;
        }

        /// <summary>
        /// Рівень залежить від близькості до центру карти: край — слабкі,
        /// центр — сильні. Невеликий розкид, щоб сусідні монстри відрізнялись.
        /// </summary>
        private int PickLevel(MonsterConfig config, int x, int y, Random random)
        {
            var centerX = _mapConfig.Width / 2.0;
            var centerY = _mapConfig.Height / 2.0;

            var dx = (x - centerX) / centerX;   // −1..1
            var dy = (y - centerY) / centerY;
            var distance = Math.Min(1.0, Math.Sqrt(dx * dx + dy * dy)); // 0 = центр, 1 = край

            // Чим ближче до центру, тим вища частка діапазону рівнів
            var proximity = 1.0 - distance;
            var span = config.MaxLevel - config.MinLevel;
            var level = config.MinLevel + (int)Math.Round(span * proximity);

            // ±1 розкид, у межах діапазону типу
            level += random.Next(-1, 2);
            return Math.Clamp(level, config.MinLevel, config.MaxLevel);
        }
    }
}
