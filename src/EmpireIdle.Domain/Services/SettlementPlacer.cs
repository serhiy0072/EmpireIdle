
namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Підбирає місце для нового села: придатна за місцевістю і вільна клітина.
    /// Пошук іде по спіралі від випадкової стартової точки — щоб гравці
    /// не купчилися в одному куті й не спавнились на воді.
    /// </summary>
    public class SettlementPlacer
    {
        private readonly TerrainGenerator _terrain;
        private readonly WorldGeometry _geometry;
        private readonly IRandomSource _random;

        public SettlementPlacer(TerrainGenerator terrain, WorldGeometry geometry, IRandomSource random)
        {
            _terrain = terrain;
            _geometry = geometry;
            _random = random;
        }

        /// <summary>
        /// Знаходить координати для нового села.
        /// </summary>
        /// <param name="serverId">Світ, у якому селимо.</param>
        /// <param name="isOccupied">Перевірка зайнятості клітини (звертається до БД).</param>
        /// <param name="maxAttempts">Скільки випадкових точок спробувати, перш ніж здатися.</param>
        /// <param name="serverLevel">Рівень світу — визначає відкриту для заселення межу.</param>
        public async Task<(int X, int Y)> FindSpotAsync(
            int serverId,
            int serverLevel,
            Func<int, int, Task<bool>> isOccupied,
            int maxAttempts = 200)
        {
            var (cx, cy) = _geometry.Centre;
            var boundary = _geometry.SettlementBoundary(serverLevel);

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Кидаємо одразу в межах туману: рандом по всій карті на першому
                // рівні промазував би повз відкриту зону в більшості спроб
                var x = cx + _random.Next(-boundary, boundary + 1);
                var y = cy + _random.Next(-boundary, boundary + 1);

                if (!_terrain.IsHabitable(serverId, x, y))
                    continue;

                if (await isOccupied(x, y))
                    continue;

                return (x, y);
            }

            throw new InvalidOperationException(
                $"No free habitable cell found on server {serverId} within radius {boundary} after {maxAttempts} attempts.");
        }

        /// <summary>
        /// Місце для виселеного села (GDD §2.6): спершу те саме кільце —
        /// виселення це «тебе зсунули», не «почни спочатку», — далі найближчі
        /// до нього, зовнішнє раніше за внутрішнє. Null — вільного місця немає,
        /// і виселення не відбувається.
        /// </summary>
        public async Task<(int X, int Y)?> FindSpotNearRingAsync(
            int serverId,
            int serverLevel,
            int ring,
            Func<int, int, Task<bool>> isOccupied,
            int attemptsPerRing = 100)
        {
            var (cx, cy) = _geometry.Centre;

            var rings = Enumerable.Range(0, _geometry.RingCount)
                .OrderBy(r => Math.Abs(r - ring))
                .ThenByDescending(r => r);

            foreach (var candidate in rings)
            {
                if (_geometry.RingDistanceBounds(candidate, serverLevel) is not { } bounds)
                    continue;

                for (var attempt = 0; attempt < attemptsPerRing; attempt++)
                {
                    // Відстань Чебишева d — це периметр квадрата: обираємо d у межах
                    // кільця, потім сторону квадрата й точку на ній
                    var d = _random.Next(bounds.Min, bounds.Max + 1);
                    var t = _random.Next(-d, d + 1);

                    var (x, y) = _random.Next(4) switch
                    {
                        0 => (cx + t, cy - d),
                        1 => (cx + t, cy + d),
                        2 => (cx - d, cy + t),
                        _ => (cx + d, cy + t)
                    };

                    // На відстані рівно в радіус периметр виходить за край карти
                    if (!_terrain.IsInBounds(x, y) || !_terrain.IsHabitable(serverId, x, y))
                        continue;

                    if (await isOccupied(x, y))
                        continue;

                    return (x, y);
                }
            }

            return null;
        }
    }
}
