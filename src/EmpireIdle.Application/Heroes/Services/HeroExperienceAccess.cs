using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Heroes.Services
{
    /// <summary>
    /// Пул досвіду гравця «знайти або створити». Нагорода, баночка й скидання рівня
    /// поповнюють той самий пул, а створювати його кожен мав би однаково.
    /// </summary>
    internal static class HeroExperienceAccess
    {
        public static async Task<HeroExperiencePool> GetOrCreateExperienceAsync(this IHeroRepository heroes,
            Guid playerId, int serverId, CancellationToken cancellationToken)
        {
            var pool = await heroes.GetExperienceAsync(playerId, cancellationToken);

            if (pool is not null)
                return pool;

            pool = new HeroExperiencePool(Guid.NewGuid(), playerId, serverId);
            await heroes.AddExperienceAsync(pool, cancellationToken);

            return pool;
        }
    }
}
