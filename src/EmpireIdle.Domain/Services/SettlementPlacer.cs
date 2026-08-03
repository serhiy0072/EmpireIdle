
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
        private readonly MapConfig _config;

        public SettlementPlacer(TerrainGenerator terrain, MapConfig config)
        {
            _terrain = terrain;
            _config = config;
        }

        /// <summary>
        /// Знаходить координати для нового села.
        /// </summary>
        /// <param name="serverId">Світ, у якому селимо.</param>
        /// <param name="isOccupied">Перевірка зайнятості клітини (звертається до БД).</param>
        /// <param name="maxAttempts">Скільки випадкових точок спробувати, перш ніж здатися.</param>
        public async Task<(int X, int Y)> FindSpotAsync(
            int serverId,
            Func<int, int, Task<bool>> isOccupied,
            int maxAttempts = 200)
        {
            var random = Random.Shared;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var x = random.Next(_config.Width);
                var y = random.Next(_config.Height);

                if (!_terrain.IsHabitable(serverId, x, y))
                    continue;

                if(await isOccupied(x, y))
                    continue;

                return (x, y);
            }

            throw new InvalidOperationException(
               $"No free habitable cell found on server {serverId} after {maxAttempts} attempts.");
        }
    }
}
