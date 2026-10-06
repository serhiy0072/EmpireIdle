using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій героїв (EF Core).</summary>
    public class HeroRepository : IHeroRepository
    {
        private readonly AppDbContext _context;

        public HeroRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<List<Hero>> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.Heroes
            .Where(h => h.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<Hero>> GetByPlayerReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.Heroes
            .AsNoTracking()
            .Where(h => h.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        /// <remarks>
        /// Спершу поточна одиниця роботи: герой, щойно виданий у тій самій серії роллів,
        /// у базі ще не видно, а дубль мав піти в сузір'я, а не в другий INSERT.
        /// </remarks>
        public async Task<Hero?> GetByKeyAsync(Guid playerId, string heroKey, CancellationToken cancellationToken = default)
            => _context.Heroes.Local.FirstOrDefault(h => h.PlayerId == playerId && h.HeroKey == heroKey)
               ?? await _context.Heroes
                   .FirstOrDefaultAsync(h => h.PlayerId == playerId && h.HeroKey == heroKey, cancellationToken);

        /// <inheritdoc/>
        public Task<Hero?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Heroes
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

        public Task<List<Hero>> GetByIdsReadOnlyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
            => _context.Heroes
            .AsNoTracking()
            .Where(h => ids.Contains(h.Id))
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountAvailableAsync(Guid playerId, Guid garrisonId,
            CancellationToken cancellationToken = default)
            => _context.Heroes
            .CountAsync(h => h.PlayerId == playerId
                && h.StationedGarrisonId == garrisonId
                && h.State == HeroState.Idle, cancellationToken);

        /// <inheritdoc/>
        public Task<List<Hero>> GetByGarrisonAsync(Guid garrisonId, CancellationToken cancellationToken = default)
            => _context.Heroes
            .Where(h => h.StationedGarrisonId == garrisonId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        /// <remarks>
        /// Лідер, призначений у цій же транзакції, у базі ще не видно — тож спершу
        /// одиниця роботи. Відповідь бази звіряємо ще раз уже на трекнутому
        /// екземплярі: лідерство могли зняти в пам'яті, не зберігши.
        /// </remarks>
        public async Task<Hero?> GetLeaderAsync(Guid garrisonId, Guid playerId, CancellationToken cancellationToken = default)
        {
            bool IsLeaderHere(Hero h) => h.StationedGarrisonId == garrisonId && h.PlayerId == playerId && h.IsLeader;

            var local = _context.Heroes.Local.FirstOrDefault(IsLeaderHere);

            if (local is not null)
                return local;

            var stored = await _context.Heroes
                .FirstOrDefaultAsync(h => h.StationedGarrisonId == garrisonId
                    && h.PlayerId == playerId
                    && h.IsLeader, cancellationToken);

            return stored is not null && IsLeaderHere(stored) ? stored : null;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Guid>> GetForeignGarrisonIdsAsync(Guid playerId,
            CancellationToken cancellationToken = default)
            => await _context.Heroes
            .Where(h => h.PlayerId == playerId && h.StationedGarrisonId != null)
            .Join(_context.Garrisons, h => h.StationedGarrisonId, g => g.Id, (h, g) => new { Hero = h, g.HostId, g.HostKind })
            // Гарнізон кланової споруди чужий завжди: власного господаря в нього немає
            .Where(x => x.HostKind == GarrisonHost.ClanStructure
                || _context.Villages.Any(v => v.Id == x.HostId && v.PlayerId != playerId))
            .Select(x => x.Hero.StationedGarrisonId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(Hero hero, CancellationToken cancellationToken = default)
        {
            await _context.Heroes.AddAsync(hero, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<HeroShardProgress?> GetShardsAsync(Guid playerId, string heroKey, CancellationToken cancellationToken = default)
            => _context.HeroShards
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.HeroKey == heroKey, cancellationToken);

        /// <inheritdoc/>
        public async Task AddShardsAsync(HeroShardProgress progress, CancellationToken cancellationToken = default)
        {
            await _context.HeroShards.AddAsync(progress, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<List<HeroShardProgress>> GetAllShardsAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.HeroShards
            .AsNoTracking()
            .Where(s => s.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task<HeroExperiencePool?> GetExperienceAsync(Guid playerId, CancellationToken cancellationToken = default)
            // Спершу трекер: пул, створений раніше в цій самій транзакції (нагорода з кількох рядків
            // досвіду), у базі ще не видно, і друге створення впало б на унікальному індексі
            => _context.HeroExperience.Local.FirstOrDefault(p => p.PlayerId == playerId)
               ?? await _context.HeroExperience.FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddExperienceAsync(HeroExperiencePool pool, CancellationToken cancellationToken = default)
        {
            await _context.HeroExperience.AddAsync(pool, cancellationToken);
        }
    }
}
