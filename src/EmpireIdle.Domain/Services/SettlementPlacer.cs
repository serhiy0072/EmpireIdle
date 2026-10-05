
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

                // Межа туману на верхніх рівнях сягає краю карти: centre + radius уже поза нею
                if (!_terrain.IsInBounds(x, y) || !_terrain.IsHabitable(serverId, x, y))
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

        /// <summary>
        /// Найближча до (x, y) вільна придатна клітина відкритої зони — телепорт до лідера клану (GDD §8.9).
        /// Ближчі за евклідовою відстанню — раніше: гравці, що прилітають один за одним, щільно оточують ціль.
        /// Зайнятість питаємо квадратом і подвоюємо його, поки місце не знайдеться.
        /// Null — у відкритій зоні вільної клітини немає.
        /// </summary>
        /// <param name="occupiedIn">Зайняті клітини квадрата (minX, minY, maxX, maxY) — один запит до БД.</param>
        public async Task<(int X, int Y)?> FindSpotNearAsync(
            int serverId,
            int serverLevel,
            int x,
            int y,
            Func<int, int, int, int, Task<IReadOnlyCollection<(int X, int Y)>>> occupiedIn,
            int initialRadius = 4)
        {
            var (cx, cy) = _geometry.Centre;

            // Квадрат із таким радіусом уже накриває всю відкриту зону — далі шукати нема де
            var maxRadius = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) + _geometry.SettlementBoundary(serverLevel);

            for (var radius = Math.Clamp(initialRadius, 1, maxRadius); ; radius = Math.Min(radius * 2, maxRadius))
            {
                var occupied = (await occupiedIn(x - radius, y - radius, x + radius, y + radius)).ToHashSet();

                // Сортуємо весь квадрат, а перевірки ліниві: перша придатна клітина й є відповіддю
                var spot = Square(x, y, radius)
                    .OrderBy(c => (c.X - x) * (c.X - x) + (c.Y - y) * (c.Y - y))
                    .ThenBy(c => c.Y)
                    .ThenBy(c => c.X)
                    .Where(c => !occupied.Contains(c)
                        && _terrain.IsInBounds(c.X, c.Y)
                        && _geometry.IsWithinFog(c.X, c.Y, serverLevel)
                        && _terrain.IsHabitable(serverId, c.X, c.Y))
                    .Select(c => ((int X, int Y)?)c)
                    .FirstOrDefault();

                if (spot is not null || radius >= maxRadius)
                    return spot;
            }
        }

        private static IEnumerable<(int X, int Y)> Square(int x, int y, int radius)
        {
            for (var dy = -radius; dy <= radius; dy++)
                for (var dx = -radius; dx <= radius; dx++)
                    yield return (x + dx, y + dy);
        }
    }
}
