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
        private readonly WorldGeometry _geometry;
        private readonly IRandomSource _random;

        public MonsterSpawner(TerrainGenerator terrain, MapConfig mapConfig, GameCatalog catalog, WorldGeometry geometry, IRandomSource random)
        {
            _terrain = terrain;
            _mapConfig = mapConfig;
            _catalog = catalog;
            _geometry = geometry;
            _random = random;
        }

        /// <summary>
        /// Скільки монстрів має бути на карті за поточної щільності.
        ///
        /// Рахується від ВІДКРИТОЇ площі, не від повної: на першому рівні
        /// сервера доступно близько 16% карти, і щільність у зоні, куди
        /// гравці мають доступ, була б у шість разів вищою за задуману.
        /// </summary>
        public int GetTargetPopulation(int serverLevel)
        {
            var boundary = _geometry.SettlementBoundary(serverLevel);
            var side = boundary * 2 + 1;

            return side * side / Math.Max(1, _mapConfig.CellsPerMonster);
        }

        /// <summary>
        /// Підбирає параметри одного монстра.
        /// </summary>
        /// <param name="serverId">Світ.</param>
        /// <param name="isOccupied">Перевірка зайнятості клітини (звертається до БД).</param>
        /// <returns>null, якщо вільного місця не знайшлося.</returns>
        public async Task<(string Type, int Level, int X, int Y)?> TrySpawnAsync(int serverId, int serverLevel, Func<int, int, Task<bool>> isOccupied, int maxAttempts = 50)
        {
            // Доступні типи: ті, що відкрились на поточному рівні світу
            var available = _catalog.Monsters.Values
                .Where(m => m.RequiresServerLevel <= serverLevel)
                .ToList();

            if (available.Count == 0)
                return null;

            var (cx, cy) = _geometry.Centre;
            var boundary = _geometry.SettlementBoundary(serverLevel);

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var x = cx + _random.Next(-boundary, boundary + 1);
                var y = cy + _random.Next(-boundary, boundary + 1);

                if (!_terrain.IsHabitable(serverId, x, y))
                    continue;

                if (await isOccupied(x, y))
                    continue;

                var config = available[_random.Next(available.Count)];
                var level = PickLevel(config, x, y, serverLevel);

                return (config.Key, level, x, y);
            }

            return null;
        }

        /// <summary>
        /// Рівень залежить від кільця: околиці — слабкі, центр — сильні.
        /// Рахується від кільця, а не від евклідової відстані: інакше монстр
        /// у куті центрального кільця був би слабшим за монстра на осі,
        /// хоч гравець бачить їх в одній зоні.
        /// </summary>
        private int PickLevel(MonsterConfig config, int x, int y, int serverLevel)
        {
            var proximity = _geometry.Proximity(x, y, serverLevel);

            var span = config.MaxLevel - config.MinLevel;
            var level = config.MinLevel + (int)Math.Round(span * proximity);

            // ±1 розкид, щоб сусідні монстри відрізнялись
            level += _random.Next(-1, 2);

            return Math.Clamp(level, config.MinLevel, config.MaxLevel);
        }
    }
}
